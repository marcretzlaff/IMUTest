using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;
using Xamarin.Essentials;
using Java.IO;
using Android.Text.Format;
using System.IO;
using System.Runtime.InteropServices.ComTypes;
using MathNet.Filtering.Kalman;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using Android.Hardware;
using System.Numerics;
using CsvHelper;
using System.Globalization;
using System.Threading;
using Android.App;

namespace IMUTest
{
    public partial class MainPage : ContentPage
    {
        public static string dkfpath = null;
        private string accloadpath = null;
        List<Records> inputrecords;
        SensorFusion fusion = null;

        public static List<string> listresult = new List<string>();

        public MainPage()
        {
            InitializeComponent();
            fusion = SensorFusion.GetSensorFusion();
            var storagepath = DependencyService.Resolve<IFileSystem>().GetExternalStorage();
            dkfpath = System.IO.Path.Combine(storagepath, "dkfdata.csv");
            accloadpath = System.IO.Path.Combine(storagepath, "enddata.csv");

            if (System.IO.File.Exists(dkfpath)) System.IO.File.Delete(dkfpath);

            using (var reader = new StreamReader(accloadpath))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                csv.Configuration.HasHeaderRecord = false;
                csv.Configuration.Delimiter = ";";
                inputrecords = csv.GetRecords<Records>().ToList();
            }
        }

        #region buttons
        private void Button_Clicked(object sender, EventArgs e)
        {
            label_onoff.Text = "Started";
            foreach(var row in inputrecords)
            {
                if(row.type == "Beacon")
                {
                    var vec = new Vector((float)row.X, (float)row.Y, (float)row.Z);
                    fusion.KalmanFusion(vec, ManagerTypes.BEACON, new PositionUpdatedEventArgs(vec), new TimeSpan());
                }
                else
                {
                    var vec = new Vector((float)row.X, (float)row.Y,(float) row.Z);
                    fusion.KalmanFusion(vec, ManagerTypes.IMU, new PositionUpdatedEventArgs(vec), TimeSpan.FromSeconds(row.span));
                }
            }
            label_onoff.Text = "Finished";
        }

        private void Button_Clicked2(object sender, EventArgs e)
        {
            label_onoff.Text = "OFF";
            foreach ( string x in listresult)
            System.IO.File.AppendAllText(dkfpath, x);

        }
        private void Button_Clicked3(object sender, EventArgs e)
        {
            button_start.IsEnabled = false;
            label_cali.Text = "Calibration";
            Device.StartTimer(TimeSpan.FromSeconds(5), () =>
            {
                label_cali.Text = "Calibrated";
                return false; // return true to repeat counting, false to stop timer
            });
            button_start.IsEnabled = true;
        }
        #endregion buttons


        public class SensorFusion
        {
            /// Einzige Instanz des SensorFusion, da dieser ein Singelton ist.
            private static readonly SensorFusion _thisManager = new SensorFusion();
            private enum SensorFusionState { Uninitzialized, Inizialized };
            private SensorFusionState State
            {
                get;
                set;
            }

            #region Matrizen
            private Matrix<double> x0 = Matrix<double>.Build.Dense(6, 1, new[] { 0d, 0d, 0d, 0d, 0d, 0d }); // State Representation: [ x y x' y' x'' y'']
            private Matrix<double> p0 = Matrix<double>.Build.Dense(6, 6, new[] { 5d, 0d, 0d, 0d, 0d, 0d,  //Covariance of inital State (same as x0 with m x m) as we start at zero
                                                                                 0d, 5d, 0d, 0d, 0d, 0d,
                                                                                 0d, 0d, 1d, 0d, 0d, 0d,
                                                                                 0d, 0d, 0d, 1d, 0d, 0d,
                                                                                 0d, 0d, 0d, 0d, 1d, 0d,
                                                                                 0d, 0d, 0d, 0d, 0d, 1d});

            //used with Beacon Event
            private Matrix<double> H_position = Matrix<double>.Build.Dense(2, 6, new[] { 1d, 0d, 0d, 0d, 0d, 0d,      // Measurement Model: [ 1 0 0 0 0 0
                                                                                        0d, 1d, 0d, 0d, 0d, 0d });    //                      0 1 0 0 0 0 ]

            private Matrix<double> R_position = Matrix<double>.Build.Dense(2, 2, new[] { 1d, 0d,          //covariance of measurments BEACON
                                                                                         0d, 1d });

