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
using Java.Util;
using MathNet.Filtering.Kalman;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using Android.Hardware;

namespace IMUTest
{
    public partial class MainPage : ContentPage
    {
        private IServiceACC accservice = null;
        private IServiceRotation rotationservice = null;

        private DateTime acctime;
        private DateTime[] linacctime = new DateTime[2];
        private TimeSpan linaccspan;
        private float[] rotationMatrix = new float[9];

        private string linpath = null;
        private string lowaccpath = null;
        private string velopath = null;
        private string positionpath = null;
        private string endpath = null;
        private string timepath = null;
        private string dkfpath = null;

        double[,] acceleration = new double[2, 3];
        double[,] xyacceleration = new double[2, 3];
        double[,] velocity = new double[2, 3];
        double[,] position = new double[2, 3];
        double[,] calibration = new double[2, 3];
        private int endcount;

        Matrix<double> x0 = Matrix<double>.Build.Dense(6, 1, new[] { 0d, 0d, 0d, 0d, 0d, 0d }); // State Representation: [ x y x' y' x'' y'']
        Matrix<double> p0 = Matrix<double>.Build.Dense(6, 6); //Covariance of inital State (same as x0 with m x m) as we start at zero
        Matrix<double> H = Matrix<double>.Build.Dense(2, 6, new[] { 0d, 0d, 0d, 0d, 1d, 0d,   // Measurement Model: [ 0 0 0 0 1 0
                                                                    0d, 0d, 0d, 0d, 0d, 1d}); //                      0 0 0 0 0 1 ]

        Matrix<double> R = Matrix<double>.Build.Dense(2, 2, new[] { 0.025d, 0d , 
                                                                    0d, 0.025d});
        DiscreteKalmanFilter dkf = null;

        public MainPage()
        {
            InitializeComponent();
            //Sensor Speed UI: 65ms Default 65ms Game 20ms Fastest 5ms
            Accelerometer.Start(SensorSpeed.Game);
            Accelerometer.ReadingChanged += acc_read;

            var storagepath = DependencyService.Resolve<IFileSystem>().GetExternalStorage();
            linpath = System.IO.Path.Combine(storagepath, "lindata.csv");
            lowaccpath = System.IO.Path.Combine(storagepath, "lowaccdata.csv");
            velopath = System.IO.Path.Combine(storagepath, "velodata.csv");
            positionpath = System.IO.Path.Combine(storagepath, "positiondata.csv");
            endpath = System.IO.Path.Combine(storagepath, "enddata.csv");
            timepath = System.IO.Path.Combine(storagepath, "timedata.csv");
            dkfpath = System.IO.Path.Combine(storagepath, "dkfdata.csv");

            if (System.IO.File.Exists(linpath)) System.IO.File.Delete(linpath);
            if (System.IO.File.Exists(lowaccpath)) System.IO.File.Delete(lowaccpath);
            if (System.IO.File.Exists(velopath)) System.IO.File.Delete(velopath);
            if (System.IO.File.Exists(positionpath)) System.IO.File.Delete(positionpath);
            if (System.IO.File.Exists(endpath)) System.IO.File.Delete(endpath);
            if (System.IO.File.Exists(timepath)) System.IO.File.Delete(timepath);
            if (System.IO.File.Exists(dkfpath)) System.IO.File.Delete(dkfpath);

            dkf = new DiscreteKalmanFilter(x0, p0);
        }

        private void acc_read(object sender, AccelerometerChangedEventArgs e)
        {
            acctime = DateTime.UtcNow;
            label_Zeit.Text = acctime.ToString();
        }

        #region buttons
        private void Button_Clicked(object sender, EventArgs e)
        {
            accservice = DependencyService.Resolve<IServiceACC>();
            rotationservice = DependencyService.Resolve<IServiceRotation>();
            rotationservice.Init();
            accservice.Init();

            Array.Clear(velocity, 0, velocity.Length);
            Array.Clear(position, 0, position.Length);
            Array.Clear(acceleration, 0, acceleration.Length);
            Array.Clear(calibration, 0, calibration.Length);
            linacctime[1] = DateTime.UtcNow;
            label_onoff.Text = "ON";

            rotationservice.ValuesChanged += save_rotation;
            accservice.ValuesChanged += SaveLin;
        }

