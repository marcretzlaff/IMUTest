using System;
using System.Collections.Generic;
using System.Text;

namespace IMUTest
{
    public interface IServiceACC
    {
        public void Init();
        public void Start();
        public void Stop();
        public event EventHandler ValuesChanged;
    }
}
