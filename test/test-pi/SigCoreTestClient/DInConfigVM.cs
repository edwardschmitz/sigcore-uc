using System.ComponentModel;
using SigCoreCommon;

namespace SigCoreTestClient {
    public class DInConfigVM : INotifyPropertyChanged {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private D_IN.DInConfig _config;
        public D_IN.DInConfig Config => _config;

        public DInConfigVM(D_IN.DInConfig config) {
            _config = config;
            Name = config.Name;
            DebounceMs = config.DebounceMs;
            Inverted = config.Inverted;
        }

        private string _name;
        public string Name {
            get => _name;
            set { _name = value; OnPropertyChanged(nameof(Name)); }
        }

        private int _debounce;
        public int DebounceMs {
            get => _debounce;
            set { _debounce = value; OnPropertyChanged(nameof(DebounceMs)); }
        }

        private bool _inverted;
        public bool Inverted {
            get => _inverted;
            set { _inverted = value; OnPropertyChanged(nameof(Inverted)); }
        }

        public void ApplyChanges() {
            _config.Name = Name;
            _config.DebounceMs = DebounceMs;
            _config.Inverted = Inverted;
        }
    }
}
