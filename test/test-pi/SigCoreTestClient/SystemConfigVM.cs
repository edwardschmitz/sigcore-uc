using System;
using System.ComponentModel;
using static SigCoreCommon.HardwareManager;

namespace SigCoreTestClient {
    public class SystemConfigVM : INotifyPropertyChanged {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private Config _config;
        public Config Config => _config;

        public SystemConfigVM(Config config) {
            _config = config;

            SystemName = config.SystemName;
            SerialNumber = config.SystemSerialNumber;
            HardwareVersion = config.HardwareVersion;
            SoftwareVersion = config.SoftwareVersion;
            UseLastKnown = config.UseLastKnownValuesAtStartup;
            SamplingRate = config.AnalogSamplingRate;
            PWMFrequency = config.PWMFrequency;
            Hostname = config.Hostname;
            DhcpEnabled = config.DhcpEnabled;
            IpAddress = config.IpAddress;
            SubnetMask = config.SubnetMask;
            Gateway = config.Gateway;
            Dns = config.Dns;
            SystemPassword = config.SystemPassword;
        }

        private string _systemName;
        public string SystemName {
            get => _systemName;
            set { _systemName = value; OnPropertyChanged(nameof(SystemName)); }
        }

        private string _serialNumber;
        public string SerialNumber {
            get => _serialNumber;
            set { _serialNumber = value; OnPropertyChanged(nameof(SerialNumber)); }
        }

        private string _hardwareVersion;
        public string HardwareVersion {
            get => _hardwareVersion;
            set { _hardwareVersion = value; OnPropertyChanged(nameof(HardwareVersion)); }
        }

        private string _softwareVersion;
        public string SoftwareVersion {
            get => _softwareVersion;
            set { _softwareVersion = value; OnPropertyChanged(nameof(SoftwareVersion)); }
        }

        private bool _useLastKnown;
        public bool UseLastKnown {
            get => _useLastKnown;
            set { _useLastKnown = value; OnPropertyChanged(nameof(UseLastKnown)); }
        }

        private Config.SamplesPerSecond _samplingRate;
        public Config.SamplesPerSecond SamplingRate {
            get => _samplingRate;
            set { _samplingRate = value; OnPropertyChanged(nameof(SamplingRate)); }
        }

        private int _pwmFrequency;
        public int PWMFrequency {
            get => _pwmFrequency;
            set { _pwmFrequency = value; OnPropertyChanged(nameof(PWMFrequency)); }
        }

        private string _hostname;
        public string Hostname {
            get => _hostname;
            set { _hostname = value; OnPropertyChanged(nameof(Hostname)); }
        }

        private bool _dhcpEnabled;
        public bool DhcpEnabled {
            get => _dhcpEnabled;
            set { _dhcpEnabled = value; OnPropertyChanged(nameof(DhcpEnabled)); }
        }

        private string _ipAddress;
        public string IpAddress {
            get => _ipAddress;
            set { _ipAddress = value; OnPropertyChanged(nameof(IpAddress)); }
        }

        private string _subnetMask;
        public string SubnetMask {
            get => _subnetMask;
            set { _subnetMask = value; OnPropertyChanged(nameof(SubnetMask)); }
        }

        private string _gateway;
        public string Gateway {
            get => _gateway;
            set { _gateway = value; OnPropertyChanged(nameof(Gateway)); }
        }

        private string _dns;
        public string Dns {
            get => _dns;
            set { _dns = value; OnPropertyChanged(nameof(Dns)); }
        }

        private string _systemPassword;
        public string SystemPassword {
            get => _systemPassword;
            set { _systemPassword = value; OnPropertyChanged(nameof(SystemPassword)); }
        }

        public void ApplyChanges() {
            try {
                _config.SystemName = SystemName;
                _config.UseLastKnownValuesAtStartup = UseLastKnown;
                _config.AnalogSamplingRate = SamplingRate;
                _config.PWMFrequency = PWMFrequency;
                _config.Hostname = Hostname;
                _config.DhcpEnabled = DhcpEnabled;
                _config.IpAddress = IpAddress;
                _config.SubnetMask = SubnetMask;
                _config.Gateway = Gateway;
                _config.Dns = Dns;
                _config.SystemPassword = SystemPassword;
            } catch (Exception ex) {
                System.Windows.MessageBox.Show($"Error applying changes: {ex.Message}", "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        public Config.SamplesPerSecond[] SamplingRateValues =>
            (Config.SamplesPerSecond[])Enum.GetValues(typeof(Config.SamplesPerSecond));
    }
}
