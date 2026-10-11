using ControlPanel;
using Newtonsoft.Json.Linq;
using SigCoreCommon;
using SigCoreCommon.Payloads;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace FactorySettings {

    internal class MainWindowVM : ViewModelBase {
        private readonly SigCoreSystem _system;
        private string _targetHost = "";
        private bool _rebootPending;
        private bool _rebootSawDisconnect;

        private const string LiveWebPath = "https://sigcoreuc.com/update/revB";

        // --------------------------------------------------------
        // Constructor
        // --------------------------------------------------------
        public MainWindowVM() {

            Date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            SystemName = "SigCore UC";

            // Factory Settings always programs the live Rev B update path.
            WebPath = LiveWebPath;

            ConnectionStatus = "Disconnected";
            IsConnected = false;

            _system = new SigCoreSystem();
            _system.ConnectionStateChanged += OnConnectionStateChanged;

            ConnectCmd = new RelayCommand(() => ConnectAsync());
            ApplyFactorySettingsCmd = new RelayCommand(ApplyFactorySettings);
            GenerateFactorySettingsCmd = new RelayCommand(GenerateFactorySettings);
            PrintLabelsCmd = new RelayCommand(PrintLabels);
            ClearLabelsCmd = new RelayCommand(ClearLabels);
            RebootCmd = new RelayCommand(Reboot);

            RefreshPendingLabelCount();
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

        private string unitNumber = "";
        public string UnitNumber {
            get { return unitNumber; }
            set {
                string digits = new string((value ?? "").Where(char.IsDigit).Take(6).ToArray());

                if (unitNumber == digits)
                    return;

                unitNumber = digits;
                OnPropertyChanged();
                UpdateIdentityFromUnitNumber();
            }
        }

        private string serial = "";
        public string Serial {
            get { return serial; }
            private set { serial = value; OnPropertyChanged(); }
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

        private string hostname = "";
        public string Hostname {
            get { return hostname; }
            private set { hostname = value; OnPropertyChanged(); }
        }

        private string systemName;
        public string SystemName {
            get { return systemName; }
            set { systemName = value; OnPropertyChanged(); }
        }

        private string macAddress = "";
        public string MacAddress {
            get { return macAddress; }
            private set { macAddress = value; OnPropertyChanged(); }
        }

        private int pendingLabelCount;
        public int PendingLabelCount {
            get { return pendingLabelCount; }
            private set { pendingLabelCount = value; OnPropertyChanged(); }
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
        public RelayCommand ClearLabelsCmd { get; }
        public RelayCommand RebootCmd { get; }

        // --------------------------------------------------------
        // Command Methods
        // --------------------------------------------------------

        private async Task ConnectAsync() {
            try {
                SigCoreDiscovery discovery = new SigCoreDiscovery();
                IReadOnlyList<SigCoreDevice> devices = await discovery.DiscoverAsync();

                SelectSigCoreDlg dlg = new SelectSigCoreDlg(devices);
                dlg.Owner = Application.Current.MainWindow;

                if (dlg.ShowDialog() != true)
                    return;

                string host = dlg.SelectedHost;

                if (string.IsNullOrWhiteSpace(host))
                    return;

                _targetHost = host.Trim();

                // Switching targets is deliberate. SigCoreSystem owns all reconnect
                // behavior after StartAsync() is called.
                _system.Stop();

                ConnectionStatus = $"Connecting to {_targetHost}...";
                IsConnected = false;

                _ = StartManagedConnectionAsync(_targetHost);

            } catch (Exception ex) {
                MessageBox.Show(
                    "Error during discovery or connection:\n" + ex.Message,
                    "Connection Error");

                ConnectionStatus = "Disconnected";
                IsConnected = false;
            }
        }

        private async Task StartManagedConnectionAsync(string host) {
            try {
                await _system.StartAsync(host, 7020);

                // StartAsync returns after handshake + subscription complete.
                await LoadConnectedSettingsAsync();

            } catch (OperationCanceledException) {
                // Expected when the operator deliberately switches targets.
            } catch (Exception ex) {
                Application.Current.Dispatcher.Invoke(() => {
                    ConnectionStatus = "Connection Fault";
                    IsConnected = false;

                    MessageBox.Show(
                        "Connection failed:\n" + ex.Message,
                        "Connection Error");
                });
            }
        }

        private async Task LoadConnectedSettingsAsync() {
            try {
                SystemConfigPayload cfg = await _system.GetGlobalConfigAsync();
                NetworkSettingsPayload netSet = await _system.GetNetworkConfigAsync();
                JObject status = await _system.GetStatusAsync();

                string connectedUnitNumber = ExtractUnitNumber(cfg.SystemSerialNumber);
                string connectedMac = await ResolveMacAddressAsync(_targetHost);

                Application.Current.Dispatcher.Invoke(() => {
                    unitNumber = connectedUnitNumber;
                    OnPropertyChanged(nameof(UnitNumber));

                    // Display what the unit actually reports. Once Unit Number is
                    // edited, both fields are rebuilt from that one source of truth.
                    Serial = cfg.SystemSerialNumber ?? "";
                    Hostname = netSet.Hostname ?? "";
                    Revision = cfg.HardwareVersion ?? "";
                    Version = cfg.SoftwareVersion ?? "";
                    SystemName = cfg.SystemName ?? "";
                    MacAddress = connectedMac;
                    WebPath = LiveWebPath;
                });

            } catch (Exception ex) {
                Application.Current.Dispatcher.Invoke(() => {
                    MessageBox.Show(
                        "Connected, but factory settings could not be read:\n" + ex.Message,
                        "Factory Settings");
                });
            }
        }

        private void OnConnectionStateChanged(SigCoreConnectionState state) {
            Application.Current.Dispatcher.Invoke(() => {
                switch (state) {
                    case SigCoreConnectionState.Connecting:
                        ConnectionStatus = string.IsNullOrWhiteSpace(_targetHost)
                            ? "Connecting..."
                            : $"Connecting to {_targetHost}...";
                        IsConnected = false;
                        break;

                    case SigCoreConnectionState.Handshaking:
                        ConnectionStatus = "Handshaking...";
                        IsConnected = false;
                        break;

                    case SigCoreConnectionState.Subscribing:
                        ConnectionStatus = "Subscribing...";
                        IsConnected = false;
                        break;

                    case SigCoreConnectionState.Connected:
                        ConnectionStatus = string.IsNullOrWhiteSpace(_targetHost)
                            ? "Connected"
                            : $"Connected: {_targetHost}";
                        IsConnected = true;

                        if (_rebootPending && _rebootSawDisconnect)
                            _ = CompleteRebootFeedbackAsync();
                        break;

                    case SigCoreConnectionState.Reconnecting:
                        if (_rebootPending)
                            _rebootSawDisconnect = true;

                        ConnectionStatus = string.IsNullOrWhiteSpace(_targetHost)
                            ? "Reconnecting..."
                            : $"Reconnecting to {_targetHost}...";
                        IsConnected = false;
                        break;

                    case SigCoreConnectionState.Faulted:
                        if (_rebootPending)
                            _rebootSawDisconnect = true;

                        ConnectionStatus = "Connection Fault";
                        IsConnected = false;
                        break;

                    default:
                        if (_rebootPending)
                            _rebootSawDisconnect = true;

                        ConnectionStatus = "Disconnected";
                        IsConnected = false;
                        break;
                }
            });
        }

        private async void ApplyFactorySettings() {
            if (!_system.IsConnected) {
                MessageBox.Show("Not connected to a SigCore device.");
                return;
            }

            if (!TryGetUnitNumber(out int unit)) {
                MessageBox.Show(
                    "Enter a valid Unit Number from 0 through 999999.",
                    "Factory Settings",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // Rebuild immediately before applying so identity can never drift.
            SetIdentity(unit);

            SystemConfigPayload beforeCfg = null;
            NetworkSettingsPayload beforeNet = null;

            try {
                beforeCfg = await _system.GetGlobalConfigAsync();
                beforeNet = await _system.GetNetworkConfigAsync();
            } catch {
                // The apply can still proceed. The final readback is authoritative.
            }

            MessageBoxResult confirm = MessageBox.Show(
                $"Apply Factory Settings to {Serial}?\n\n" +
                "This will replace the unit identity and wipe channel configuration.",
                "Apply Factory Settings",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            try {
                JObject result = await _system.FactoryReset(
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

                bool success = result.Value<bool>("success");
                string apiMessage = result.Value<string>("message") ?? "";

                SystemConfigPayload afterCfg = await _system.GetGlobalConfigAsync();
                NetworkSettingsPayload afterNet = await _system.GetNetworkConfigAsync();
                JObject afterStatus = await _system.GetStatusAsync();
                string afterMac = await ResolveMacAddressAsync(_targetHost);

                Application.Current.Dispatcher.Invoke(() => MacAddress = afterMac);

                string feedback = BuildApplyFeedback(
                    success,
                    apiMessage,
                    result,
                    beforeCfg,
                    beforeNet,
                    afterCfg,
                    afterNet,
                    afterStatus);

                await LoadConnectedSettingsAsync();

                if (success) {
                    if (string.IsNullOrWhiteSpace(MacAddress)) {
                        feedback += "\n\nLABEL NOT SAVED: MAC address could not be determined.";
                    } else {
                        SaveCurrentLabel();
                        feedback += $"\n\nLabel saved. Pending labels: {PendingLabelCount}.";
                    }
                }

                MessageBox.Show(
                    feedback,
                    success ? "Factory Settings" : "Factory Settings Failed",
                    MessageBoxButton.OK,
                    success ? MessageBoxImage.Information : MessageBoxImage.Error);

            } catch (Exception ex) {
                MessageBox.Show(
                    "Error applying Factory Settings:\n" + ex.Message,
                    "Factory Settings Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void GenerateFactorySettings() {
            FactoryAppData appData = FactoryAppData.Load();

            int nextSerialNum = appData.LastSerial + 1;
            if (nextSerialNum > 999999) {
                MessageBox.Show(
                    "The next Unit Number is outside the six-digit range.",
                    "Factory Settings",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            appData.LastSerial = nextSerialNum;

            UnitNumber = nextSerialNum.ToString();
            Revision = appData.LastRev ?? "";
            Version = appData.LastVer ?? "";
            WebPath = LiveWebPath;
            Date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            appData.Save();
        }

        private async void Reboot() {
            if (!_system.IsConnected) {
                MessageBox.Show("Not connected to a SigCore device.");
                return;
            }

            MessageBoxResult confirm = MessageBox.Show(
                $"Reboot {Serial}?",
                "Reboot SigCore",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            try {
                _rebootPending = true;
                _rebootSawDisconnect = false;
                ConnectionStatus = "Sending reboot command...";

                await _system.RestartAsync();

                ConnectionStatus = "Reboot command sent. Waiting for unit to restart...";
            } catch (Exception ex) {
                _rebootPending = false;
                _rebootSawDisconnect = false;

                MessageBox.Show(
                    "Reboot command failed:\n" + ex.Message,
                    "Reboot Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task CompleteRebootFeedbackAsync() {
            if (!_rebootPending)
                return;

            _rebootPending = false;

            try {
                SystemConfigPayload cfg = await _system.GetGlobalConfigAsync();
                NetworkSettingsPayload net = await _system.GetNetworkConfigAsync();
                await _system.GetStatusAsync();

                await LoadConnectedSettingsAsync();

                string text =
                    "The unit disconnected, rebooted, and returned to the API.\n\n" +
                    $"Serial Number: {cfg.SystemSerialNumber}\n" +
                    $"Hostname: {net.Hostname}\n" +
                    $"Revision: {cfg.HardwareVersion}\n" +
                    $"Version: {cfg.SoftwareVersion}";

                MessageBox.Show(
                    text,
                    "Reboot Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            } catch (Exception ex) {
                MessageBox.Show(
                    "The unit reconnected, but its status could not be read:\n" + ex.Message,
                    "Reboot",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            } finally {
                _rebootSawDisconnect = false;
            }
        }

        private void PrintLabels() {
            FactoryAppData appData = FactoryAppData.Load();
            var labels = appData.PendingLabels ?? new List<FactoryLabelRecord>();

            if (labels.Count == 0) {
                MessageBox.Show(
                    "There are no saved labels. Apply Factory Settings to each unit first.",
                    "Create Label Sheet",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            try {
                string path = LabelDocument.CreateAndOpen(labels);

                MessageBox.Show(
                    $"Created {labels.Count} label(s).\n\n{path}",
                    "Label Sheet Created",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            } catch (Exception ex) {
                MessageBox.Show(
                    "Could not create the Word label sheet:\n" + ex.Message,
                    "Create Label Sheet Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ClearLabels() {
            FactoryAppData appData = FactoryAppData.Load();
            int count = appData.PendingLabels?.Count ?? 0;

            if (count == 0)
                return;

            MessageBoxResult answer = MessageBox.Show(
                $"Clear all {count} saved label(s)?",
                "Clear Saved Labels",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (answer != MessageBoxResult.Yes)
                return;

            appData.PendingLabels = new List<FactoryLabelRecord>();
            appData.Save();
            RefreshPendingLabelCount();
        }

        private void SaveCurrentLabel() {
            FactoryAppData appData = FactoryAppData.Load();
            appData.PendingLabels ??= new List<FactoryLabelRecord>();

            string localUrl = string.IsNullOrWhiteSpace(Hostname)
                ? ""
                : $"http://{Hostname}/";

            FactoryLabelRecord existing = appData.PendingLabels
                .FirstOrDefault(x => string.Equals(
                    x.SerialNumber,
                    Serial,
                    StringComparison.OrdinalIgnoreCase));

            if (existing == null) {
                existing = new FactoryLabelRecord();
                appData.PendingLabels.Add(existing);
            }

            existing.SerialNumber = Serial ?? "";
            existing.MacAddress = NormalizeMacAddress(MacAddress);
            existing.Revision = Revision ?? "";
            existing.LocalUrl = localUrl;

            appData.Save();
            RefreshPendingLabelCount();
        }

        private void RefreshPendingLabelCount() {
            try {
                FactoryAppData appData = FactoryAppData.Load();
                PendingLabelCount = appData.PendingLabels?.Count ?? 0;
            } catch {
                PendingLabelCount = 0;
            }
        }

        // --------------------------------------------------------
        // Helpers
        // --------------------------------------------------------

        private bool TryGetUnitNumber(out int unit) {
            if (int.TryParse(UnitNumber, out unit) && unit >= 0 && unit <= 999999)
                return true;

            unit = 0;
            return false;
        }

        private void UpdateIdentityFromUnitNumber() {
            if (TryGetUnitNumber(out int unit))
                SetIdentity(unit);
            else {
                Serial = "";
                Hostname = "";
            }
        }

        private void SetIdentity(int unit) {
            string identity = $"SCUC-{unit:000000}";
            Serial = identity;
            Hostname = identity;
        }

        private string ExtractUnitNumber(string serialNumber) {
            if (string.IsNullOrWhiteSpace(serialNumber))
                return "";

            string text = serialNumber.Trim();
            const string prefix = "SCUC-";

            if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                text = text.Substring(prefix.Length);

            string digits = new string(text.Where(char.IsDigit).Take(6).ToArray());

            if (!int.TryParse(digits, out int unit) || unit < 0 || unit > 999999)
                return "";

            return unit.ToString();
        }

        private string BuildApplyFeedback(
            bool success,
            string apiMessage,
            JObject result,
            SystemConfigPayload beforeCfg,
            NetworkSettingsPayload beforeNet,
            SystemConfigPayload afterCfg,
            NetworkSettingsPayload afterNet,
            JObject afterStatus) {

            var lines = new List<string>();

            if (!string.IsNullOrWhiteSpace(apiMessage))
                lines.Add("API: " + apiMessage.Trim());

            lines.Add("");
            lines.Add(success
                ? "API verification: passed"
                : "API verification: failed");

            JArray failures = result?["details"]?["verification"]?["failures"] as JArray;
            if (failures != null && failures.Count > 0) {
                lines.Add("");
                lines.Add("Verification failures:");
                foreach (JToken failure in failures)
                    lines.Add("- " + (failure?.ToString() ?? ""));
            }

            lines.Add("");
            lines.Add("Current values reported by the unit:");
            lines.Add($"Serial Number: {afterCfg?.SystemSerialNumber ?? ""}");
            lines.Add($"Hostname: {afterNet?.Hostname ?? ""}");
            lines.Add($"Revision: {afterCfg?.HardwareVersion ?? ""}");
            lines.Add($"Version: {afterCfg?.SoftwareVersion ?? ""}");
            lines.Add($"System Name: {afterCfg?.SystemName ?? ""}");

            string reportedWebPath = GetHostWebPath(afterStatus);
            if (!string.IsNullOrWhiteSpace(reportedWebPath))
                lines.Add($"Web Path: {reportedWebPath}");

            var changes = new List<string>();
            AddChange(changes, "Serial Number", beforeCfg?.SystemSerialNumber, afterCfg?.SystemSerialNumber);
            AddChange(changes, "Hostname", beforeNet?.Hostname, afterNet?.Hostname);
            AddChange(changes, "Revision", beforeCfg?.HardwareVersion, afterCfg?.HardwareVersion);
            AddChange(changes, "Version", beforeCfg?.SoftwareVersion, afterCfg?.SoftwareVersion);
            AddChange(changes, "System Name", beforeCfg?.SystemName, afterCfg?.SystemName);

            if (changes.Count > 0) {
                lines.Add("");
                lines.Add("Changed:");
                lines.AddRange(changes);
            } else if (beforeCfg != null || beforeNet != null) {
                lines.Add("");
                lines.Add("Identity/settings shown above were already at these values.");
            }

            return string.Join("\n", lines);
        }

        private void AddChange(List<string> changes, string label, string before, string after) {
            string oldValue = before ?? "";
            string newValue = after ?? "";

            if (!string.Equals(oldValue, newValue, StringComparison.Ordinal))
                changes.Add($"{label}: {oldValue} -> {newValue}");
        }

        private async Task<string> ResolveMacAddressAsync(string host) {
            try {
                string ip = await ResolveIpv4AddressAsync(host);
                if (string.IsNullOrWhiteSpace(ip))
                    return "";

                // Touch the unit first so Windows has a fresh ARP entry.
                try {
                    using Ping ping = new Ping();
                    await ping.SendPingAsync(ip, 1000);
                } catch {
                    // The API connection itself may already have populated ARP.
                }

                ProcessStartInfo psi = new ProcessStartInfo {
                    FileName = "arp.exe",
                    Arguments = $"-a {ip}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using Process process = new Process { StartInfo = psi };
                process.Start();
                string output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                Match match = Regex.Match(
                    output,
                    @"(?i)\b" + Regex.Escape(ip) + @"\s+([0-9a-f]{2}(?:[-:][0-9a-f]{2}){5})\b");

                return match.Success
                    ? NormalizeMacAddress(match.Groups[1].Value)
                    : "";

            } catch {
                return "";
            }
        }

        private async Task<string> ResolveIpv4AddressAsync(string host) {
            if (string.IsNullOrWhiteSpace(host))
                return "";

            string value = host.Trim();

            if (IPAddress.TryParse(value, out IPAddress parsed) &&
                parsed.AddressFamily == AddressFamily.InterNetwork)
                return parsed.ToString();

            IPAddress[] addresses = await Dns.GetHostAddressesAsync(value);
            IPAddress ipv4 = addresses.FirstOrDefault(
                a => a.AddressFamily == AddressFamily.InterNetwork);

            return ipv4?.ToString() ?? "";
        }

        private string NormalizeMacAddress(string value) {
            if (string.IsNullOrWhiteSpace(value))
                return "";

            string hex = new string(value.Where(Uri.IsHexDigit).ToArray()).ToUpperInvariant();
            if (hex.Length != 12)
                return value.Trim().ToUpperInvariant();

            return string.Join(":", Enumerable.Range(0, 6)
                .Select(i => hex.Substring(i * 2, 2)));
        }

        private string GetHostWebPath(JObject status) {
            JObject update = status?["update"] as JObject;

            if (update == null)
                return "";

            string[] candidates = {
                update.Value<string>("manifest_url"),
                update.Value<string>("manifest_sig_url"),
                update.Value<string>("url"),
                update.Value<string>("signature")
            };

            foreach (string candidate in candidates) {
                string basePath = GetParentUrl(candidate);
                if (!string.IsNullOrWhiteSpace(basePath))
                    return basePath;
            }

            return "";
        }

        private string GetParentUrl(string url) {
            if (string.IsNullOrWhiteSpace(url))
                return "";

            string trimmed = url.Trim().TrimEnd('/');
            int slash = trimmed.LastIndexOf('/');

            if (slash <= 0)
                return "";

            return trimmed.Substring(0, slash);
        }

        private void UpdateDerivedUrls() {
            if (string.IsNullOrWhiteSpace(WebPath)) {
                ManifestUrl = "";
                ManifestSigUrl = "";
                UpdateUrl = "";
                UpdateSigUrl = "";
                return;
            }

            string basePath = WebPath.TrimEnd('/');

            ManifestUrl = $"{basePath}/manifest.json";
            ManifestSigUrl = $"{basePath}/manifest.sig";
            UpdateUrl = $"{basePath}/update.scu";
            UpdateSigUrl = $"{basePath}/update.scu.sig";
        }
    }
}
