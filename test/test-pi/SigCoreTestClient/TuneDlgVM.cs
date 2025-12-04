using SigCoreCommon;
using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace Certification {
    internal class TuneDlgVM : INotifyPropertyChanged {
        private readonly SigCoreSystem _system;
        private PID_LOOP.Config _config;
        private readonly uint _loopIndex;

        public TuneDlgVM(SigCoreSystem system, uint loopIndex) {
            _system = system;
            _loopIndex = loopIndex;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void Notify(string prop) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        // ================================================================
        //  Initialization / Load
        // ================================================================
        public async Task LoadAsync() {
            //_config = _system.GetPIDConfig(_loopIndex);
            NotifyAll();
        }

        private void NotifyAll() {
            Notify(nameof(Title));
            Notify(nameof(Kp));
            Notify(nameof(Ki));
            Notify(nameof(Kd));
            Notify(nameof(OutputMin));
            Notify(nameof(OutputMax));
            Notify(nameof(PVMin));
            Notify(nameof(PVMax));
            Notify(nameof(IsAutoMode));
            Notify(nameof(PvSource));
            Notify(nameof(OutputDestination));
        }

        // ================================================================
        //  Bindable Properties (directly mapped to Config)
        // ================================================================
        public string Title {
            get => _config?.Title ?? "";
            set { _config.Title = value; Notify(nameof(Title)); }
        }

        public double Kp {
            get => _config.Kp;
            set { _config.Kp = value; Notify(nameof(Kp)); }
        }

        public double Ki {
            get => _config.Ki;
            set { _config.Ki = value; Notify(nameof(Ki)); }
        }

        public double Kd {
            get => _config.Kd;
            set { _config.Kd = value; Notify(nameof(Kd)); }
        }

        public double OutputMin {
            get => _config.OutputMin;
            set { _config.OutputMin = value; Notify(nameof(OutputMin)); }
        }

        public double OutputMax {
            get => _config.OutputMax;
            set { _config.OutputMax = value; Notify(nameof(OutputMax)); }
        }

        public double PVMin {
            get => _config.PVMin;
            set { _config.PVMin = value; Notify(nameof(PVMin)); }
        }

        public double PVMax {
            get => _config.PVMax;
            set { _config.PVMax = value; Notify(nameof(PVMax)); }
        }


        public bool IsAutoMode {
            get => _config.Auto;
            set {
                _config.Auto = value;
                Notify(nameof(IsAutoMode));
            }
        }

        public uint PvSource {
            get => _config.PvSource;
            set { _config.PvSource = value; Notify(nameof(PvSource)); }
        }

        public uint OutputDestination {
            get => _config.OutputDestination;
            set { _config.OutputDestination = value; Notify(nameof(OutputDestination)); }
        }

        // ================================================================
        //  Commit Changes
        // ================================================================
        public void CommitAsync() {
            _system.SetPIDConfig(_loopIndex, _config);
        }
    }
}
