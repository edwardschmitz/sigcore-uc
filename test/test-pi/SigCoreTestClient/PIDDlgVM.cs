using SigCoreCommon;
using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace Certification {
    internal class PIDDlgVM : INotifyPropertyChanged {
        private readonly SigCoreSystem _system;
        private PID_LOOP.Config _config;
        private readonly uint _channel;

        public PIDDlgVM(SigCoreSystem system, uint channel) {
            _system = system;
            _channel = channel;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public async Task LoadAsync() {
            //_curVal = await _system.GetPIDCurValAsync(_channel);
            //_config = await _system.GetPIDConfigAsync(_channel);
            NotifyAll();
        }

        private void NotifyAll() {
            Notify(nameof(Name));
            Notify(nameof(Number));
        }

        public string Name {
            get => _config.Title;
            set { _config.Title = value; Notify(nameof(Name)); }
        }

        public uint Number => _channel;

    }
}