            //used with IMU Event
            private Matrix<double> H_acceleration = Matrix<double>.Build.Dense(2, 6, new[] { 0d, 0d, 0d, 0d, 1d, 0d,      // Measurement Model: [ 0 0 0 0 1 0
                                                                                            0d, 0d, 0d, 0d, 0d, 1d });    //                      0 0 0 0 0 1 ]

            private Matrix<double> R_acceleration = Matrix<double>.Build.Dense(2, 2, new[] { 0.3d, 0d,           //covariance of measurments IMU
                                                                                             0d, 0.3d });
            //commonly used 
            //ca. 50ms for next step 
            //https://nbviewer.jupyter.org/github/balzer82/Kalman/blob/master/Kalman-Filter-CA-2.ipynb?create=1
            private double qdelta = 0.05d;
            private Matrix<double> Q;

            private Matrix<double> F = Matrix<double>.Build.Dense(6, 6); // State Transition Matrix: [ 1 0 T 0 .5T^2   0      example, later filled with time delta
                                                                         //                            0 1 0 T   0   .5T^2
                                                                         //                            0 0 1 0   T     0
                                                                         //                            0 0 0 1   0     T
                                                                         //                            0 0 0 0   1     0
                                                                         //                            0 0 0 0   0     1  ]

            /* needed?????
            Matrix<double> G = Matrix<double>.Build.Dense(6, 4, new[] { 1d, 0d, 0d, 0d, //plant noise matrix
                                                                        0d, 1d, 0d, 0d,
                                                                        0d, 0d, 0d, 0d,
                                                                        0d, 0d, 0d, 0d,
                                                                        0d, 0d, 1d, 0d,
                                                                        0d, 0d, 0d, 1d });

            */
            private Matrix<double> z = Matrix<double>.Build.Dense(2, 1, new[] { 0d, 0d }); // Measurement: [x y]
            #endregion Matrizen

            private DiscreteKalmanFilter dkf;
            private SensorFusion()
            {
                State = SensorFusionState.Uninitzialized;

            //fill q with qdelta
            Q = Matrix<double>.Build.Dense(6, 6, new[] { 0.25d * System.Math.Pow(qdelta,4), 0.25d * System.Math.Pow(qdelta,4), 0.5d * System.Math.Pow(qdelta,3), 0.5d * System.Math.Pow(qdelta,3), 0.5d * System.Math.Pow(qdelta,2), 0.5d * System.Math.Pow(qdelta,2),  //plant noise covariance
                                                             0.25d * System.Math.Pow(qdelta,4), 0.25d * System.Math.Pow(qdelta,4), 0.5d * System.Math.Pow(qdelta,3), 0.5d * System.Math.Pow(qdelta,3), 0.5d * System.Math.Pow(qdelta,2), 0.5d * System.Math.Pow(qdelta,2),
                                                             0.5d * System.Math.Pow(qdelta,3), 0.5d * System.Math.Pow(qdelta,3), System.Math.Pow(qdelta,2), System.Math.Pow(qdelta,2), qdelta, qdelta,
                                                             0.5d * System.Math.Pow(qdelta,3), 0.5d * System.Math.Pow(qdelta,3), System.Math.Pow(qdelta,2), System.Math.Pow(qdelta,2), qdelta, qdelta,
                                                             0.5d * System.Math.Pow(qdelta,2), 0.5d * System.Math.Pow(qdelta,2), qdelta, qdelta, 1, 1,
                                                             0.5d * System.Math.Pow(qdelta,2), 0.5d * System.Math.Pow(qdelta,2), qdelta, qdelta, 1, 1});
            double sa = 4; //standard deviation acceleration
            Q = Q * sa*sa;
            }
            public static SensorFusion GetSensorFusion()
            {
                return _thisManager;
            }

