using SigCoreCommon;
using SigCoreCommon.Payloads;

namespace ControlPanel.Dialogs {
    public class SystemPropertiesVM : ViewModelBase {
        private readonly SystemConfigPayload _config;

        public SystemPropertiesVM(SystemConfigPayload config) {
            _config = config;
        }

        public string SystemName {
            get => _config.SystemName;
            set {
                if (_config.SystemName != value) {
                    _config.SystemName = value;
                    OnPropertyChanged();
                }
            }
        }
        public static NameValueItem[] SampleRates {
            get {
                (string Name, int Value)[] src = SystemConfigPayload.SamplingRates;
                NameValueItem[] result = new NameValueItem[src.Length];

                for (int i = 0; i < src.Length; i++) {
                    result[i] = new NameValueItem(src[i].Name, src[i].Value);
                }

                return result;
            }
        }

        public string SystemSerialNumber => _config.SystemSerialNumber;
        public string HardwareVersion => _config.HardwareVersion;
        public string SoftwareVersion => _config.SoftwareVersion;

        public bool UseLastKnownValuesAtStartup {
            get => _config.UseLastKnownValuesAtStartup;
            set {
                if (_config.UseLastKnownValuesAtStartup != value) {
                    _config.UseLastKnownValuesAtStartup = value;
                    OnPropertyChanged();
                }
            }
        }

        public int AnalogSamplingRate {
            get => _config.AnalogSamplingRate;
            set {
                if (_config.AnalogSamplingRate != value) {
                    _config.AnalogSamplingRate = value;
                    OnPropertyChanged();
                }
            }
        }

        public int PWMFrequency {
            get => _config.PWMFrequency;
            set {
                if (_config.PWMFrequency != value) {
                    _config.PWMFrequency = value;
                    OnPropertyChanged();
                }
            }
        }

        public SystemConfigPayload Config => _config;
    }
}
