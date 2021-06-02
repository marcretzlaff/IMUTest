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
        private IServiceRotation rotationservice = null;
        private DateTime acctime;
        private DateTime[] linacctime = new DateTime[2];
        private TimeSpan linaccspan;
        private float[] rotationMatrix = new float[9];
        private int changed = 0;
        private const double DegreeToRadian = System.Math.PI / 180;
        public double? requiredRotationInDegrees = null;

        private string linpath = null;
        private string positionpath = null;
        private string endpath = null;
        private string timepath = null;
        private string endpospath = null; 

        double[,] acceleration = new double[2, 3];
        double[,] velocity = new double[2, 3];
        double[,] xyacceleration = new double[2, 3];
        double[,] position = new double[2, 3];
        double[,] xyposition = new double[2, 3];
        double[,] calibration = new double[2, 3];
        double[,] xyvelocity = new double[2, 3];
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
            endpospath = System.IO.Path.Combine(storagepath, "endposdata.csv");

            if (System.IO.File.Exists(linpath)) System.IO.File.Delete(linpath);
            if (System.IO.File.Exists(positionpath)) System.IO.File.Delete(positionpath);
            if (System.IO.File.Exists(timepath)) System.IO.File.Delete(timepath);
            if (System.IO.File.Exists(endpath)) System.IO.File.Delete(endpath);
            if (System.IO.File.Exists(endpospath)) System.IO.File.Delete(endpospath);
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
            rotationservice = DependencyService.Resolve<IServiceRotation>();
            rotationservice.Init();
            Array.Clear(velocity, 0, velocity.Length);
            Array.Clear(position, 0, position.Length);
            Array.Clear(acceleration, 0, acceleration.Length);
            linacctime[1] = DateTime.UtcNow;
            label_onoff.Text = "ON";
            rotationservice.ValuesChanged += save_rotation;
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
                label_cal.Text = "X: " + calibration[1, 0].ToString() + " Y: " + calibration[1, 1].ToString();
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
            linacctime[1] = args.stamp;
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
        private void save_rotation(object sender, EventArgs e)
        {
            RotationEventArgs args = e as RotationEventArgs;
            SensorManager.GetRotationMatrixFromVector(rotationMatrix, args.values);
        }

        private void integration(AccVector vec)
        {
            acceleration[1, 0] = (0.3 * acceleration[0, 0]) + (0.7 * (vec.x - calibration[1, 0]));
            acceleration[1, 1] = (0.3 * acceleration[0, 1]) + (0.7 * (vec.y - calibration[1, 1]));
            acceleration[1, 2] = (0.3 * acceleration[0, 1]) + (0.7 * (vec.y - calibration[1, 2]));

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
                xyvelocity[0, 0] = 0;
                xyvelocity[0, 1] = 0;
            }

            /* roation transform */
            for (int i = 0; i < 3; i++)
            {
                double s = 0;
                for (int j = 0; j < 3; j++)
                {
                    s += acceleration[1, j] * rotationMatrix[j + i * 3];
                }
                xyacceleration[1, i] = s;
            }
            AccVector korregiertervector = new AccVector((float)xyacceleration[1, 0], (float)xyacceleration[1, 1], (float)xyacceleration[1, 2]);
            System.IO.File.AppendAllText(endpath,korregiertervector.ToString(linacctime[1]) + System.Environment.NewLine);

            //integrate
            velocity[1, 0] = velocity[0, 0] + (acceleration[0, 0] + (acceleration[1, 0] - acceleration[0, 0]) / 2) * linaccspan.TotalSeconds;
            velocity[1, 1] = velocity[0, 1] + (acceleration[0, 1] + (acceleration[1, 1] - acceleration[0, 1]) / 2) * linaccspan.TotalSeconds;

            //integrate
            position[1, 0] = position[0, 0] + (velocity[0, 0] + (velocity[1, 0] - velocity[0, 0]) / 2) * linaccspan.TotalSeconds;
            position[1, 1] = position[0, 1] + (velocity[0, 1] + (velocity[1, 1] - velocity[0, 1]) / 2) * linaccspan.TotalSeconds;

            //integrate
            xyvelocity[1, 0] = xyvelocity[0, 0] + (xyacceleration[0, 0] + (xyacceleration[1, 0] - xyacceleration[0, 0]) / 2) * linaccspan.TotalSeconds;
            xyvelocity[1, 1] = xyvelocity[0, 1] + (xyacceleration[0, 1] + (xyacceleration[1, 1] - xyacceleration[0, 1]) / 2) * linaccspan.TotalSeconds;

            //integrate
            xyposition[1, 0] = xyposition[0, 0] + (xyvelocity[0, 0] + (xyvelocity[1, 0] - xyvelocity[0, 0]) / 2) * linaccspan.TotalSeconds;
            xyposition[1, 1] = xyposition[0, 1] + (xyvelocity[0, 1] + (xyvelocity[1, 1] - xyvelocity[0, 1]) / 2) * linaccspan.TotalSeconds;

            AccVector posvector = new AccVector((float)position[1, 0], (float)position[1, 1], (float)position[1, 2]);
            System.IO.File.AppendAllText(positionpath, posvector.ToString(linacctime[1]) + System.Environment.NewLine);
            posvector = new AccVector((float)xyposition[1, 0], (float)xyposition[1, 1], (float)xyposition[1, 2]);
            System.IO.File.AppendAllText(endpospath, posvector.ToString(linacctime[1]) + System.Environment.NewLine);

            velocity[0, 0] = velocity[1, 0];
            velocity[0, 1] = velocity[1, 1];

            position[0, 0] = position[1, 0];
            position[0, 1] = position[1, 1];

            xyvelocity[0, 0] = xyvelocity[1, 0];
            xyvelocity[0, 1] = xyvelocity[1, 1];

            xyposition[0, 0] = xyposition[1, 0];
            xyposition[0, 1] = xyposition[1, 1];

            label_x.Text = "X: " + position[1, 0].ToString();
            label_y.Text = "Y: " + position[1, 1].ToString();
        }

        private Vector2 rotateAccelerationVectors(double degrees, Vector2 vec)
        {
            var ca = System.Math.Cos(degrees * DegreeToRadian);
            var sa = System.Math.Sin(degrees * DegreeToRadian);
            return new Vector2((float)(ca * vec.X - sa * vec.Y), (float)(sa * vec.X + ca * vec.Y));
        }

    }
}
