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

            if (System.IO.File.Exists(linpath)) System.IO.File.Delete(linpath);
            if (System.IO.File.Exists(accpath)) System.IO.File.Delete(accpath);
            if (System.IO.File.Exists(ringpath)) System.IO.File.Delete(ringpath);
            if (System.IO.File.Exists(accpath)) System.IO.File.Delete(velopath);
            if (System.IO.File.Exists(ringpath)) System.IO.File.Delete(positionpath);
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
            Accelerometer.ReadingChanged -= SaveAcc;
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
            ringBuffer(vec);
            System.IO.File.AppendAllText(linpath, vec.ToString() + System.Environment.NewLine);
        }

        private void ringBuffer(AccVector vec)
        {
            buffer[index++] = vec;
            if (index == 40) index = 0;

            double sumx = 0;
            double sumy = 0;
            double sumz = 0;
            foreach(var element in buffer)
            {
                sumx += element.x;
                sumy += element.y;
                sumz += element.z;
            }
            sumx /= 40;
            sumy /= 40;
            sumz /= 40;

            AccVector vector = new AccVector((float)sumx,(float)sumy,(float)sumz);
            System.IO.File.AppendAllText(ringpath, vector.ToString() + System.Environment.NewLine);
        }

    }
}
