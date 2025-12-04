using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Certification {
    public class TestVM : INotifyPropertyChanged {
        private readonly ITest _test;
        private string _progress;
        private bool _running;
        private CancellationTokenSource _cts;

        public string Name { get { return _test.Name; } }
        public string Instructions { get { return _test.Instructions; } }

        public string Progress {
            get { return _progress; }
            private set {
                if (_progress != value) {
                    _progress = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ModeStr {
            get { return _running ? "Stop Test" : "Run Test"; }
        }

        public ICommand RunCmd { get; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public TestVM(ITest test) {
            _test = test;
            _progress = "Idle";
            RunCmd = new RelayCommand(RunTest);
        }

        private async void RunTest(object obj) {
            if (_running) {
                if (_cts != null) {
                    _cts.Cancel();
                    Progress = "Cancelling...";
                }
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ModeStr)));
            } else {
                _running = true;
                OnPropertyChanged(nameof(ModeStr));

                _cts = new CancellationTokenSource();
                CancellationToken token = _cts.Token;

                Progress<string> reporter = new Progress<string>(message => Progress = message);

                try {
                    bool result = await _test.RunAsync(reporter, token);
                    Progress = result ? "Test Passed" : "Test Failed";
                } catch (OperationCanceledException) {
                    Progress = "Test Cancelled";
                } catch (Exception ex) {
                    Progress = "Error: " + ex.Message;
                } finally {
                    _running = false;
                    _cts.Dispose();
                    _cts = null;
                    OnPropertyChanged(nameof(ModeStr));
                }
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ModeStr)));
            }
        }

        public async Task<bool> RunAsync(CancellationToken token) {
            Progress<string> reporter = new Progress<string>(message => Progress = message);
            bool result = await _test.RunAsync(reporter, token);
            return result;
        }
    }
}
