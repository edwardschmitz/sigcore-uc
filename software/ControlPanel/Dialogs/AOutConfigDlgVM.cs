using SigCoreCommon;
using SigCoreCommon.Payloads;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ControlPanel.Dialogs {
    public class AOutConfigDlgVM : ViewModelBase {
        private readonly AOutConfigPayload _config;

        public AOutConfigDlgVM(AOutConfigPayload config) {
            _config = config;
        }

        public static NameValueItem[] DisplayFormats {
            get {
                (string Name, int Value)[] src = AOutConfigPayload.DisplayFormats;
                NameValueItem[] result = new NameValueItem[src.Length];

                for (int i = 0; i < src.Length; i++) {
                    result[i] = new NameValueItem(src[i].Name, src[i].Value);
                }

                return result;
            }
        }

        public static NameValueItem[] Modes {
            get {
                (string Name, int Value)[] src = AOutConfigPayload.OutputModes;
                NameValueItem[] result = new NameValueItem[src.Length];

                for (int i = 0; i < src.Length; i++) {
                    result[i] = new NameValueItem(src[i].Name, src[i].Value);
                }

                return result;
            }
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

        public int Mode {
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

        public int Display {
            get { return _config.Display; }
            set {
                if (_config.Display != value) {
                    _config.Display = value;
                    OnPropertyChanged();
                }
            }
        }

        public AOutConfigPayload Config {
            get { return _config; }
        }

        public static A_OUT.OutputMode[] OutputModes {
            get { return (A_OUT.OutputMode[])System.Enum.GetValues(typeof(A_OUT.OutputMode)); }
        }
    }
}
