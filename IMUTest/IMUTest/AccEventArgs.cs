using System;
using System.Collections.Generic;
using System.Text;

namespace IMUTest
{
    public class AccEventArgs : EventArgs
    {
        public float x;
        public float y;
        public float z;
        public DateTime stamp;

        public AccEventArgs(float px,float py,float pz,DateTime stamp)
        {
            this.x = px;
            this.y = py;
            this.z = pz;
            this.stamp = stamp;
        }
    }
}
