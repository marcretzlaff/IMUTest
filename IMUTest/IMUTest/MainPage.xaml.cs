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

        private DateTime acctime;
        private DateTime linacctime;

        private string linpath = null;
        private string accpath = null;
        private string lowaccpath = null;
        private string velopath = null;
        private string positionpath = null;
        private string calipath = null;
        private string rollpath = null;
        private string pitchpath = null;
        private string endpath = null;

        double[,] acceleration = new double[2, 3];
        double[,] velocity = new double[2,3];
        double[,] position = new double[2,3];
        double[,]  calibration = new double[2,3];
        double[] rollcalibration = new double[2] { 0, 0 };
        double[] pitchcalibration = new double[2] { 0, 0 };
        double[] roll = new double[2] { 0, 0};
        double[] pitch = new double[2] { 0, 0};

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
            calipath = System.IO.Path.Combine(storagepath, "calidata.csv");
            rollpath = System.IO.Path.Combine(storagepath, "rolldata.csv");
            pitchpath = System.IO.Path.Combine(storagepath, "pitchdata.csv");
            endpath = System.IO.Path.Combine(storagepath, "enddata.csv");

            if (System.IO.File.Exists(linpath)) System.IO.File.Delete(linpath);
            if (System.IO.File.Exists(accpath)) System.IO.File.Delete(accpath);
            if (System.IO.File.Exists(lowaccpath)) System.IO.File.Delete(lowaccpath);
            if (System.IO.File.Exists(velopath)) System.IO.File.Delete(velopath);
            if (System.IO.File.Exists(positionpath)) System.IO.File.Delete(positionpath);
            if (System.IO.File.Exists(calipath)) System.IO.File.Delete(calipath);
            if (System.IO.File.Exists(rollpath)) System.IO.File.Delete(rollpath);
            if (System.IO.File.Exists(pitchpath)) System.IO.File.Delete(pitchpath);
            if (System.IO.File.Exists(endpath)) System.IO.File.Delete(endpath);

        }

        private void linacc_read(object sender, EventArgs e)
        {
            AccEventArgs args = e as AccEventArgs;
            linacctime = DateTime.UtcNow;
            label_linx.Text = "X:" + args.x.ToString();
            label_liny.Text = "Y:" + args.y.ToString();
            label_linz.Text = "Z:" + args.z.ToString();
        }

        private void acc_read(object sender, AccelerometerChangedEventArgs e)
        {
            acctime = DateTime.UtcNow;
            label_x.Text = "X:" + e.Reading.Acceleration.X.ToString();
            label_y.Text = "Y:" + e.Reading.Acceleration.Y.ToString();
            label_z.Text = "Z:" + e.Reading.Acceleration.Z.ToString();
            label_Zeit.Text = acctime.ToString();
        }

        private void roll_pitch_calibration(object sender, AccelerometerChangedEventArgs e)
        {
            rollcalibration[1] = 0.9 * rollcalibration[0] + 0.1 * (Math.Atan(e.Reading.Acceleration.Y / (Math.Sqrt(Math.Pow(e.Reading.Acceleration.X, 2) + Math.Pow(e.Reading.Acceleration.Z, 2)))) * 180 / Math.PI);
            label_rollcali.Text = "Roll:" + rollcalibration[1].ToString();
            pitchcalibration[1] = 0.9 * pitchcalibration[0] + 0.1 * (Math.Atan(-1 * e.Reading.Acceleration.X / (Math.Sqrt(Math.Pow(e.Reading.Acceleration.Y, 2) + Math.Pow(e.Reading.Acceleration.Z, 2)))) * 180 / Math.PI);
            label_pitchcali.Text = "Pitch:" + pitchcalibration[1].ToString();
            rollcalibration[0] = rollcalibration[1];
            pitchcalibration[0] = pitchcalibration[1];
        }

        private void roll_pitch(object sender, AccelerometerChangedEventArgs e)
        {
            roll[1] = (0.9 * roll[0] + 0.1 * (Math.Atan(e.Reading.Acceleration.Y / (Math.Sqrt(Math.Pow(e.Reading.Acceleration.X, 2) + Math.Pow(e.Reading.Acceleration.Z, 2)))) * 180 / Math.PI));
            roll[0] = roll[1];
            roll[1] -= rollcalibration[1];
            label_roll.Text = "Roll:" + roll[1].ToString();
            System.IO.File.AppendAllText(rollpath, acctime.ToString("ss.fff") + ';' + roll[1].ToString() + System.Environment.NewLine);
            pitch[1] = (0.9 * pitch[0] + 0.1 * (Math.Atan(-1 * e.Reading.Acceleration.X / (Math.Sqrt(Math.Pow(e.Reading.Acceleration.Y, 2) + Math.Pow(e.Reading.Acceleration.Z, 2)))) * 180 / Math.PI));
            pitch[0] = pitch[1];
            pitch[1] -= pitchcalibration[1];
            label_pitch.Text = "Pitch:" + pitch[1].ToString();
            System.IO.File.AppendAllText(pitchpath, acctime.ToString("ss.fff") + ';' + pitch[1].ToString() + System.Environment.NewLine);
        }

        private void Button_Clicked(object sender, EventArgs e)
        {
            accservice = DependencyService.Resolve<IServiceACC>();
            accservice.Init();

            Array.Clear(velocity, 0, velocity.Length);
            Array.Clear(position, 0, position.Length);
            Array.Clear(acceleration, 0, acceleration.Length);
            Array.Clear(calibration, 0, calibration.Length);

            accservice.ValuesChanged += linacc_read;
            accservice.ValuesChanged += SaveLin;
            Accelerometer.ReadingChanged += SaveAcc;
            Accelerometer.ReadingChanged += roll_pitch;
        }

        private void Button_Clicked2(object sender, EventArgs e)
        {
            accservice = DependencyService.Resolve<IServiceACC>();
            accservice.Stop();

            Accelerometer.ReadingChanged -= SaveAcc;
            Accelerometer.ReadingChanged -= roll_pitch;
        }

        private void Button_Clicked3(object sender, EventArgs e)
        {
            button_start.IsEnabled = false;
            accservice = DependencyService.Resolve<IServiceACC>();
            accservice.Init();
            accservice.ValuesChanged += linacc_read;
            accservice.ValuesChanged += Calibrate;
            Accelerometer.ReadingChanged += roll_pitch_calibration;

            Device.StartTimer(TimeSpan.FromSeconds(5), () =>
            {
                accservice.ValuesChanged -= linacc_read;
                accservice.ValuesChanged -= Calibrate;
                Accelerometer.ReadingChanged -= roll_pitch_calibration;
                return false; // return true to repeat counting, false to stop timer
            });
            button_start.IsEnabled = true;
        }

        private void SaveAcc(object sender, AccelerometerChangedEventArgs e)
        {
            AccVector vec = new AccVector(e.Reading.Acceleration.X, e.Reading.Acceleration.Y, e.Reading.Acceleration.Z);
            System.IO.File.AppendAllText(accpath, vec.ToString(acctime) + System.Environment.NewLine);
        }

        private void SaveLin(object sender, EventArgs e)
        {
            AccEventArgs args = e as AccEventArgs;
            AccVector vec = new AccVector(args.x, args.y, args.z);
            integration(vec);
            System.IO.File.AppendAllText(linpath, vec.ToString(linacctime) + System.Environment.NewLine);
        }

        private void Calibrate(object sender, EventArgs e)
        {
            AccEventArgs args = e as AccEventArgs;

            calibration[1, 0] = 0.97 * calibration[0, 0] + 0.3 * args.x;
            calibration[1, 1] = 0.97 * calibration[0, 1] + 0.3 * args.y;
            calibration[1, 2] = 0.97 * calibration[0, 2] + 0.3 * args.z;

            calibration[0, 0] = calibration[1, 0];
            calibration[0, 1] = calibration[1, 1];
            calibration[0, 2] = calibration[1, 1];

            AccVector accvector = new AccVector((float)calibration[1,0], (float)calibration[1,1], (float)calibration[1,2]);
            System.IO.File.AppendAllText(calipath, accvector.ToString(linacctime) + System.Environment.NewLine);
        }

        private void integration(AccVector vec)
        {
            acceleration[1, 0] = 0.97 * acceleration[0, 0] + 0.03 * vec.x - calibration[1,0];
            acceleration[1, 1] = 0.97 * acceleration[0, 1] + 0.03 * vec.y - calibration[1,1];
            acceleration[1, 2] = 0.97 * acceleration[0, 2] + 0.03 * vec.z - calibration[1,2];

            AccVector accvector = new AccVector((float)acceleration[1, 0], (float)acceleration[1, 1], (float)acceleration[1, 2]);
            System.IO.File.AppendAllText(lowaccpath, accvector.ToString(linacctime) + System.Environment.NewLine);

            //vektortransformation

            //Beschleunigung in x-Achse : Beschleunigung in x Achse aus Z-Sensor + Beschleunigung in x-Achse aus X-Sensor
            acceleration[1, 0] = Math.Sin(roll[1]) * acceleration[1, 2] + Math.Cos(roll[1]) * acceleration[1, 0];
            //Beschleunigung in y-Achse : Beschleunigung in y Achse aus Z-Sensor + Beschleunigung in y-Achse aus Y-Sensor
            acceleration[1, 1] = Math.Sin(pitch[1]) * acceleration[1, 2] + Math.Cos(pitch[1]) * acceleration[1, 1];

            AccVector korregiertervector = new AccVector((float)acceleration[1, 0], (float)acceleration[1, 1], (float)acceleration[1, 2]);
            System.IO.File.AppendAllText(endpath, korregiertervector.ToString(linacctime) + System.Environment.NewLine);

            if (!(acceleration[1, 0] > 0.1 || acceleration[1, 0] < -0.1)) acceleration[1, 0] = 0;
            if (!(acceleration[1, 1] > 0.1 || acceleration[1, 1] < -0.1)) acceleration[1, 1] = 0;
            if (!(acceleration[1, 2] > 0.1 || acceleration[1, 2] < -0.1)) acceleration[1, 2] = 0;

            //integrate
            velocity[1, 0] = velocity[0, 0] + acceleration[0, 0] + ((acceleration[1, 0] - acceleration[0, 0]) / 2);
            velocity[1, 1] = velocity[0, 1] + acceleration[0, 1] + ((acceleration[1, 1] - acceleration[0, 1]) / 2);
            velocity[1, 2] = velocity[0, 2] + acceleration[0, 2] + ((acceleration[1, 2] - acceleration[0, 2]) / 2);



            AccVector velovector = new AccVector((float)velocity[1, 0], (float)velocity[1, 1], (float)velocity[1, 2]);
            System.IO.File.AppendAllText(velopath, velovector.ToString(linacctime) + System.Environment.NewLine);

            //integrate
            position[1, 0] = position[0, 0] + velocity[0, 0] + ((velocity[1, 0] - velocity[0, 0]) / 2);
            position[1, 1] = position[0, 1] + velocity[0, 1] + ((velocity[1, 1] - velocity[0, 1]) / 2);
            position[1, 2] = position[0, 2] + velocity[0, 2] + ((velocity[1, 2] - velocity[0, 2]) / 2);

            AccVector posvector = new AccVector((float)position[1, 0], (float)position[1, 1], (float)position[1, 2]);
            System.IO.File.AppendAllText(positionpath, posvector.ToString(linacctime) + System.Environment.NewLine);

            acceleration[0, 0] = acceleration[1, 0];
            acceleration[0, 1] = acceleration[1, 1];
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
