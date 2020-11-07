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


[assembly: Dependency(typeof(DependencyServiceAcc))]
namespace IMUTest.Droid
{
    public class DependencyServiceAcc : Activity,IServiceACC, ISensorEventListener
    {
        public event EventHandler ValuesChanged;
        SensorManager sensorManager = null;
        Sensor linearSensor = null;

        public void Init()
        {
            sensorManager = (SensorManager)Android.App.Application.Context.GetSystemService(SensorService);
            linearSensor = sensorManager.GetDefaultSensor(SensorType.LinearAcceleration);
            sensorManager.RegisterListener(this, linearSensor, SensorDelay.Fastest);
        }

        public void Start()
        {
            //Sensor Speed UI: 40ms Normal: 40ms Game: 40ms Fastest: 20ms
            sensorManager.RegisterListener(this, linearSensor, SensorDelay.Fastest);
        }

        public void Stop()
        {
            sensorManager.UnregisterListener(this, linearSensor);
        }

        public void OnAccuracyChanged(Sensor sensor, [GeneratedEnum] SensorStatus accuracy)
        {
            return;
        }

        public void OnSensorChanged(SensorEvent e)
        {
            if (e.Sensor.Type != SensorType.LinearAcceleration)
                return;

            var args = new AccEventArgs(e.Values[0], e.Values[1], e.Values[2], DateTime.UtcNow);
            ValuesChanged?.Invoke(this, args);
        }
    }
}
