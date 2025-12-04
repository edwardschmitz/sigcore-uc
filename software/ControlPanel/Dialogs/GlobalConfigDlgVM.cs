using SigCoreCommon;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace ControlPanel.Dialogs {
    public class GlobalConfigDlgVM : ViewModelBase {
        private readonly HardwareManager.Config _config;

        public GlobalConfigDlgVM(HardwareManager.Config config) {
            _config = config;
        }

        public string SystemName {
            get { return _config.SystemName; }
            set { if (_config.SystemName != value) { _config.SystemName = value; OnPropertyChanged(); } }
        }

        public string SystemSerialNumber => _config.SystemSerialNumber;
        public string HardwareVersion => _config.HardwareVersion;
        public string SoftwareVersion => _config.SoftwareVersion;

        public bool UseLastKnownValuesAtStartup {
            get { return _config.UseLastKnownValuesAtStartup; }
            set { if (_config.UseLastKnownValuesAtStartup != value) { _config.UseLastKnownValuesAtStartup = value; OnPropertyChanged(); } }
        }

        public HardwareManager.Config.SamplesPerSecond AnalogSamplingRate {
            get { return _config.AnalogSamplingRate; }
            set { if (_config.AnalogSamplingRate != value) { _config.AnalogSamplingRate = value; OnPropertyChanged(); } }
        }

        public int PWMFrequency {
            get { return _config.PWMFrequency; }
            set { if (_config.PWMFrequency != value) { _config.PWMFrequency = value; OnPropertyChanged(); } }
        }

        public string Hostname {
            get { return _config.Hostname; }
            set { if (_config.Hostname != value) { _config.Hostname = value; OnPropertyChanged(); } }
        }

        public bool DhcpEnabled {
            get { return _config.DhcpEnabled; }
            set { if (_config.DhcpEnabled != value) { _config.DhcpEnabled = value; OnPropertyChanged(); } }
        }

        public string IpAddress {
            get { return _config.IpAddress; }
            set { if (_config.IpAddress != value) { _config.IpAddress = value; OnPropertyChanged(); } }
        }

        public string SubnetMask {
            get { return _config.SubnetMask; }
            set { if (_config.SubnetMask != value) { _config.SubnetMask = value; OnPropertyChanged(); } }
        }

        public string Gateway {
            get { return _config.Gateway; }
            set { if (_config.Gateway != value) { _config.Gateway = value; OnPropertyChanged(); } }
        }

        public string Dns {
            get { return _config.Dns; }
            set { if (_config.Dns != value) { _config.Dns = value; OnPropertyChanged(); } }
        }

        public string SystemPassword {
            get { return _config.SystemPassword; }
            set { if (_config.SystemPassword != value) { _config.SystemPassword = value; OnPropertyChanged(); } }
        }

        public HardwareManager.Config Config => _config;
    }
}
