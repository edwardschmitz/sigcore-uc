using SigCoreCommon; // assuming RelayConfig lives there
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ControlPanel.Dialogs {
    public class RelayConfigDlgVM : ViewModelBase {
        private readonly RELAY_OUT.RelayConfig _config;

        public RelayConfigDlgVM(RELAY_OUT.RelayConfig config) {
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

        public bool DefaultState {
            get { return _config.DefaultState; }
            set {
                if (_config.DefaultState != value) {
                    _config.DefaultState = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool FailSafeState {
            get { return _config.FailSafeState; }
            set {
                if (_config.FailSafeState != value) {
                    _config.FailSafeState = value;
                    OnPropertyChanged();
                }
            }
        }

        public RELAY_OUT.RelayConfig Config { get { return _config; } }

    }
}
