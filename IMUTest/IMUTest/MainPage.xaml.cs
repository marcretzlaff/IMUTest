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
using System.Numerics;

namespace IMUTest
{
    public partial class MainPage : ContentPage
    {
        private IServiceACC accservice = null;
        private DateTime acctime;
        private DateTime[] linacctime = new DateTime[2];
        private TimeSpan linaccspan;
        private int changed = 0;
        private const double DegreeToRadian = System.Math.PI / 180;
        public double? requiredRotationInDegrees = null;

        private string linpath = null;
        private string positionpath = null;
        private string endpath = null;
        private string timepath = null;

        double[,] acceleration = new double[2, 3];
        double[,] velocity = new double[2, 3];
        double[,] position = new double[2, 3];
        double[,] calibration = new double[2, 3];
        private int endcount;

        public MainPage()
        {
            InitializeComponent();
            //Sensor Speed UI: 65ms Default 65ms Game 20ms Fastest 5ms
            Accelerometer.Start(SensorSpeed.Game);
            Accelerometer.ReadingChanged += acc_read;

            var storagepath = DependencyService.Resolve<IFileSystem>().GetExternalStorage();
            linpath = System.IO.Path.Combine(storagepath, "lindata.csv");
            positionpath = System.IO.Path.Combine(storagepath, "positiondata.csv");
            timepath = System.IO.Path.Combine(storagepath, "timedata.csv");
            endpath = System.IO.Path.Combine(storagepath, "enddata.csv");

            if (System.IO.File.Exists(linpath)) System.IO.File.Delete(linpath);
            if (System.IO.File.Exists(positionpath)) System.IO.File.Delete(positionpath);
            if (System.IO.File.Exists(timepath)) System.IO.File.Delete(timepath);
            if (System.IO.File.Exists(endpath)) System.IO.File.Delete(endpath);
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
            accservice.Init();

            Array.Clear(velocity, 0, velocity.Length);
            Array.Clear(position, 0, position.Length);
            Array.Clear(acceleration, 0, acceleration.Length);
            linacctime[1] = DateTime.UtcNow;
            label_onoff.Text = "ON";
            accservice.ValuesChanged += SaveLin;
        }

        private void Button_Clicked2(object sender, EventArgs e)
        {
            accservice.Stop();
            label_onoff.Text = "OFF";
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
            System.IO.File.AppendAllText(timepath, linaccspan.TotalSeconds.ToString().Replace(',','.') + System.Environment.NewLine);

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
                var res = rotateAccelerationVectors((double)requiredRotationInDegrees, new Vector2((float)acceleration[1, 0], (float)acceleration[1, 1]));
                acceleration[1, 0] = res.X;
                acceleration[1, 1] = res.Y;
            }
            AccVector korregiertervector = new AccVector((float)acceleration[1, 0], (float)acceleration[1, 1], (float)acceleration[1, 2]);
            System.IO.File.AppendAllText(endpath, linaccspan.TotalSeconds.ToString().Replace(',', '.') + ';' + korregiertervector.ToString(linacctime[1]) + System.Environment.NewLine);

            //integrate
            velocity[1, 0] = velocity[0, 0] + (acceleration[0, 0] + (acceleration[1, 0] - acceleration[0, 0]) / 2) * linaccspan.TotalSeconds;
            velocity[1, 1] = velocity[0, 1] + (acceleration[0, 1] + (acceleration[1, 1] - acceleration[0, 1]) / 2) * linaccspan.TotalSeconds;
            velocity[1, 2] = velocity[0, 2] + (acceleration[0, 2] + (acceleration[1, 2] - acceleration[0, 2]) / 2) * linaccspan.TotalSeconds;

            //integrate
            position[1, 0] = position[0, 0] + (velocity[0, 0] + (velocity[1, 0] - velocity[0, 0]) / 2) * linaccspan.TotalSeconds;
            position[1, 1] = position[0, 1] + (velocity[0, 1] + (velocity[1, 1] - velocity[0, 1]) / 2) * linaccspan.TotalSeconds;
            position[1, 2] = position[0, 2] + (velocity[0, 2] + (velocity[1, 2] - velocity[0, 2]) / 2) * linaccspan.TotalSeconds;

            AccVector posvector = new AccVector((float)position[1, 0], (float)position[1, 1], (float)position[1, 2]);
            System.IO.File.AppendAllText(positionpath, posvector.ToString(linacctime[1]) + System.Environment.NewLine);

            velocity[0, 0] = velocity[1, 0];
            velocity[0, 1] = velocity[1, 1];
            velocity[0, 2] = velocity[1, 2];

            position[0, 0] = position[1, 0];
            position[0, 1] = position[1, 1];
            position[0, 2] = position[1, 2];
        }

        private Vector2 rotateAccelerationVectors(double degrees, Vector2 vec)
        {
            var ca = System.Math.Cos(degrees * DegreeToRadian);
            var sa = System.Math.Sin(degrees * DegreeToRadian);
            return new Vector2((float)(ca * vec.X - sa * vec.Y), (float)(sa * vec.X + ca * vec.Y));
        }

    }
}
