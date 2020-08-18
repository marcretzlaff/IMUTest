using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using IMUTest.Droid;
using Xamarin.Forms;

[assembly: Dependency(typeof(DependencyServiceFile))]
namespace IMUTest.Droid
{
    class DependencyServiceFile : IFileSystem
    {
        public string GetExternalStorage()
        { 
            Context context = Android.App.Application.Context; 
            var filePath = context.GetExternalFilesDir(""); 
            return filePath.Path; 
        }
    }
}