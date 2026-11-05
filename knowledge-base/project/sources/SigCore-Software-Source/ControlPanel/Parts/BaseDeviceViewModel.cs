using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControlPanel.Parts {
    public abstract class BaseDeviceVM : ViewModelBase {
        public virtual string Name { get; set; } // Tab Title
        public string Part { get; protected set; } // Identifies device type
        public virtual void Save() { }
        public virtual void Load() { }

        public virtual void StartSampling() { }

        public virtual void Closing() {
            throw new NotImplementedException();
        }

        public virtual void Stop() {
            throw new NotImplementedException();
        }
    }
}
