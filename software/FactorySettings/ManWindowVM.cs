using ControlPanel;
using SigCoreCommon;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using Zeroconf;

namespace FactorySettings {

    internal class MainWindowVM : ViewModelBase {
        private SigCoreSystem _system;

        // --------------------------------------------------------
        // Constructor
        // --------------------------------------------------------
        public MainWindowVM() {

            Date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            SystemName = "SigCore UC";

            // Single source of truth
            WebPath = "https://sigcoreuc.com/update/revA";

            ConnectionStatus = "Disconnected";
            IsConnected = false;

            ConnectCmd = new RelayCommand(Connect);
            ApplyFactorySettingsCmd = new RelayCommand(ApplyFactorySettings);
            GenerateFactorySettingsCmd = new RelayCommand(GenerateFactorySettings);
            PrintLabelsCmd = new RelayCommand(PrintLabels);
        }

        // --------------------------------------------------------
        // Properties (bindable)
        // --------------------------------------------------------

        private string connectionStatus;
        public string ConnectionStatus {
            get { return connectionStatus; }
            set { connectionStatus = value; OnPropertyChanged(); }
        }

        private bool isConnected;
        public bool IsConnected {
            get { return isConnected; }
            set { isConnected = value; OnPropertyChanged(); }
        }

        private string date;
        public string Date {
            get { return date; }
            set { date = value; OnPropertyChanged(); }
        }

        private string serial;
        public string Serial {
            get { return serial; }
            set {
                serial = value;
                OnPropertyChanged();
                UpdateHostname();
            }
        }

        private string revision;
        public string Revision {
            get { return revision; }
            set { revision = value; OnPropertyChanged(); }
        }

        private string version;
        public string Version {
            get { return version; }
            set { version = value; OnPropertyChanged(); }
        }

        private string hostname;
        public string Hostname {
            get { return hostname; }
            set { hostname = value; OnPropertyChanged(); }
        }

        private string systemName;
        public string SystemName {
            get { return systemName; }
            set { systemName = value; OnPropertyChanged(); }
        }

        // ---------------- WEB PATH ----------------

        private string webPath;
        public string WebPath {
            get { return webPath; }
            set {
                webPath = value;
                OnPropertyChanged();
                UpdateDerivedUrls();
            }
        }

        // --------- DERIVED (NOT EDITABLE) ---------

        private string manifestUrl;
        public string ManifestUrl {
            get { return manifestUrl; }
            private set { manifestUrl = value; OnPropertyChanged(); }
        }

        private string manifestSigUrl;
        public string ManifestSigUrl {
            get { return manifestSigUrl; }
            private set { manifestSigUrl = value; OnPropertyChanged(); }
        }

        private string updateUrl;
        public string UpdateUrl {
            get { return updateUrl; }
            private set { updateUrl = value; OnPropertyChanged(); }
        }

        private string updateSigUrl;
        public string UpdateSigUrl {
            get { return updateSigUrl; }
            private set { updateSigUrl = value; OnPropertyChanged(); }
        }

        // --------------------------------------------------------
        // Commands
        // --------------------------------------------------------

        public RelayCommand ConnectCmd { get; }
        public RelayCommand ApplyFactorySettingsCmd { get; }
        public RelayCommand GenerateFactorySettingsCmd { get; }
        public RelayCommand PrintLabelsCmd { get; }

        // --------------------------------------------------------
        // Command Methods
        // --------------------------------------------------------

        private async void Connect() {
            try {
                SigCoreDiscovery discovery = new SigCoreDiscovery();
                discovery.Start();
                await Task.Delay(3000);
                discovery.Stop();

                IReadOnlyList<IZeroconfHost> devices = discovery.GetDiscoveredDevices();
                if (devices.Count == 0) {
                    MessageBox.Show("No SigCore UC devices found.");
                    ConnectionStatus = "Disconnected";
                    IsConnected = false;
                    return;
                }

                IZeroconfHost dev = devices[0];
                string ip = dev.IPAddress;

                _system ??= new SigCoreSystem();

                bool ok = await _system.ConnectAsync(ip, 7020);
                if (!ok) {
                    MessageBox.Show("Connection failed for: " + ip);
                    ConnectionStatus = "Disconnected";
                    IsConnected = false;
                    return;
                }

                ConnectionStatus = ip;
                IsConnected = true;

                HardwareManager.Config cfg = await _system.GetGlobalConfigAsync();

                Serial = cfg.SystemSerialNumber ?? "";
                Revision = cfg.HardwareVersion ?? "";
                Version = cfg.SoftwareVersion ?? "";
                Hostname = cfg.Hostname ?? "";
                SystemName = cfg.SystemName ?? "";

            } catch (Exception ex) {
                MessageBox.Show("Error during discovery or connection:\n" + ex.Message);
                ConnectionStatus = "Disconnected";
                IsConnected = false;
            }
        }

        private async void ApplyFactorySettings() {
            if (_system == null || !_system.IsConnected) {
                MessageBox.Show("Not connected to a SigCore device.");
                return;
            }

            try {
                await _system.FactoryReset(
                    Serial,
                    Revision,
                    Version,
                    Hostname,
                    SystemName,
                    ManifestUrl,
                    ManifestSigUrl,
                    UpdateUrl,
                    UpdateSigUrl
                );

                MessageBox.Show("Factory settings applied.");
            } catch (Exception ex) {
                MessageBox.Show("Error applying factory settings:\n" + ex.Message);
            }
        }

        private void GenerateFactorySettings() {
            FactoryAppData appData = FactoryAppData.Load();

            int nextSerialNum = appData.LastSerial + 1;
            appData.LastSerial = nextSerialNum;

            Serial = $"SCUC-{nextSerialNum:000000}";
            Hostname = $"scuc-{nextSerialNum:000000}";
            Revision = appData.LastRev ?? "";
            Version = appData.LastVer ?? "";
            Date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            appData.Save();
        }

        private void PrintLabels() {
            MessageBox.Show("PrintLabels");
        }

        // --------------------------------------------------------
        // Helpers
        // --------------------------------------------------------

        private void UpdateHostname() {
            if (!string.IsNullOrWhiteSpace(Serial))
                Hostname = $"scuc-{Serial}".ToLower();
        }

        private void UpdateDerivedUrls() {
            if (string.IsNullOrWhiteSpace(WebPath))
                return;

            string basePath = WebPath.TrimEnd('/');

            ManifestUrl = $"{basePath}/manifest.json";
            ManifestSigUrl = $"{basePath}/manifest.sig";
            UpdateUrl = $"{basePath}/update.scu";
            UpdateSigUrl = $"{basePath}/update.scu.sig";
        }
    }
}
