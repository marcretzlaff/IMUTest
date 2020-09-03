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

namespace IMUTest
{
    public partial class MainPage : ContentPage
    {
        private IServiceACC accservice = null;
        private IServiceOrientation orientationservice = null;

        private DateTime acctime;
        private DateTime[] linacctime =  new DateTime[2];
        private TimeSpan linaccspan;

        private string linpath = null;
        private string accpath = null;
        private string lowaccpath = null;
        private string velopath = null;
        private string positionpath = null;
        private string calipath = null;
        private string orientationpath = null;
        private string endpath = null;
        private string timepath = null;

        double[,] acceleration = new double[2, 3];
        double[,] xyacceleration = new double[2, 2];
        double[,] velocity = new double[2,3];
        double[,] position = new double[2,3];
        double[,]  calibration = new double[2,3];
        double[] rollcalibration = new double[2] { 0, 0 };
        double[] pitchcalibration = new double[2] { 0, 0 };
        double[] roll = new double[2] { 0, 0};
        double[] pitch = new double[2] { 0, 0};
        private int endcount;

        public MainPage()
        {
            InitializeComponent();
            //Sensor Speed UI: 65ms Default 65ms Game 20ms Fastest 5ms
            Accelerometer.Start(SensorSpeed.Game);
            Accelerometer.ReadingChanged += acc_read;

            var storagepath = DependencyService.Resolve<IFileSystem>().GetExternalStorage();
            linpath = System.IO.Path.Combine(storagepath, "lindata.csv");
            accpath = System.IO.Path.Combine(storagepath, "accdata.csv");
            lowaccpath = System.IO.Path.Combine(storagepath, "lowaccdata.csv");
            velopath = System.IO.Path.Combine(storagepath, "velodata.csv");
            positionpath = System.IO.Path.Combine(storagepath, "positiondata.csv");
            orientationpath = System.IO.Path.Combine(storagepath, "orientationdata.csv");
            endpath = System.IO.Path.Combine(storagepath, "enddata.csv");
            timepath = System.IO.Path.Combine(storagepath, "timedata.csv");

            if (System.IO.File.Exists(linpath)) System.IO.File.Delete(linpath);
            if (System.IO.File.Exists(accpath)) System.IO.File.Delete(accpath);
            if (System.IO.File.Exists(lowaccpath)) System.IO.File.Delete(lowaccpath);
            if (System.IO.File.Exists(velopath)) System.IO.File.Delete(velopath);
            if (System.IO.File.Exists(positionpath)) System.IO.File.Delete(positionpath);
            if (System.IO.File.Exists(orientationpath)) System.IO.File.Delete(orientationpath);
            if (System.IO.File.Exists(endpath)) System.IO.File.Delete(endpath);
            if (System.IO.File.Exists(timepath)) System.IO.File.Delete(timepath); 

        }

        private void acc_read(object sender, AccelerometerChangedEventArgs e)
        {
            acctime = DateTime.UtcNow;
            label_x.Text = "X:" + e.Reading.Acceleration.X.ToString();
            label_y.Text = "Y:" + e.Reading.Acceleration.Y.ToString();
            label_z.Text = "Z:" + e.Reading.Acceleration.Z.ToString();
            label_Zeit.Text = acctime.ToString();
        }

        private void roll_pitch_calibration(object sender, EventArgs e)
        {
            rollcalibration[1] = 0.9 * rollcalibration[0] + 0.1 * (e as OrientationEventArgs).roll;
            pitchcalibration[1] = 0.9 * pitchcalibration[0] + 0.1 * (e as OrientationEventArgs).pitch;
            rollcalibration[0] = rollcalibration[1];
            pitchcalibration[0] = pitchcalibration[1];
        }

        #region buttons
        private void Button_Clicked(object sender, EventArgs e)
        {
            accservice = DependencyService.Resolve<IServiceACC>();
            orientationservice = DependencyService.Resolve<IServiceOrientation>();
            accservice.Init();
            orientationservice.Init();

            Array.Clear(velocity, 0, velocity.Length);
            Array.Clear(position, 0, position.Length);
            Array.Clear(acceleration, 0, acceleration.Length);
            Array.Clear(calibration, 0, calibration.Length);
            //start with calibration offset roll & pitch
            roll[0] = rollcalibration[1];
            pitch[0] = pitchcalibration[1];
            linacctime[1] = DateTime.UtcNow;
            label_onoff.Text = "ON";

            accservice.ValuesChanged += SaveLin;
            orientationservice.ValuesChanged += save_orientation;
            Accelerometer.ReadingChanged += SaveAcc;
        }

        private void save_orientation(object sender, EventArgs e)
        {
            OrientationEventArgs args = e as OrientationEventArgs;
            OrientationVector vec = new OrientationVector(args.azimoth, args.roll, args.pitch);
            label_pitch.Text = "Pitch:" + args.pitch.ToString();
            label_roll.Text = "Roll:" + args.roll.ToString();
            label_yaw.Text = "Yaw:" + args.azimoth.ToString();
            roll[1] = 0.5 * roll[0] + 0.5 * (args.roll - rollcalibration[1]);
            pitch[1] = 0.5 * pitch[0] + 0.5 * (args.pitch - pitchcalibration[1]);
            roll[0] = roll[1];
            pitch[0] = pitch[1];
            System.IO.File.AppendAllText(orientationpath, vec.ToString(DateTime.UtcNow) + System.Environment.NewLine);
        }


