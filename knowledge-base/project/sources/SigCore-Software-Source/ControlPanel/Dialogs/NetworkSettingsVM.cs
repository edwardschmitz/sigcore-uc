using SigCoreCommon;
using SigCoreCommon.Payloads;

namespace ControlPanel.Dialogs {

    internal class NetworkSettingsVM : ViewModelBase {

        private readonly NetworkSettingsPayload _config;

        public NetworkSettingsVM(NetworkSettingsPayload config) {
            _config = config;
        }

        // ====================
        // System
        // ====================

        public string Hostname {
            get => _config.Hostname;
            set {
                if (_config.Hostname != value) {
                    _config.Hostname = value;
                    OnPropertyChanged();
                }
            }
        }

        // ====================
        // Ethernet (eth0)
        // ====================

        public bool EthDhcpEnabled {
            get => _config.EthDhcpEnabled;
            set {
                if (_config.EthDhcpEnabled != value) {
                    _config.EthDhcpEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        public string EthIpAddress {
            get => _config.EthIpAddress;
            set {
                if (_config.EthIpAddress != value) {
                    _config.EthIpAddress = value;
                    OnPropertyChanged();
                }
            }
        }

        public string EthSubnetMask {
            get => _config.EthSubnetMask;
            set {
                if (_config.EthSubnetMask != value) {
                    _config.EthSubnetMask = value;
                    OnPropertyChanged();
                }
            }
        }

        public string EthGateway {
            get => _config.EthGateway;
            set {
                if (_config.EthGateway != value) {
                    _config.EthGateway = value;
                    OnPropertyChanged();
                }
            }
        }

        public string EthDns {
            get => _config.EthDns;
            set {
                if (_config.EthDns != value) {
                    _config.EthDns = value;
                    OnPropertyChanged();
                }
            }
        }

        // ====================
        // Wi-Fi (wlan0)
        // ====================

        public string WifiSSID {
            get => _config.WifiSsid;
            set {
                if (_config.WifiSsid != value) {
                    _config.WifiSsid = value;
                    OnPropertyChanged();
                }
            }
        }

        public string WifiPSK {
            get => _config.WifiPsk;
            set {
                if (_config.WifiPsk != value) {
                    _config.WifiPsk = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool WifiDhcpEnabled {
            get => _config.WifiDhcpEnabled;
            set {
                if (_config.WifiDhcpEnabled != value) {
                    _config.WifiDhcpEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        public string WifiIpAddress {
            get => _config.WifiIpAddress;
            set {
                if (_config.WifiIpAddress != value) {
                    _config.WifiIpAddress = value;
                    OnPropertyChanged();
                }
            }
        }

        public string WifiSubnetMask {
            get => _config.WifiSubnetMask;
            set {
                if (_config.WifiSubnetMask != value) {
                    _config.WifiSubnetMask = value;
                    OnPropertyChanged();
                }
            }
        }

        public string WifiGateway {
            get => _config.WifiGateway;
            set {
                if (_config.WifiGateway != value) {
                    _config.WifiGateway = value;
                    OnPropertyChanged();
                }
            }
        }

        public string WifiDns {
            get => _config.WifiDns;
            set {
                if (_config.WifiDns != value) {
                    _config.WifiDns = value;
                    OnPropertyChanged();
                }
            }
        }

        // ====================
        // Access
        // ====================

        public NetworkSettingsPayload Config => _config;
    }
}
