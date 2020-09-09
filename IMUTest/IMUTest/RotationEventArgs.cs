using System;
using System.Collections.Generic;
using System.Text;

namespace IMUTest
{
    public class RotationEventArgs : EventArgs
    {
        public float x;
        public float y;
        public float z;
        public float scalar;

        public float[] values;

        public RotationEventArgs(float px,float py,float pz, float scal)
        {
            this.x = px;
            this.y = py;
            this.z = pz;
            this.scalar = scal;
            this.values = new float[] { px, py, pz, scal };
        }
    }
}
