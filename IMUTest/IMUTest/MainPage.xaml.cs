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

        private string linpath = null;
        private string accpath = null;
        private string ringpath = null;
        private string velopath = null;
        private string positionpath = null;
        private string calipath = null;


        double[,] acceleration = new double[2, 3];
        double[,] velocity = new double[2,3];
        double[,] position = new double[2,3];
        double[]  calibration = new double[3] { 0, 0, 0 };

        private AccVector[] buffer = new AccVector[40];
        private int index = 0;
        public MainPage()
        {
            InitializeComponent();

            for (int i = 0; i < 40; i++)
            {
                buffer[i] = new AccVector();
            }

            Accelerometer.Start(SensorSpeed.UI);
            Accelerometer.ReadingChanged += acc_read;

            var storagepath = DependencyService.Resolve<IFileSystem>().GetExternalStorage();
            linpath = System.IO.Path.Combine(storagepath, "lindata.csv");
            accpath = System.IO.Path.Combine(storagepath, "accdata.csv");
            ringpath = System.IO.Path.Combine(storagepath, "ringdata.csv");
            velopath = System.IO.Path.Combine(storagepath, "velodata.csv");
            positionpath = System.IO.Path.Combine(storagepath, "positiondata.csv");
            calipath = System.IO.Path.Combine(storagepath, "calidata.csv");

            if (System.IO.File.Exists(linpath)) System.IO.File.Delete(linpath);
            if (System.IO.File.Exists(accpath)) System.IO.File.Delete(accpath);
            if (System.IO.File.Exists(ringpath)) System.IO.File.Delete(ringpath);
            if (System.IO.File.Exists(velopath)) System.IO.File.Delete(velopath);
            if (System.IO.File.Exists(positionpath)) System.IO.File.Delete(positionpath);
            if (System.IO.File.Exists(calipath)) System.IO.File.Delete(calipath);
        }

        private void linacc_read(object sender, EventArgs e)
        {
            AccEventArgs args = e as AccEventArgs;
            label_linx.Text = "X:" + args.x.ToString();
            label_liny.Text = "Y:" + args.y.ToString();
            label_linz.Text = "Z:" + args.z.ToString();
        }

        private void acc_read(object sender, AccelerometerChangedEventArgs e)
        {
            label_x.Text = "X:" + e.Reading.Acceleration.X.ToString();
            label_y.Text = "Y:" + e.Reading.Acceleration.Y.ToString();
            label_z.Text = "Z:" + e.Reading.Acceleration.Z.ToString();
        }

        private void Button_Clicked(object sender, EventArgs e)
        {
            accservice = DependencyService.Resolve<IServiceACC>();
            accservice.Init();
            accservice.ValuesChanged += linacc_read;
            accservice.ValuesChanged += SaveLin;
            Accelerometer.ReadingChanged += SaveAcc;
        }

        private void Button_Clicked2(object sender, EventArgs e)
        {
            accservice = DependencyService.Resolve<IServiceACC>();
            accservice.Stop();
            for(int i = 0; i < 40;i++)
            {
                buffer[i] = new AccVector();
            }
            index = 0;

            Array.Clear(velocity, 0, velocity.Length);
            Array.Clear(position, 0, position.Length);
            Array.Clear(acceleration, 0, acceleration.Length);

            Accelerometer.ReadingChanged -= SaveAcc;
        }

        private void Button_Clicked3(object sender, EventArgs e)
        {
            button_start.IsEnabled = false;
            accservice = DependencyService.Resolve<IServiceACC>();
            accservice.Init();
            accservice.ValuesChanged += linacc_read;
            accservice.ValuesChanged += Calibrate;

            Device.StartTimer(TimeSpan.FromSeconds(5), () =>
            {
                accservice.ValuesChanged -= Calibrate;
                accservice.ValuesChanged -= linacc_read;
                return false; // return true to repeat counting, false to stop timer
            });

            for (int i = 0; i < 40; i++)
            {
                buffer[i] = new AccVector();
            }
            index = 0;
            button_start.IsEnabled = true;
        }

        private void SaveAcc(object sender, AccelerometerChangedEventArgs e)
        {
            AccVector vec = new AccVector(e.Reading.Acceleration.X, e.Reading.Acceleration.Y, e.Reading.Acceleration.Z);
            System.IO.File.AppendAllText(accpath, vec.ToString() + System.Environment.NewLine);
        }

        private void SaveLin(object sender, EventArgs e)
        {
            AccEventArgs args = e as AccEventArgs;
            AccVector vec = new AccVector(args.x, args.y, args.z);
            integration(vec);
            System.IO.File.AppendAllText(linpath, vec.ToString() + System.Environment.NewLine);
        }

        private void Calibrate(object sender, EventArgs e)
        {
            AccEventArgs args = e as AccEventArgs;
            AccVector vec = new AccVector(args.x, args.y, args.z);

            buffer[index++] = vec;
            if (index == 40) index = 0;

            double accelerationx = 0;
            double accelerationy = 0;
            double accelerationz = 0;

            foreach (var element in buffer)
            {
                accelerationx += element.x;
                accelerationy += element.y;
                accelerationz += element.z;
            }
            calibration[0] = accelerationx / 40;
            calibration[1] = accelerationy / 40;
            calibration[2] = accelerationz / 40;

            AccVector accvector = new AccVector((float)calibration[0], (float)calibration[1], (float)calibration[2]);
            System.IO.File.AppendAllText(calipath, accvector.ToString() + System.Environment.NewLine);
        }

        private void integration(AccVector vec)
        {
            buffer[index++] = vec;
            if (index == 40) index = 0;

            double accelerationx = 0;
            double accelerationy = 0;
            double accelerationz = 0;

            foreach (var element in buffer)
            {
                accelerationx += element.x;
                accelerationy += element.y;
                accelerationz += element.z;
            }
            acceleration[1, 0] = accelerationx / 40 + calibration[0];
            acceleration[1, 1] = accelerationy / 40 + calibration[1];
            acceleration[1, 2] = accelerationz / 40 + calibration[2];

            AccVector accvector = new AccVector((float)acceleration[1, 0], (float)acceleration[1, 1], (float)acceleration[1, 2]);
            System.IO.File.AppendAllText(ringpath, accvector.ToString() + System.Environment.NewLine);

            //integrate
            velocity[1, 0] = velocity[0, 0] + acceleration[0, 0] + ((acceleration[1, 0] - acceleration[0, 0]) / 2);
            velocity[1, 1] = velocity[0, 1] + acceleration[0, 1] + ((acceleration[1, 1] - acceleration[0, 1]) / 2);
            velocity[1, 2] = velocity[0, 2] + acceleration[0, 2] + ((acceleration[1, 2] - acceleration[0, 2]) / 2);



            AccVector velovector = new AccVector((float)velocity[1, 0], (float)velocity[1, 1], (float)velocity[1, 2]);
            System.IO.File.AppendAllText(velopath, velovector.ToString() + System.Environment.NewLine);

            //integrate
            position[1, 0] = position[0, 0] + velocity[0, 0] + ((velocity[1, 0] - velocity[0, 0]) / 2);
            position[1, 1] = position[0, 1] + velocity[0, 1] + ((velocity[1, 1] - velocity[0, 1]) / 2);
            position[1, 2] = position[0, 2] + velocity[0, 2] + ((velocity[1, 2] - velocity[0, 2]) / 2);

            AccVector posvector = new AccVector((float)position[1, 0], (float)position[1, 1], (float)position[1, 2]);
            System.IO.File.AppendAllText(positionpath, posvector.ToString() + System.Environment.NewLine);

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
