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
using CsvHelper;
using System.Globalization;
using MathNet.Numerics;
using System.Numerics;

namespace IMUTest
{
    public partial class MainPage : ContentPage
    {
        private IServiceACC accservice = null;

        private string linpath = null;
        private string positionpath = null;
        private string timepath = null;

        double[,] acceleration = new double[2, 3];
        double[,] xyacceleration = new double[2, 3];
        double[,] velocity = new double[2, 3];
        double[,] position = new double[2, 3];
        double[,] calibration = new double[2, 3];
        private int endcount;
        private int changed = 0;

        public static List<Records> listlinacc = new List<Records>();
        public static List<Double> listtimespan = new List<Double>();
        public static List<String> result = new List<String>();

        private const double DegreeToRadian = System.Math.PI / 180;
        public double? requiredRotationInDegrees = null;

        public MainPage()
        {
            InitializeComponent();

            var storagepath = DependencyService.Resolve<IFileSystem>().GetExternalStorage();
            linpath = System.IO.Path.Combine(storagepath, "lindata.csv");
            positionpath = System.IO.Path.Combine(storagepath, "positiondata.csv");
            timepath = System.IO.Path.Combine(storagepath, "timedata.csv");

            if (System.IO.File.Exists(positionpath)) System.IO.File.Delete(positionpath);

            using (var reader = new StreamReader(linpath))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                csv.Configuration.HasHeaderRecord = false;
                csv.Configuration.Delimiter = ";";
                listlinacc = csv.GetRecords<Records>().ToList();
            }

            using (var reader = new StreamReader(timepath))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                csv.Configuration.HasHeaderRecord = false;
                csv.Configuration.Delimiter = ";";
                listtimespan = csv.GetRecords<Double>().ToList();
            }
        }

        #region buttons
        private void Button_Clicked(object sender, EventArgs e)
        {
            result.Clear();
            Array.Clear(velocity, 0, velocity.Length);
            Array.Clear(position, 0, position.Length);
            Array.Clear(acceleration, 0, acceleration.Length);
            AccVector resvec = null;
            int j = 0;
            for (int i = 1; i < 100; i++)
            {
                foreach (var x in listlinacc)
                {
                    AccVector vec = new AccVector((float)x.X, (float)x.Y, (float)x.Z);
                    resvec = integration(i, vec, listtimespan[j++]);
                }
                j = 0;
                Array.Clear(velocity, 0, velocity.Length);
                Array.Clear(position, 0, position.Length);
                Array.Clear(acceleration, 0, acceleration.Length);
                result.Add(i.ToString() + ';' + resvec.ToString() + System.Environment.NewLine); 
            }

            label_onoff.Text = "ON";
        }



        private void Button_Clicked2(object sender, EventArgs e)
        {
            foreach (string x in result)
                System.IO.File.AppendAllText(positionpath, x);
            label_onoff.Text = "stored";
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

        private AccVector integration(int percentage, AccVector vec, double linaccspan)
        {
            acceleration[1, 0] = ((((double)(100 - percentage)) / 100) * acceleration[0, 0]) + (((double)percentage / 100) * (vec.x - calibration[1, 0]));
            acceleration[1, 1] = ((((double)(100 - percentage)) / 100) * acceleration[0, 1]) + (((double)percentage / 100) * (vec.y - calibration[1, 1]));
            acceleration[1, 2] = ((((double)(100 - percentage)) / 100) * acceleration[0, 2]) + (((double)percentage / 100) * (vec.z - calibration[1, 2]));

            acceleration[0, 0] = acceleration[1, 0];
            acceleration[0, 1] = acceleration[1, 1];
            acceleration[0, 2] = acceleration[1, 2];

            if (!(acceleration[1, 0] > 0.05 || acceleration[1, 0] < -0.05)) acceleration[1, 0] = 0;
            if (!(acceleration[1, 1] > 0.05 || acceleration[1, 1] < -0.05)) acceleration[1, 1] = 0;
            if (!(acceleration[1, 2] > 0.05 || acceleration[1, 2] < -0.05)) acceleration[1, 2] = 0;

            //end of movement
            if ((Math.Abs(acceleration[1, 0]) <= 0.01) && (Math.Abs(acceleration[1, 1]) <= 0.01))
            {
                if (changed == 0 && endcount != 0) //start endcount new if not in serial
                    endcount = 0;
                endcount++; 
                changed = 1;
                //endcount added 
            }
            else changed = 0; //no endcount added 

            if (endcount > 5)
            {
                endcount = 0;
                //die von davor 0 damit die danach auch null sind
                velocity[0, 0] = 0;
                velocity[0, 1] = 0;
                velocity[0, 2] = 0;
            }

            //rotate X,Y acceleration from world frame to map frame if required
            if (requiredRotationInDegrees != null)
            {
                Vector2 mapframe = rotateAccelerationVectors((double)requiredRotationInDegrees, new Vector2((float)acceleration[1, 0], (float)acceleration[1, 1]));
                acceleration[1, 0] = mapframe.X;
                acceleration[1, 1] = mapframe.Y;
            }
            //integrate
            velocity[1, 0] = velocity[0, 0] + (acceleration[0, 0] + (acceleration[1, 0] - acceleration[0, 0]) / 2) * linaccspan;
            velocity[1, 1] = velocity[0, 1] + (acceleration[0, 1] + (acceleration[1, 1] - acceleration[0, 1]) / 2) * linaccspan;
            velocity[1, 2] = velocity[0, 2] + (acceleration[0, 2] + (acceleration[1, 2] - acceleration[0, 2]) / 2) * linaccspan;

            //integrate
            position[1, 0] = position[0, 0] + (velocity[0, 0] + (velocity[1, 0] - velocity[0, 0]) / 2) * linaccspan;
            position[1, 1] = position[0, 1] + (velocity[0, 1] + (velocity[1, 1] - velocity[0, 1]) / 2) * linaccspan;
            position[1, 2] = position[0, 2] + (velocity[0, 2] + (velocity[1, 2] - velocity[0, 2]) / 2) * linaccspan;

            velocity[0, 0] = velocity[1, 0];
            velocity[0, 1] = velocity[1, 1];
            velocity[0, 2] = velocity[1, 2];

            position[0, 0] = position[1, 0];
            position[0, 1] = position[1, 1];
            position[0, 2] = position[1, 2];

            return new AccVector((float)position[1, 0], (float)position[1,1],(float)position[1,2]);
        }

        private Vector2 rotateAccelerationVectors(double degrees, Vector2 vec)
        {
            var ca = System.Math.Cos(degrees * DegreeToRadian);
            var sa = System.Math.Sin(degrees * DegreeToRadian);
            return new Vector2((float)(ca * vec.X - sa * vec.Y), (float)(sa * vec.X + ca * vec.Y));
        }

        public class Records
        {
            public double time { get; set; }
            public double X { get; set; }
            public double Y { get; set; }
            public double Z { get; set; }


            public Records(double partime, double parx, double pary, double parz)
            {
                time = partime;
                X = parx;
                Y = pary;
                Z = parz;

            }
        }
    }
}
