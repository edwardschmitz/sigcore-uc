using SigCoreCommon;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using static SigCoreCommon.D_IN;

namespace ControlPanel.Dialogs {
    public class DInConfigDlgVM : ViewModelBase {
        private readonly DInConfig _config;

        public DInConfigDlgVM(DInConfig config) {
            _config = config;
        }

        public string Name {
            get { return _config.Name; }
            set {
                if (_config.Name != value) {
                    _config.Name = value;
                    OnPropertyChanged();
                }
            }
        }

        public int DebounceMs {
            get { return _config.DebounceMs; }
            set {
                if (_config.DebounceMs != value) {
                    _config.DebounceMs = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool Inverted {
            get { return _config.Inverted; }
            set {
                if (_config.Inverted != value) {
                    _config.Inverted = value;
                    OnPropertyChanged();
                }
            }
        }

        public DInConfig Config {
            get { return _config; }
        }
    }
}
