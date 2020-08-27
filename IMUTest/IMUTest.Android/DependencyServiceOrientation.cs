using System;
using Android.App;
using Android.Content.PM;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Android.Hardware;
using IMUTest;
using Xamarin.Forms;
using IMUTest.Droid;


[assembly: Dependency(typeof(DependencyServiceOrientation))]
namespace IMUTest.Droid
{
    public class DependencyServiceOrientation : Activity,IServiceOrientation, ISensorEventListener
    {
        public event EventHandler ValuesChanged;
        SensorManager sensorManager = null;
        Sensor orientationSensor = null;

        public void Init()
        {
            sensorManager = (SensorManager)Forms.Context.GetSystemService(SensorService);
            orientationSensor = sensorManager.GetDefaultSensor(SensorType.Orientation);
            sensorManager.RegisterListener(this, orientationSensor, SensorDelay.Ui);
        }

        public void Start()
        {
            //Sensor Speed UI: 40ms Normal: 40ms Game: 40ms Fastest: 20ms
            sensorManager.RegisterListener(this, orientationSensor, SensorDelay.Fastest);
        }

        public void Stop()
        {
            sensorManager.UnregisterListener(this, orientationSensor);
        }

        public void OnAccuracyChanged(Sensor sensor, [GeneratedEnum] SensorStatus accuracy)
        {
            return;
        }

        public void OnSensorChanged(SensorEvent e)
        {
            if (e.Sensor.Type != SensorType.Orientation)
                return;

            var args = new OrientationEventArgs(e.Values[0], e.Values[1], e.Values[2]);
            ValuesChanged?.Invoke(this, args);
        }
    }
}
