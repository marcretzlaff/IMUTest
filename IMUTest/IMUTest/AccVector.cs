using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace IMUTest
{
    public class AccVector
    {
        public float x;
        public float y;
        public float z;
        DateTime time;

        public AccVector(float parx, float pary, float parz)
        {
            x = parx;
            y = pary;
            z = parz;
            time = DateTime.Now;
        }

        public AccVector()
        {
            x = 0;
            y = 0;
            z = 0;
            time = DateTime.UtcNow;
        }

        public override string ToString()
        {
            return time.ToString("ss.fff") + ";" + x.ToString().Replace(',','.') + ";" + y.ToString().Replace(',', '.') + ";" + z.ToString().Replace(',', '.');
        }
    }
}