        private void Button_Clicked2(object sender, EventArgs e)
        {
            accservice = DependencyService.Resolve<IServiceACC>();
            accservice.Stop();
            label_onoff.Text = "OFF";
            Accelerometer.ReadingChanged -= SaveAcc;
            orientationservice.ValuesChanged -= save_orientation;
        }

        private void Button_Clicked3(object sender, EventArgs e)
        {
            button_start.IsEnabled = false;
            label_cali.Text = "Calibration";
            accservice = DependencyService.Resolve<IServiceACC>();
            accservice.Init();
            orientationservice = DependencyService.Resolve<IServiceOrientation>();
            orientationservice.Init();
            accservice.ValuesChanged += Calibrate;
            orientationservice.ValuesChanged += roll_pitch_calibration;

            Device.StartTimer(TimeSpan.FromSeconds(5), () =>
            {
                accservice.ValuesChanged -= Calibrate;
                orientationservice.ValuesChanged -= roll_pitch_calibration;
                label_cali.Text = "Calibrated";
                return false; // return true to repeat counting, false to stop timer
            });
            button_start.IsEnabled = true;
        }

        #endregion buttons

        private void SaveAcc(object sender, AccelerometerChangedEventArgs e)
        {
            AccVector vec = new AccVector(e.Reading.Acceleration.X, e.Reading.Acceleration.Y, e.Reading.Acceleration.Z);
            System.IO.File.AppendAllText(accpath, vec.ToString(acctime) + System.Environment.NewLine);
        }

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
            acceleration[1, 0] = (0.5 * acceleration[0, 0]) + (0.5 * (vec.x - calibration[1,0]));
            acceleration[1, 1] = (0.5 * acceleration[0, 1]) + (0.5 * (vec.y - calibration[1,1]));
            acceleration[1, 2] = (0.5 * acceleration[0, 2]) + (0.5 * (vec.z - calibration[1,2]));

            AccVector accvector = new AccVector((float)acceleration[1, 0], (float)acceleration[1, 1], (float)acceleration[1, 2]);
            System.IO.File.AppendAllText(lowaccpath, accvector.ToString(linacctime[1]) + System.Environment.NewLine);

            acceleration[0, 0] = acceleration[1, 0];
            acceleration[0, 1] = acceleration[1, 1];


            //vektortransformation
            //y | x <->
            //Beschleunigung in y-Achse
            xyacceleration[1, 1] = Math.Cos(Math.PI * roll[1] / 180) * acceleration[1, 1] + Math.Sin(Math.PI * roll[1] / 180) * acceleration[1, 2];
            //Beschleunigung in x-Achse
            xyacceleration[1, 0] = Math.Cos(Math.PI * pitch[1] / 180) * acceleration[1, 0] + Math.Sin(Math.PI * pitch[1] / 180) * acceleration[1, 2];

            //lowpass korriegierter
            xyacceleration[1, 0] = 0.50 * xyacceleration[0, 0] + 0.50 * xyacceleration[1, 0];
            xyacceleration[1, 1] = 0.50 * xyacceleration[0, 1] + 0.50 * xyacceleration[1, 1];

            AccVector korregiertervector = new AccVector((float)xyacceleration[1, 0], (float)xyacceleration[1, 1], (float)acceleration[1, 2]);
            System.IO.File.AppendAllText(endpath, korregiertervector.ToString(linacctime[1])+ System.Environment.NewLine);

            if (!(xyacceleration[1, 0] > 0.05 || xyacceleration[1, 0] < -0.05)) xyacceleration[1, 0] = 0;
            if (!(xyacceleration[1, 1] > 0.05 || xyacceleration[1, 1] < -0.05)) xyacceleration[1, 1] = 0;
            if (!(acceleration[1, 2] > 0.05 || acceleration[1, 2] < -0.05)) acceleration[1, 2] = 0;

            //end of movement
            if ((Math.Abs(xyacceleration[1, 0]) <= 0.01) && (Math.Abs(xyacceleration[1, 1]) <= 0.01)) endcount++; //vergleich auf 0
            if(endcount > 10)
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
            velocity[1, 2] = velocity[0, 2] + (acceleration[0, 2] + (acceleration[1, 2] - acceleration[0, 2]) / 2) * linaccspan.TotalSeconds;

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
            acceleration[0, 2] = acceleration[1, 2];

            velocity[0, 0] = velocity[1, 0];
            velocity[0, 1] = velocity[1, 1];
            velocity[0, 2] = velocity[1, 2];

            position[0, 0] = position[1, 0];
            position[0, 1] = position[1, 1];
            position[0, 2] = position[1, 2];
        }

    }
}