        private void save_rotation(object sender, EventArgs e)
        {
            RotationEventArgs args = e as RotationEventArgs;
            SensorManager.GetRotationMatrixFromVector(rotationMatrix,args.values);
        }


        private void Button_Clicked2(object sender, EventArgs e)
        {
            accservice.Stop();
            label_onoff.Text = "OFF";
            rotationservice.ValuesChanged -= save_rotation;
            accservice.ValuesChanged -= SaveLin;
        }

        private void Button_Clicked3(object sender, EventArgs e)
        {
            button_start.IsEnabled = false;
            label_cali.Text = "Calibration";
            accservice = DependencyService.Resolve<IServiceACC>();
            accservice.Init();
            accservice.ValuesChanged += Calibrate;

            Device.StartTimer(TimeSpan.FromSeconds(5), () =>
            {
                accservice.ValuesChanged -= Calibrate;
                label_cali.Text = "Calibrated";
                return false; // return true to repeat counting, false to stop timer
            });
            button_start.IsEnabled = true;
        }

        #endregion buttons

        #region linacc
        private void SaveLin(object sender, EventArgs e)
        {
            AccEventArgs args = e as AccEventArgs;
            AccVector vec = new AccVector(args.x, args.y, args.z);
            linacctime[0] = linacctime[1];
            linacctime[1] = DateTime.UtcNow;
            linaccspan = linacctime[1] - linacctime[0];
            System.IO.File.AppendAllText(timepath, linaccspan.TotalMilliseconds.ToString() + System.Environment.NewLine);

            integration(vec);
            System.IO.File.AppendAllText(linpath, vec.ToString(linacctime[1]) + System.Environment.NewLine);
        }

        private void Calibrate(object sender, EventArgs e)
        {
            AccEventArgs args = e as AccEventArgs;

            calibration[1, 0] = 0.90 * calibration[0, 0] + 0.1 * args.x;
            calibration[1, 1] = 0.90 * calibration[0, 1] + 0.1 * args.y;
            calibration[1, 2] = 0.90 * calibration[0, 2] + 0.1 * args.z;

            calibration[0, 0] = calibration[1, 0];
            calibration[0, 1] = calibration[1, 1];
            calibration[0, 2] = calibration[1, 1];
        }

        #endregion linacc

