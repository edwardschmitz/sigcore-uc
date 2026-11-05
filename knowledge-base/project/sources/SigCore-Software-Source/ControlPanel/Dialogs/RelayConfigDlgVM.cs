using SigCoreCommon.Payloads;

namespace ControlPanel.Dialogs {
    public class RelayConfigDlgVM : ViewModelBase {
        private readonly RelayConfigPayload _config;

        public RelayConfigDlgVM(RelayConfigPayload config) {
            _config = config;
        }

        public string Name {
            get => _config.Name;
            set {
                if (_config.Name != value) {
                    _config.Name = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool DefaultState {
            get => _config.DefaultState;
            set {
                if (_config.DefaultState != value) {
                    _config.DefaultState = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool FailSafeState {
            get => _config.FailSafeState;
            set {
                if (_config.FailSafeState != value) {
                    _config.FailSafeState = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool InvertedLogic {
            get => _config.InvertedLogic;
            set {
                if (_config.InvertedLogic != value) {
                    _config.InvertedLogic = value;
                    OnPropertyChanged();
                }
            }
        }

        public RelayConfigPayload Config => _config;
    }
}