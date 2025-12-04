using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControlPanel.Parts {
    public class CPSettingsVM : ViewModelBase {
        private int sampling_rate;
        public int SamplingRate {
            get => sampling_rate;
            set {
                sampling_rate = value;
                OnPropertyChanged();
            }
        }

        public Dictionary<string, bool> RelayDefaults { get; set; }
        public Dictionary<string, double> AnalogDefaults { get; set; }

        public CPSettingsVM() {
            SamplingRate = 1000; // Default value

            RelayDefaults = new Dictionary<string, bool> {
            { "R1", false },
            { "R2", false },
            { "R3", false },
            { "R4", false },
            { "R5", false },
            { "R6", false },
            { "R7", false },
            { "R8", false }
        };

            AnalogDefaults = new Dictionary<string, double> {
            { "AO1", 0.0 },
            { "AO2", 0.0 }
        };
        }

    }
    public class KeyValueWrapper : INotifyPropertyChanged {
        private bool _value;
        public string Key { get; set; }

        public bool Value {
            get => _value;
            set {
                if (_value != value) {
                    _value = value;
                    OnPropertyChanged(nameof(Value));
                }
            }
        }

        public KeyValueWrapper(string key, bool value) {
            Key = key;
            _value = value;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