        private void integration(AccVector vec)
        {
            acceleration[1, 0] = (0.5 * acceleration[0, 0]) + (0.5 * (vec.x - calibration[1, 0]));
            acceleration[1, 1] = (0.5 * acceleration[0, 1]) + (0.5 * (vec.y - calibration[1, 1]));
            acceleration[1, 2] = (0.5 * acceleration[0, 2]) + (0.5 * (vec.z - calibration[1, 2]));

            AccVector accvector = new AccVector((float)acceleration[1, 0], (float)acceleration[1, 1], (float)acceleration[1, 2]);
            System.IO.File.AppendAllText(lowaccpath, accvector.ToString(linacctime[1]) + System.Environment.NewLine);

            acceleration[0, 0] = acceleration[1, 0];
            acceleration[0, 1] = acceleration[1, 1]; 
            acceleration[0, 2] = acceleration[1, 2];


            /* roation transform */
            for (int i = 0; i < 3; i++)
            {
                float s = 0;
                for (int j = 0; j < 3; j++)
                {
                    s += (float)acceleration[1, j] * rotationMatrix[j + i * 3];
                }
                xyacceleration[1,i] = s;
            }
            AccVector korregiertervector = new AccVector((float)xyacceleration[1, 0], (float)xyacceleration[1, 1], (float)xyacceleration[1, 2]);
            System.IO.File.AppendAllText(endpath, korregiertervector.ToString(linacctime[1]) + System.Environment.NewLine);


            Matrix<double> F = Matrix<double>.Build.Dense(6, 6, new[] { 1d, 0d, 1d, 0d,  0.5d, 0d,   // State Transition Matrix: [ 1 0 T 0 .5T^2   0
                                                                        0d, 1d, 0d, 1d, 0d, 0.5d,   //                            0 1 0 T   0   .5T^2
                                                                        0d, 0d, 1d, 0d, 1d, 0d,   //                            0 0 1 0   T     0
                                                                        0d, 0d, 0d, 1d, 0d, 1d,   //                            0 0 0 1   0     T
                                                                        0d, 0d, 0d, 0d, 1d, 0d,                        //                            0 0 0 0   1     0
                                                                        0d, 0d, 0d, 0d, 0d, 1d});                      //                            0 0 0 0   0     1  ]
            //kalmann
            dkf.Predict(F);

            if (!(xyacceleration[1, 0] > 0.05 || xyacceleration[1, 0] < -0.05)) xyacceleration[1, 0] = 0;
            if (!(xyacceleration[1, 1] > 0.05 || xyacceleration[1, 1] < -0.05)) xyacceleration[1, 1] = 0;
            if (!(xyacceleration[1, 2] > 0.05 || xyacceleration[1, 2] < -0.05)) xyacceleration[1, 2] = 0;

            Matrix<double> z = Matrix<double>.Build.Dense(2, 1, new[] { xyacceleration[1,0], xyacceleration[1,1] }); // Measurement: [x'' y'']
            dkf.Update(z,H,R);
            System.IO.File.AppendAllText(dkfpath, linacctime[1].ToString("ss.fff") + ';' + dkf.State[0,0].ToString() + ';' + dkf.State[1,0] + System.Environment.NewLine);

            //end of movement
            if ((Math.Abs(xyacceleration[1, 0]) <= 0.01) && (Math.Abs(xyacceleration[1, 1]) <= 0.01)) endcount++; //vergleich auf 0
            if(endcount > 5)
            {
                endcount = 0;
                //die von davor 0 damit die danach auch null sind
                velocity[0, 0] = 0;
                velocity[0, 1] = 0;
            }

            //integrate
            velocity[1, 0] = velocity[0, 0] + (xyacceleration[0, 0] + (xyacceleration[1, 0] - xyacceleration[0, 0]) / 2) * linaccspan.TotalSeconds;
            velocity[1, 1] = velocity[0, 1] + (xyacceleration[0, 1] + (xyacceleration[1, 1] - xyacceleration[0, 1]) / 2) * linaccspan.TotalSeconds;
            //z-achse eigentlich latte
            velocity[1, 2] = velocity[0, 2] + (xyacceleration[0, 2] + (xyacceleration[1, 2] - xyacceleration[0, 2]) / 2) * linaccspan.TotalSeconds;

            AccVector velovector = new AccVector((float)velocity[1, 0], (float)velocity[1, 1], (float)velocity[1, 2]);
            System.IO.File.AppendAllText(velopath, velovector.ToString(linacctime[1]) + System.Environment.NewLine);

            //integrate
            position[1, 0] = position[0, 0] + (velocity[0, 0] + (velocity[1, 0] - velocity[0, 0]) / 2) * linaccspan.TotalSeconds;
            position[1, 1] = position[0, 1] + (velocity[0, 1] + (velocity[1, 1] - velocity[0, 1]) / 2) * linaccspan.TotalSeconds;
            position[1, 2] = position[0, 2] + (velocity[0, 2] + (velocity[1, 2] - velocity[0, 2]) / 2) * linaccspan.TotalSeconds;

            AccVector posvector = new AccVector((float)position[1, 0], (float)position[1, 1], (float)position[1, 2]);
            System.IO.File.AppendAllText(positionpath, posvector.ToString(linacctime[1]) + System.Environment.NewLine);

            xyacceleration[0, 0] = xyacceleration[1, 0];
            xyacceleration[0, 1] = xyacceleration[1, 1];
            xyacceleration[0, 2] = xyacceleration[1, 2];

            velocity[0, 0] = velocity[1, 0];
            velocity[0, 1] = velocity[1, 1];
            velocity[0, 2] = velocity[1, 2];

            position[0, 0] = position[1, 0];
            position[0, 1] = position[1, 1];
            position[0, 2] = position[1, 2];
        }

    }
}
