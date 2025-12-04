using System.ComponentModel;
using SigCoreCommon;
using static SigCoreCommon.RELAY_OUT;

namespace SigCoreTestClient {
    public class RelayConfigVM : INotifyPropertyChanged {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private RelayConfig _config;
        public RelayConfig Config => _config;

        public RelayConfigVM(RelayConfig config) {
            _config = config;
            Name = config.Name;
            DefaultState = config.DefaultState;
            FailSafeState = config.FailSafeState;
        }

        private string _name;
        public string Name { get => _name; set { _name = value; OnPropertyChanged(nameof(Name)); } }

        private bool _defaultState;
        public bool DefaultState { get => _defaultState; set { _defaultState = value; OnPropertyChanged(nameof(DefaultState)); } }

        private bool _failSafe;
        public bool FailSafeState { get => _failSafe; set { _failSafe = value; OnPropertyChanged(nameof(FailSafeState)); } }

        public void ApplyChanges() {
            _config.Name = Name;
            _config.DefaultState = DefaultState;
            _config.FailSafeState = FailSafeState;
        }
    }
}
