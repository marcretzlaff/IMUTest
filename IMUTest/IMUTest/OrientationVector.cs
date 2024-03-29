﻿using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace IMUTest
{
    public class OrientationVector
    {
        public float x;
        public float y;
        public float z;

        public OrientationVector(float parx, float pary, float parz)
        {
            x = parx;
            y = pary;
            z = parz;
        }

        public OrientationVector()
        {
            x = 0;
            y = 0;
            z = 0;
        }

        public string ToString(DateTime time)
        {
            return time.ToString("ss.fff") + ";" + x.ToString().Replace(',','.') + ";" + y.ToString().Replace(',', '.') + ";" + z.ToString().Replace(',', '.');
        }
    }
}
