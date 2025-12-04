using SigCoreCommon;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ControlPanel.Dialogs {
    public class AOutConfigDlgVM : ViewModelBase {
        private readonly A_OUT.AnalogOutChannelConfig _config;

        public AOutConfigDlgVM(A_OUT.AnalogOutChannelConfig config) {
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

        public string Units {
            get { return _config.Units; }
            set {
                if (_config.Units != value) {
                    _config.Units = value;
                    OnPropertyChanged();
                }
            }
        }

        public double VoltageScaleM {
            get { return Convert.ToDouble(_config.VoltageScaleM); }
            set {
                if (_config.VoltageScaleM != value) {
                    _config.VoltageScaleM = value;
                    OnPropertyChanged();
                }
            }
        }

        public double VoltageScaleB {
            get { return Convert.ToDouble(_config.VoltageScaleB); }
            set {
                if (_config.VoltageScaleB != value) {
                    _config.VoltageScaleB = value;
                    OnPropertyChanged();
                }
            }
        }

        public A_OUT.OutputMode Mode {
            get { return _config.Mode; }
            set {
                if (_config.Mode != value) {
                    _config.Mode = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsAuto {
            get { return _config.IsAuto; }
            set {
                if (_config.IsAuto != value) {
                    _config.IsAuto = value;
                    OnPropertyChanged();
                }
            }
        }
        public int Precision {
            get { return _config.Precision; }
            set {
                if (_config.Precision != value) {
                    _config.Precision = value;
                    OnPropertyChanged();
                }
            }
        }

        public A_OUT.DisplayFormat Display {
            get { return _config.Display; }
            set {
                if (_config.Display != value) {
                    _config.Display = value;
                    OnPropertyChanged();
                }
            }
        }

        public A_OUT.AnalogOutChannelConfig Config {
            get { return _config; }
        }

        public static A_OUT.OutputMode[] OutputModes {
            get { return (A_OUT.OutputMode[])System.Enum.GetValues(typeof(A_OUT.OutputMode)); }
        }
    }
}
