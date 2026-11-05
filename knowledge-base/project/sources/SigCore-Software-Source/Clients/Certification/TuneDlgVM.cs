using SigCoreCommon;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Certification {
    internal class TuneDlgVM : INotifyPropertyChanged {
        private SigCoreSystem _system;
        private PID_LOOP.Config _config;


        public TuneDlgVM(SigCoreSystem system) {
            _system = system;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
