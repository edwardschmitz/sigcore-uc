using ControlPanel.Dialogs;
using ControlPanel.IOChannelHandlers;
using ControlPanel.Parts;
using Iot.Device.Bno055;
using Newtonsoft.Json.Linq;
using SigCoreCommon;
using SigCoreUC;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Zeroconf;

namespace ControlPanel {
    public class MainWindowVM : ViewModelBase, IDimmer {
        private SigCoreSystem system;

        // ----------------------------------------------------
        // Basic Info
        // ----------------------------------------------------
        private string _name = "SigCore UC";
        public string Name {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        private string _part = "SigCore-UC";
        public string Part {
            get => _part;
            set { _part = value; OnPropertyChanged(); }
        }

        private string _serialNum = "120023";
        public string SerialNum {
            get => _serialNum;
            set { _serialNum = value; OnPropertyChanged(); }
        }

        private string rev = "revA";
        public string Rev {
            get => rev;
            set {
                rev = value;
                OnPropertyChanged();
            }
        }
        private string ver = "1.0.0";
        public string Ver {
            get => ver;
            set {
                ver = value;
                OnPropertyChanged();
            }
        }
        private string ipAddress = "xxx.xxx.xxx.xxx";
        public string IPAddress {
            get => ipAddress;
            set {
                ipAddress = value;
                OnPropertyChanged();
            }
        }

        public void SetDim(bool dim) {
            if (dim) {
                WindowVis = Visibility.Visible;
            } else {
                WindowVis = Visibility.Hidden;
            }
        }

        private Visibility windowVis = Visibility.Visible;
        public Visibility WindowVis {
            get => windowVis;
            set {
                windowVis = value;
                OnPropertyChanged();
            }
        }
        private bool isLogging = false;
        public bool IsLogging {
            get => isLogging;
            set {
                isLogging = value;
                _ = SetLoggingEnabled(value);
                OnPropertyChanged();
            }
        }

        private async Task SetLoggingEnabled(bool value) {
            MYSQL_LOGGER.DataLoggerConfig config = await system.GetLoggingConfigAsync();
            config.Enabled = value;
            system.SetLoggingConfig(config);
        }

        private string logStatus = string.Empty;
        public string LogStatus {
            get => logStatus;
            set {
                logStatus = value; 
                OnPropertyChanged();
            }
        }

        private bool showPIDs = false;
        public bool ShowPIDs {
            get => showPIDs;
            set {
                showPIDs = value;
                OnPropertyChanged();
                UserPrefs prefs = UserPrefs.Load();
                prefs.PidViewOpen = value;
                prefs.Save();
            }
        }

        public ObservableCollection<RelayChannel> Relays { get; }
        public ObservableCollection<DigitalInChannel> DigitalInputs { get; }
        public ObservableCollection<AnalogInChannel> AnalogInputs { get; }
        public ObservableCollection<AnalogOutChannel> AnalogOutputs { get; }
        public ObservableCollection<PidControllerChannel> PidControllers { get; }

        // ----------------------------------------------------
        // Commands
        // ----------------------------------------------------
        public ICommand ResetCmd { get; }
        public ICommand SettingsCmd { get; }
        public ICommand LogConfigCmd { get; }
        public ICommand ConnectCmd { get; }
        public ICommand RestartCmd { get; }
        public ICommand GetStatusCmd { get; }

        public MainWindowVM(SigCoreCommon.SigCoreSystem system) {
            this.system = system;
            system.AnalogInChanged += OnAnalogInChanged;
            system.DigitalInChanged += OnDigitalInChanged;
            system.RelayChanged += OnRelayChanged;
            system.AnalogOutChanged += OnAnalogOutChanged;
            system.LoggerStatusChanged += OnLoggerStatusChanged;
            //system.PIDCurValChanged += OnPIDChanged;

            Relays = new ObservableCollection<RelayChannel> {
                new RelayChannel(0, "", system, this),
                new RelayChannel(1, "", system, this),
                new RelayChannel(2, "", system, this),
                new RelayChannel(3, "", system, this),
                new RelayChannel(4, "", system, this),
                new RelayChannel(5, "", system, this),
                new RelayChannel(6, "", system, this),
                new RelayChannel(7, "", system, this),
            };

            DigitalInputs = new ObservableCollection<DigitalInChannel> {
                new DigitalInChannel(0, "DI1", system, this),
                new DigitalInChannel(1, "DI2", system, this),
                new DigitalInChannel(2, "DI3", system, this),
                new DigitalInChannel(3, "DI4", system, this),
                new DigitalInChannel(4, "DI5", system, this),
                new DigitalInChannel(5, "DI6", system, this),
                new DigitalInChannel(6, "DI7", system, this),
                new DigitalInChannel(7, "DI8", system, this),
            };

            AnalogInputs = new ObservableCollection<AnalogInChannel> {
                new AnalogInChannel(0, "AI1", system, this),
                new AnalogInChannel(1, "AI2", system, this),
                new AnalogInChannel(2, "AI3", system, this),
                new AnalogInChannel(3, "AI4", system, this),
            };
            AnalogOutputs = new ObservableCollection<AnalogOutChannel> {
                new AnalogOutChannel(0, "AI1", system, this),
                new AnalogOutChannel(1, "AI2", system, this),
                new AnalogOutChannel(2, "AI3", system, this),
                new AnalogOutChannel(3, "AI4", system, this),
            };
            PidControllers = new ObservableCollection<PidControllerChannel> {
                new PidControllerChannel(0, "PID1", system, this),
                new PidControllerChannel(1, "PID2", system, this),
                new PidControllerChannel(2, "PID3", system, this),
                new PidControllerChannel(3, "PID4", system, this),
            };

            ResetCmd = new RelayCommand(ResetAll);
            SettingsCmd = new RelayCommand(ShowSettings);
            LogConfigCmd = new RelayCommand(ShowLogConfig);
            ConnectCmd = new RelayCommand(DiscoverSigCore_Click);
            RestartCmd = new RelayCommand(RestartCommand);
            GetStatusCmd = new RelayCommand(GetStatusCommand);

            ResetAll();
            SetDim(false);

            UserPrefs prefs = UserPrefs.Load();
            if (prefs.AutoReconnect) {
                _ = Connect(prefs.LastIp);
            } else {
                DiscoverSigCore_Click();
            }
        }

        private void GetStatusCommand() {
            _ = GetStatusAsync();
        }

        private async Task GetStatusAsync() {
            JObject status = await system.GetStatusAsync();
            Clipboard.SetText(status.ToString());
            StatusDlg dlg = new StatusDlg(status);
            dlg.Owner = Application.Current.MainWindow;
            dlg.ShowDialog();
        }

        private void OnLoggerStatusChanged(string status, bool logging) {
            isLogging = logging;
            OnPropertyChanged(nameof(IsLogging));
            LogStatus = status;
        }

        public void InitializeComponents() {
            foreach (RelayChannel relay in Relays) {
                relay.Initialize();
            }
            foreach (DigitalInChannel dIn in DigitalInputs) {
                dIn.Initialize();
            }
            foreach (AnalogInChannel aIn in AnalogInputs) {
                aIn.Initialize();
            }
            foreach (AnalogOutChannel aOut in AnalogOutputs) {
                aOut.Initialize();
            }
            foreach (PidControllerChannel pid in PidControllers) {
                pid.Initialize();
            }
            Initialize();
        }

        private void OnAnalogInChanged(double[] values) {
            for (int i = 0; i < values.Length && i < AnalogInputs.Count; i++) {
                AnalogInputs[i].Value = values[i];
            }
        }

        private void OnDigitalInChanged(bool[] values) {
            for (int i = 0; i < values.Length && i < DigitalInputs.Count; i++) {
                DigitalInputs[i].Value = values[i];
            }
        }

        private void OnRelayChanged(uint channel, bool state) {
            if (channel < Relays.Count)
                Relays[(int)channel].SetValueFromServer(state);
        }

        private void OnAnalogOutChanged(uint channel, double setVal, bool auto) {
            AnalogOutputs[(int)channel].SetValueFromServer(setVal);
            AnalogOutputs[(int)channel].IsEnabled = !auto;
        }

        private void ResetAll() {
            for (uint i = 0; i < Relays.Count; i++)
                system.SetRelay(i, false);
            for (uint i = 0; i < AnalogOutputs.Count; i++) {
                system.SetAOutValue(i, 0);
            }
            for (uint i = 0; i < PidControllers.Count; i++) {
                system.SetPIDAutoAsync(i, false);
            }

            OnPropertyChanged(string.Empty);
        }

        private void ShowLogConfig() {
            _ = ShowLogConfigAsync();
        }
        private async Task ShowLogConfigAsync() {
            SetDim(true);
            MYSQL_LOGGER.DataLoggerConfig config;

            config = await system.GetLoggingConfigAsync();

            LoggerConfigDlgVM vm = new LoggerConfigDlgVM(config);
            LoggerConfigDlg dlg = new LoggerConfigDlg();
            dlg.DataContext = vm;

            dlg.Owner = Application.Current.MainWindow;
            bool? result = dlg.ShowDialog();
            if (result == true) {
                system.SetLoggingConfig(vm.Config);
            }
            SetDim(false);
        }
        private void ShowSettings() {
            _ = ShowSettingAsync();
        }
        private async Task ShowSettingAsync() {
            SetDim(true);
            HardwareManager.Config config;

            config = await system.GetGlobalConfigAsync();

            GlobalConfigDlgVM vm = new GlobalConfigDlgVM(config);
            GlobalConfigDlg dlg = new GlobalConfigDlg();
            dlg.DataContext = vm;

            dlg.Owner = Application.Current.MainWindow;
            bool? result = dlg.ShowDialog();
            if (result == true) {
                await system.SetGlobalConfigAsync(vm.Config);
                ShowConfig(vm.Config);
            }
            SetDim(false);
        }

        public void Initialize() {
            _ = UpdateFromConfig();
        }
        public async Task UpdateFromConfig() {
            HardwareManager.Config config = await system.GetGlobalConfigAsync();
            ShowConfig(config);
        }

        private void ShowConfig(HardwareManager.Config config) {
            Name = config.SystemName;
            Rev = config.HardwareVersion;
            Ver = config.SoftwareVersion;
            SerialNum = config.SystemSerialNumber;
            if (config.DhcpEnabled) {
                IPAddress = $"DHCP ({config.IpAddress})";
            } else {
                IPAddress = config.IpAddress;
            }
        }
        private void RestartCommand() {
            _ = RestartAsync();
        }

        private async Task RestartAsync() {
            MessageBoxResult answer = MessageBox.Show(
                "Restart the SigCore UC Server?",
                "System Restart Request",
                MessageBoxButton.YesNo
            );

            if (answer != MessageBoxResult.Yes) {
                return;
            }

            SetDim(true);

            try {
                // Tell the device to restart
                system.Restart();

                // Wait for restart window
                DateTime deadline = DateTime.Now.AddSeconds(60);
                bool connected = false;

                // Close old connection
                if (system.IsConnected) {
                    system.Close();
                }

                // Start retry loop
                while (DateTime.Now < deadline) {
                    await Task.Delay(1500);

                    try {
                        connected = await system.ConnectAsync(IPAddress);
                        if (connected) {
                            InitializeComponents();
                            system.Subscribe();
                            break;
                        }
                    } catch {
                        // ignore and retry
                    }
                }

                if (!connected) {
                    MessageBox.Show(
                        "Unable to reconnect to the SigCore UC server after restart.",
                        "Reconnect Timeout"
                    );
                }

            } finally {
                SetDim(false);
            }
        }

        private async Task Connect(string ipAddress) {
            bool result = await system.ConnectAsync(ipAddress);
            if (result) {
                InitializeComponents();
                system.Subscribe();
            } else {
                MessageBox.Show($"Connect Failed {ipAddress}", "System Connection Error");
            }

        }
        private void DiscoverSigCore_Click() {
            _ = DiscoverSigCore_ClickAsync();
        }
        private async Task DiscoverSigCore_ClickAsync() {
            try {
                SetDim(true);
                SigCoreDiscovery discovery = new SigCoreDiscovery();

                discovery.Start();
                await Task.Delay(3000);
                discovery.Stop();

                IReadOnlyList<IZeroconfHost> devices = discovery.GetDiscoveredDevices();
                if (devices.Count == 0) {
                    MessageBox.Show("No SigCore UC devices found on the network.", "Discovery");
                } else {
                    IZeroconfHost selected = devices[0];
                    SelectSigCoreDlg dlg = new SelectSigCoreDlg(devices);
                    dlg.Owner = Application.Current.MainWindow;
                    if (dlg.ShowDialog() == true && dlg.SelectedHost != null) {
                        selected = dlg.SelectedHost;

                        if (system.IsConnected) {
                            system.Close();
                        }
                        await Connect(selected.IPAddress);
                    }
                }
            } catch (Exception ex) {
                MessageBox.Show("Connection failed: " + ex.Message, "Error");
            } finally {
                SetDim(false);
            }
        }

        private async Task<bool> ShowReconnectDialogAsync() {
            try {
                SigCoreDiscovery discovery = new SigCoreDiscovery();
                discovery.Start();
                await Task.Delay(3000);
                discovery.Stop();

                IReadOnlyList<IZeroconfHost> devices = discovery.GetDiscoveredDevices();
                if (devices.Count == 0) {
                    MessageBox.Show("No SigCore UC devices found on the network.", "Reconnect");
                    return false;
                }

                SelectSigCoreDlg dlg = new SelectSigCoreDlg(devices);
                dlg.Owner = Application.Current.MainWindow;

                if (dlg.ShowDialog() == true && dlg.SelectedHost != null) {
                    bool result = await system.ConnectAsync(dlg.SelectedHost.IPAddress);
                    if (result) {
                        InitializeComponents();
                        system.Subscribe();
                        return true;
                    }
                }

                return false;
            } catch (Exception ex) {
                MessageBox.Show("Reconnect failed: " + ex.Message, "Error");
                return false;
            }
        }
        public async void HandleSystemResume() {
            try {
                if (system.IsConnected) {
                    system.Close();
                }

                // Allow NICs to come back online
                await Task.Delay(2000);

                bool ok = await system.ConnectAsync(IPAddress);

                if (ok) {
                    InitializeComponents();
                    system.Subscribe();
                    return;
                }

                // If direct reconnect failed, open your Connection Dialog
                await ShowReconnectDialogAsync();
            } catch {
                // Worst case: stay disconnected but stable
            }
        }
    }
    public interface IDimmer {
        void SetDim(bool isDimmed);
    }
}
