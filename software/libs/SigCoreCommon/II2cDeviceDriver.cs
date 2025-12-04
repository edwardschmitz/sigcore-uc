using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SigCoreCommon {
    public interface II2cDeviceDriver {
        string DeviceName { get; }
        int I2cAddress { get; }
        bool Probe();
        void Initialize();
    }
}
