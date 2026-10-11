using ControlPanel.Dialogs;
using ControlPanel.IOChannelHandlers;
using ControlPanel.Parts;
using Iot.Device.Bno055;
using Newtonsoft.Json.Linq;
using SigCoreCommon;
using SigCoreCommon.Payloads;
using SigCoreUC;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
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
        private string _targetIp = string.Empty;

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

        private string _serialNum = string.Empty;
        public string SerialNum {
            get => _serialNum;
            set { _serialNum = value; OnPropertyChanged(); }
        }

        // Header metadata stays blank until a device connection is established.
        private string _controlPanelVersion = string.Empty;
        public string ControlPanelVersion {
            get => _controlPanelVersion;
            private set { _controlPanelVersion = value; OnPropertyChanged(); }
        }

        public string CoreVersionDisplay =>
            string.IsNullOrWhiteSpace(Rev) || string.IsNullOrWhiteSpace(Ver)
                ? string.Empty : $"Core: {Rev} / v{Ver}";

        private string rev = string.Empty;
        public string Rev {
            get => rev;
            set {
                rev = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CoreVersionDisplay));
            }
        }
        private string ver = string.Empty;
        public string Ver {
            get => ver;
            set {
                ver = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CoreVersionDisplay));
            }
        }
        private string ipAddress = string.Empty;
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
            LoggerConfigPayload config = await system.GetLoggingConfigAsync();
            config.Enabled = value;
            _ = system.SetLoggingConfigAsync(config);
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
        public ICommand NetworkSettingsCmd { get; }
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
            system.ConnectionStateChanged += OnConnectionStateChanged;
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
            NetworkSettingsCmd = new RelayCommand(ShowNetworkSettings);
            LogConfigCmd = new RelayCommand(ShowLogConfig);
            ConnectCmd = new RelayCommand(DiscoverSigCore_Click);
            RestartCmd = new RelayCommand(RestartCommand);
            GetStatusCmd = new RelayCommand(GetStatusCommand);

            ResetAll();

            UserPrefs prefs = UserPrefs.Load();

            if (prefs.AutoReconnect) {
                _ = AutoConnect(prefs.LastIp);
            } else {
                DiscoverSigCore_Click();
            }

            system.ConnectionWarning += msg => {
                MessageBox.Show(msg, "Connection Warning");
            };

            SetDim(false);
        }

        private Task AutoConnect(string ip) {
            if (string.IsNullOrWhiteSpace(ip)) {
                DiscoverSigCore_Click();
                return Task.CompletedTask;
            }

            return StartManagedConnectionAsync(ip);
        }

        private void GetStatusCommand() {
            _ = GetStatusAsync();
        }

        private async Task GetStatusAsync() {
            JObject status = await system.GetStatusAsync();
            Clipboard.SetText(status.ToString());
            if (status.HasValues) {
                StatusDlg dlg = new StatusDlg(status);
                dlg.Owner = Application.Current.MainWindow;
                dlg.ShowDialog();
            } else {
                MessageBox.Show("No Status Available", "Error");
            }
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
            if (system.IsConnected) {
                for (uint i = 0; i < Relays.Count; i++)
                    _ = system.SetRelayAsync(i, false);

                for (uint i = 0; i < AnalogOutputs.Count; i++) {
                    _ = system.SetAOutValueAsync(i, 0);
                }

                for (uint i = 0; i < PidControllers.Count; i++) {
                    _ = system.SetPIDAutoAsync(i, false);
                }
            }

            OnPropertyChanged(string.Empty);
        }

        private void ShowLogConfig() {
            _ = ShowLogConfigAsync();
        }
        private async Task ShowLogConfigAsync() {
            SetDim(true);
            LoggerConfigPayload config;

            config = await system.GetLoggingConfigAsync();

            LoggerConfigDlgVM vm = new LoggerConfigDlgVM(config);
            LoggerConfigDlg dlg = new LoggerConfigDlg();
            dlg.DataContext = vm;

            dlg.Owner = Application.Current.MainWindow;
            bool? result = dlg.ShowDialog();
            if (result == true) {
                _ = system.SetLoggingConfigAsync(config);
            }
            SetDim(false);
        }
        private void ShowSettings() {
            _ = ShowSystemSettingsAsync();
        }
        private void ShowNetworkSettings() {
            _ = ShowNetworkSettingsAsync();
        }

        private async Task ShowSystemSettingsAsync() {
            SetDim(true);

            SystemConfigPayload config =
                await system.GetGlobalConfigAsync();

            SystemPropertiesVM vm = new SystemPropertiesVM(config);
            SystemPropertiesDlg dlg = new SystemPropertiesDlg {
                DataContext = vm,
                Owner = Application.Current.MainWindow
            };

            bool? result = dlg.ShowDialog();
            if (result == true) {
                await system.SetGlobalConfigAsync(config);
                ShowConfig(config);
            }

            SetDim(false);
        }

        private async Task ShowNetworkSettingsAsync() {
            SetDim(true);

            NetworkSettingsPayload config =
                await system.GetNetworkConfigAsync();

            NetworkSettingsVM vm = new NetworkSettingsVM(config);
            NetworkSettings dlg = new NetworkSettings {
                DataContext = vm,
                Owner = Application.Current.MainWindow
            };

            bool? result = dlg.ShowDialog();
            if (result == true) {
                await system.SetNetworkConfigAsync(vm.Config);
            }

            SetDim(false);
        }


        public void Initialize() {
            _ = UpdateFromConfig();
        }
        public async Task UpdateFromConfig() {
            SystemConfigPayload config = await system.GetGlobalConfigAsync();
            ShowConfig(config);
        }

        private void ShowConfig(SystemConfigPayload config) {
            Name = config.SystemName;
            Rev = config.HardwareVersion;
            Ver = config.SoftwareVersion;
            SerialNum = config.SystemSerialNumber;
        }
        private void RestartCommand() {
            _ = RestartAsync();
        }

        private async Task RestartAsync() {
            MessageBoxResult answer = MessageBox.Show(
                "Restart the SigCore UC Server?",
                "System Restart Request",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (answer != MessageBoxResult.Yes)
                return;

            SetDim(true);

            try {
                await system.RestartAsync();

                MessageBox.Show(
                    "The SigCore UC server is restarting.\n\n" +
                    "The Control Panel will reconnect automatically.",
                    "Server Restarting",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            } finally {
                SetDim(false);
            }
        }

        private async Task StartManagedConnectionAsync(string ipAddress) {
            _targetIp = ipAddress;
            ConnectionStatus = "Connecting";

            try {
                await system.StartAsync(ipAddress);
            } catch (OperationCanceledException) {
                // A deliberate Stop() or a switch to another device cancels
                // the current managed connection attempt.
            } catch (Exception ex) {
                Application.Current.Dispatcher.Invoke(() => {
                    ConnectionStatus = "Connection Fault";
                    MessageBox.Show(
                        "Connection failed: " + ex.Message,
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                });
            }
        }
        private void DiscoverSigCore_Click() {
            _ = DiscoverSigCore_ClickAsync();
        }
        private async Task DiscoverSigCore_ClickAsync() {
            try {
                SetDim(true);

                SigCoreDiscovery discovery = new SigCoreDiscovery();

                IReadOnlyList<SigCoreDevice> devices =
                    await discovery.DiscoverAsync();

                if (devices.Count == 0) {
                    MessageBox.Show("No SigCore UC devices found on the network.", "Discovery");
                    return;
                }

                SelectSigCoreDlg dlg = new SelectSigCoreDlg(devices);
                dlg.Owner = Application.Current.MainWindow;

                if (dlg.ShowDialog() == true && dlg.SelectedDevice != null) {
                    SigCoreDevice selected = dlg.SelectedDevice;

                    if (!selected.Ready || string.IsNullOrWhiteSpace(selected.Address))
                        return;

                    system.Stop();
                    _ = StartManagedConnectionAsync(selected.Address);
                }
            } catch (Exception ex) {
                MessageBox.Show("Connection failed: " + ex.Message, "Error");
            } finally {
                SetDim(false);
            }
        }

        public async void HandleSystemResume() {
            // SigCoreSystem owns reconnect. After resume, allow the network
            // stack to settle; the managed connection loop will recover the
            // existing target without Control Panel creating another loop.
            await Task.Delay(2000);

            if (system.ConnectionState == SigCoreConnectionState.Disconnected &&
                !string.IsNullOrWhiteSpace(_targetIp)) {

                _ = StartManagedConnectionAsync(_targetIp);
            }
        }

        private void OnConnected(string ipAddress) {
            InitializeComponents();
            IPAddress = ipAddress;
            if (string.IsNullOrEmpty(ControlPanelVersion)) {
                var v = typeof(MainWindowVM).Assembly.GetName().Version;
                if (v != null)
                    ControlPanelVersion = $"Control Panel: v{v.Major}.{v.Minor}.{Math.Max(0, v.Build)}";
            }
            ConnectionStatus = "Connected";
        }
        private void OnConnectionStateChanged(SigCoreConnectionState state) {
            Application.Current.Dispatcher.Invoke(() => {
                switch (state) {
                    case SigCoreConnectionState.Connecting:
                    case SigCoreConnectionState.Handshaking:
                    case SigCoreConnectionState.Subscribing:
                        ConnectionStatus = "Connecting";
                        break;

                    case SigCoreConnectionState.Connected:
                        OnConnected(_targetIp);
                        break;

                    case SigCoreConnectionState.Reconnecting:
                        ConnectionStatus = "Reconnecting";
                        break;

                    case SigCoreConnectionState.Faulted:
                        ConnectionStatus = "Connection Fault";
                        break;

                    case SigCoreConnectionState.Disconnected:
                    default:
                        ConnectionStatus = "Not Connected";
                        break;
                }
            });
        }

        private string _connectionStatus = "Not Connected";
        public string ConnectionStatus {
            get => _connectionStatus;
            set { _connectionStatus = value; OnPropertyChanged(); }
        }

    }
    public interface IDimmer {
        void SetDim(bool isDimmed);
    }
}
