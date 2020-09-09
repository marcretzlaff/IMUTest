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


[assembly: Dependency(typeof(DependencyServiceRotation))]
namespace IMUTest.Droid
{
    public class DependencyServiceRotation : Activity,IServiceRotation, ISensorEventListener
    {
        public event EventHandler ValuesChanged;
        SensorManager sensorManager = null;
        Sensor rotationSensor = null;

        public void Init()
        {
            sensorManager = (SensorManager)Forms.Context.GetSystemService(SensorService);
            rotationSensor = sensorManager.GetDefaultSensor(SensorType.RotationVector);
            sensorManager.RegisterListener(this, rotationSensor, SensorDelay.Ui);
        }

        public void Start()
        {
            //Sensor Speed UI: 40ms Normal: 40ms Game: 40ms Fastest: 20ms
            sensorManager.RegisterListener(this, rotationSensor, SensorDelay.Fastest);
        }

        public void Stop()
        {
            sensorManager.UnregisterListener(this, rotationSensor);
        }

        public void OnAccuracyChanged(Sensor sensor, [GeneratedEnum] SensorStatus accuracy)
        {
            return;
        }

        public void OnSensorChanged(SensorEvent e)
        {
            if (e.Sensor.Type != SensorType.RotationVector)
                return;

            var args = new RotationEventArgs(e.Values[0], e.Values[1], e.Values[2], e.Values[3]);
            ValuesChanged?.Invoke(this, args);
        }
    }
}