            public Vector KalmanFusion(Vector vec, ManagerTypes manager, IPhysEvent args, TimeSpan timespan)
            {
                if (State == SensorFusionState.Uninitzialized)
                {
                    if (manager == ManagerTypes.BEACON)
                    {
                        x0 = Matrix<double>.Build.Dense(6, 1, new[] { vec.X, vec.Y, 0d, 0d, 0d, 0d });
                        dkf = new DiscreteKalmanFilter(x0, p0);
                        State = SensorFusionState.Inizialized;
                        listresult.Add(manager.ToString() + ';' + "0.0" + ';' + dkf.State[0, 0].ToString().Replace(',', '.') + ';' + dkf.State[1, 0].ToString().Replace(',', '.') +';' + dkf.Cov[0,0].ToString().Replace(',', '.') + ';' + dkf.Cov[1, 0].ToString().Replace(',', '.') + System.Environment.NewLine);
                        return vec;
                    }
                    else return null; //kalman starts with first beacon event -> first location
                }

                if (manager == ManagerTypes.BEACON)
                {
                    z = Matrix<double>.Build.Dense(2, 1, new double[] { vec.X, vec.Y }); // Measurement: [x y]
                    dkf.Update(z, H_position, R_position);
                }
                else
                {
                    //fill state transition matrix
                    F = Matrix<double>.Build.Dense(6, 6, new[] { 1d, 0d, timespan.TotalSeconds, 0d, 0.5 * System.Math.Pow(timespan.TotalSeconds,2), 0d,
                                                                 0d, 1d, 0d, timespan.TotalSeconds, 0d, 0.5 * System.Math.Pow(timespan.TotalSeconds,2),
                                                                 0d, 0d, 1d, 0d, timespan.TotalSeconds, 0d,
                                                                 0d, 0d, 0d, 1d, 0d, timespan.TotalSeconds,
                                                                 0d, 0d, 0d, 0d, 1d, 0d,
                                                                 0d, 0d, 0d, 0d, 0d, 1d});

                    dkf.Predict(F, Q); //predict state when IMU measurement given

                    var acc = (args as PositionUpdatedEventArgs).accvec;
                    z = Matrix<double>.Build.Dense(2, 1, new double[] { acc.X, acc.Y }); // Measurement: [x'' y'']
                    dkf.Update(z, H_acceleration, R_acceleration);
                }

                listresult.Add(manager.ToString() + ';' + timespan.TotalSeconds.ToString().Replace(',', '.') + ';' + dkf.State[0,0].ToString().Replace(',', '.') + ';' + dkf.State[1,0].ToString().Replace(',', '.') + ';' + dkf.Cov[0, 0].ToString().Replace(',', '.') + ';' + dkf.Cov[1, 1].ToString().Replace(',', '.') + System.Environment.NewLine);
                Vector result = new Vector((float)dkf.State[0, 0], (float)dkf.State[1, 0], 0);
                return result;
            }
        }

        public class Vector
        {
            private Vector3 _vec = new Vector3();

            public float X
            {
                get { return _vec.X; }
                set { _vec.X = value; }
            }

            public float Y
            {
                get { return _vec.Y; }
                set { _vec.Y = value; }
            }

            public float Z
            {
                get { return _vec.Z; }
                set { _vec.Z = value; }
            }

            public Vector(float x, float y, float z)
            {
                _vec = new Vector3(x, y, z);
            }

            public Vector(Vector3 vector)
            {
                _vec.X = vector.X;
                _vec.Y = vector.Y;
                _vec.Z = vector.Z;
            }

            public Vector(Vector vector)
            {
                _vec.X = vector.X;
                _vec.Y = vector.Y;
                _vec.Z = vector.Z;
            }

            public static Vector Zero
            {
                get { return new Vector(Vector3.Zero); }
            }

            public static Vector NaN
            {
                get { return new Vector(float.NaN, float.NaN, float.NaN); }
            }

            public static implicit operator Vector3(Vector vector) => new Vector3(vector.X, vector.Y, vector.Z);

            public bool IsNaN()
            {
                return float.IsNaN(X) && float.IsNaN(Y) && float.IsNaN(Z);
            }
        }
        public enum ManagerTypes
        {
            BEACON,
            IMU
        }

        public class PositionUpdatedEventArgs : EventArgs, IPhysEvent
        {
            public Vector posvec;
            public Vector accvec;

            public PositionUpdatedEventArgs(double[] p, double[] a)
            {
                posvec = new Vector((float)p[0], (float)p[1], (float)p[2]);
                accvec = new Vector((float)a[0], (float)a[1], (float)a[2]);
            }

            public PositionUpdatedEventArgs(Vector p)
            {
                accvec = p;
            }
        }

        public interface IPhysEvent
        {
        }

        public class Records
        {
            public string type { get; set; }
            public double span { get; set; }
            public double time { get; set; }
            public double X { get; set; }
            public double Y { get; set; }
            public double Z { get; set; }


            public Records(string partype, double parspan,double partime, double parx, double pary, double parz)
            {
                type = partype;
                span = parspan;
                time = partime;
                X = parx;
                Y = pary;
                Z = parz;

            }
        }
    }
}