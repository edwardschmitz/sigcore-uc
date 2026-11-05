using Iot.Device.Mcp23xxx;
using Newtonsoft.Json.Linq;
using SigCoreCommon;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Certification {
    public class MainWindowVM : INotifyPropertyChanged {
        public event PropertyChangedEventHandler PropertyChanged;


        
        private string _serverName;
        private string _serialNumber;
        private string _status;
        private string _firmware;
        private string _hwRevision;
        private string _hostname;
        private string _ipAddress;
        private string _upTime;
        private string _results;
        private string _scProtocol;
        private string _mBusProtocol;
        public string ServerName {
            get => _serverName;
            set => SetField(ref _serverName, value);
        }

        public string SerialNumber {
            get => _serialNumber;
            set => SetField(ref _serialNumber, value);
        }

        public string Status {
            get => _status;
            set => SetField(ref _status, value);
        }

        public string Firmware {
            get => _firmware;
            set => SetField(ref _firmware, value);
        }

        public string HWRevision {
            get => _hwRevision;
            set => SetField(ref _hwRevision, value);
        }

        public string SCProtocol {
            get => _scProtocol;
            set => SetField(ref _scProtocol, value);
        }

        public string MBusProtocol {
            get => _mBusProtocol;
            set => SetField(ref _mBusProtocol, value);
        }

        public string Hostname {
            get => _hostname;
            set => SetField(ref _hostname, value);
        }

        public string IPAddress {
            get => _ipAddress;
            set => SetField(ref _ipAddress, value);
        }

        public string UpTime {
            get => _upTime;
            set => SetField(ref _upTime, value);
        }

        public string Results {
            get => _results;
            set => SetField(ref _results, value);
        }

        public ICommand RelayLoopBackCommand { get; }
        public ICommand PWMLoopBackCommand { get; }
        public ICommand GNDSenseCommand { get; }
        public ICommand V5Command { get; }
        public ICommand V10Command { get; }
        public ICommand mV256Command { get; }
        public ICommand ModbusCommand { get; }
        public ICommand GetGlobalConfigCmd { get; }
        public ICommand SaveGlobalConfigCmd { get; }
        public SigCoreSystem System { get; internal set; }

        public MainWindowVM() {
            RelayLoopBackCommand = new RelayCommand(RelayLoopBackCmd);
            PWMLoopBackCommand = new RelayCommand(PWMTestCmd);
            GNDSenseCommand = new RelayCommand(_ => RunTest("GND Sense"));
            V5Command = new RelayCommand(_ => RunTest("5V Test"));
            V10Command = new RelayCommand(_ => RunTest("10V Test"));
            mV256Command = new RelayCommand(_ => RunTest("256mV Test"));
            ModbusCommand = new RelayCommand(_ => RunTest("Modbus Test"));
            GetGlobalConfigCmd = new RelayCommand(GetConfig);
            SaveGlobalConfigCmd = new RelayCommand(SaveConfig);
        }
        private void RelayLoopBackCmd(object obj) {
            TestDlg dlg = new TestDlg();
            LoopBackTest test = new LoopBackTest(System);
            TestVM vm = new TestVM(test);
            dlg.DataContext = vm;
            dlg.ShowDialog();
        }
        private void PWMTestCmd(object obj) {
            TestDlg dlg = new TestDlg();
            PWMTest test = new PWMTest(System);
            TestVM vm = new TestVM(test);
            dlg.DataContext = vm;
            dlg.ShowDialog();
        }
        private void SaveConfig(object obj) {
            throw new NotImplementedException();
        }

        private async void GetConfig(object obj) {
            try {
                HardwareManager.Config config = await System.GetGlobalConfigAsync();

                Firmware = config.SoftwareVersion;
                ServerName = config.SystemName;
                SerialNumber = config.SystemSerialNumber;
                Status = "Online";
                HWRevision = config.HardwareVersion;
                SCProtocol = "Operational";
                MBusProtocol = "State Unknown";
                Hostname = System.Host;
                IPAddress = System.IP + ":" + System.Port;
                UpTime = "Unknown";
 
            } catch (TimeoutException ex) {
                Console.WriteLine("Timed out: " + ex.Message);
            }
        }
        private void RunTest(string testName) {
            Results += $"Running: {testName}\n";
            // TODO: Insert actual logic here
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null) {
            if (Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }

    public class RelayCommand : ICommand {
        private readonly Action<object> _execute;
        private readonly Predicate<object> _canExecute;

        public event EventHandler CanExecuteChanged;

        public RelayCommand(Action<object> execute, Predicate<object> canExecute = null) {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute?.Invoke(parameter) ?? true;

        public void Execute(object parameter) => _execute(parameter);
    }
}