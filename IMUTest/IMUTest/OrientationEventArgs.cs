using System;
using System.Collections.Generic;
using System.Text;

namespace IMUTest
{
    public class OrientationEventArgs : EventArgs
    {
        public float azimoth;
        public float roll;
        public float pitch;

        public OrientationEventArgs(float px,float py,float pz)
        {
            this.azimoth = px;
            this.roll = py;
            this.pitch = pz;
        }
    }
}
