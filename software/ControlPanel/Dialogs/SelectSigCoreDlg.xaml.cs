using SigCoreCommon;
using SigCoreUC;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;

namespace ControlPanel.Dialogs {

    public class DeviceListItem {
        public string DisplayName { get; set; } = "";
        public SigCoreDevice Device { get; set; } = default!;
    }

    public partial class SelectSigCoreDlg : Window {

        public SigCoreDevice? SelectedDevice { get; private set; }

        private readonly List<DeviceListItem> deviceList = new();
        private readonly List<DeviceListItem> arpList = new();

        public SelectSigCoreDlg(IEnumerable<SigCoreDevice> devices) {
            InitializeComponent();

            foreach (SigCoreDevice device in devices) {
                deviceList.Add(new DeviceListItem {
                    Device = device,
                    DisplayName = MakeDisplayName(device)
                });
            }

            RefreshDeviceList();

            UserPrefs prefs = UserPrefs.Load();
            AutoConnect.IsChecked = prefs.AutoReconnect;
            IPAddress.Text = prefs.LastIp;
        }

        private static string MakeDisplayName(SigCoreDevice device) {
            string name = string.IsNullOrWhiteSpace(device.Name)
                ? "SigCore UC"
                : device.Name;

            string address = device.Address ?? "";

            string version = string.IsNullOrWhiteSpace(device.Version)
                ? ""
                : $" v{device.Version}";

            string ready = device.Ready ? "Ready" : "Offline";

            return $"{name} ({address}){version} - {ready}";
        }

        private void Ok_Click(object sender, RoutedEventArgs e) {
            string ipAddress = "";

            if (DeviceList.SelectedItem is DeviceListItem item) {
                SelectedDevice = item.Device;
                ipAddress = item.Device.Address ?? "";
                DialogResult = true;
            } else {
                string typed = IPAddress.Text.Trim();

                if (!string.IsNullOrWhiteSpace(typed)) {
                    SelectedDevice = new SigCoreDevice {
                        Name = $"SigCore UC ({typed})",
                        Id = typed,
                        Address = typed,
                        AdvertisedAddresses = new[] { typed },
                        Version = null,
                        ServicePort = 0,
                        ControlPort = 7020,
                        Ready = true
                    };

                    ipAddress = typed;
                    DialogResult = true;
                }
            }

            UserPrefs prefs = UserPrefs.Load();
            prefs.AutoReconnect = AutoConnect.IsChecked == true;
            prefs.LastIp = ipAddress;
            prefs.Save();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) {
            DialogResult = false;
        }

        private void ScanSubnet_Click(object sender, RoutedEventArgs e) {
            ScanSubnet();
        }

        private List<string> GetArpAddresses() {
            List<string> results = new();

            ProcessStartInfo psi = new() {
                FileName = "arp",
                Arguments = "-a",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            Process? p = Process.Start(psi);
            if (p == null)
                return results;

            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();

            string[] lines = output.Split(
                new[] { '\n', '\r' },
                StringSplitOptions.RemoveEmptyEntries);

            foreach (string line in lines) {
                if (!line.Contains("dynamic") && !line.Contains("static"))
                    continue;

                string[] parts = line.Split(
                    new[] { ' ' },
                    StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length >= 2 && LooksLikeIp(parts[0]))
                    results.Add(parts[0].Trim());
            }

            return results.Distinct().ToList();
        }

        private async Task<bool> CanPingAsync(string ip) {
            try {
                using Ping ping = new();
                PingReply reply = await ping.SendPingAsync(ip, 500);
                return reply.Status == IPStatus.Success;
            } catch {
                return false;
            }
        }

        private async Task<bool> IsSigCoreServerAsync(string ip) {
            try {
                using TcpClient client = new();

                Task connectTask = client.ConnectAsync(ip, 7020);
                Task timeoutTask = Task.Delay(500);

                Task completed = await Task.WhenAny(connectTask, timeoutTask);

                if (completed != connectTask)
                    return false;

                await connectTask;
                return client.Connected;
            } catch {
                return false;
            }
        }

        private async void ScanSubnet() {
            List<string> ips = GetArpAddresses();

            foreach (string ip in ips) {
                if (!await CanPingAsync(ip))
                    continue;

                bool ready = await IsSigCoreServerAsync(ip);

                bool exists =
                    deviceList.Any(i => i.Device.Address == ip) ||
                    arpList.Any(i => i.Device.Address == ip);

                if (exists)
                    continue;

                SigCoreDevice device = new() {
                    Name = $"SigCore UC ({ip})",
                    Id = ip,
                    Address = ip,
                    AdvertisedAddresses = new[] { ip },
                    Version = null,
                    ServicePort = 0,
                    ControlPort = 7020,
                    Ready = ready
                };

                arpList.Add(new DeviceListItem {
                    Device = device,
                    DisplayName = MakeDisplayName(device)
                });
            }

            RefreshDeviceList();
        }

        private void RefreshDeviceList() {
            List<DeviceListItem> all = new();

            all.AddRange(deviceList);
            all.AddRange(arpList);

            DeviceList.ItemsSource = null;
            DeviceList.ItemsSource = all;
        }

        private void DeviceList_MouseDoubleClick(
            object sender,
            System.Windows.Input.MouseButtonEventArgs e) {

            if (DeviceList.SelectedItem is DeviceListItem item) {
                SelectedDevice = item.Device;
                DialogResult = true;
            }
        }

        private void DeviceList_SelectionChanged(
            object sender,
            System.Windows.Controls.SelectionChangedEventArgs e) {

            if (DeviceList.SelectedItem is DeviceListItem item) {
                IPAddress.Text = item.Device.Address ?? "";
                OkButton.IsEnabled = item.Device.Ready;
            } else {
                OkButton.IsEnabled = LooksLikeIp(IPAddress.Text);
            }
        }

        private void IPAddress_TextChanged(
            object sender,
            System.Windows.Controls.TextChangedEventArgs e) {

            if (DeviceList.SelectedItem != null)
                DeviceList.SelectedItem = null;

            OkButton.IsEnabled = LooksLikeIp(IPAddress.Text);
        }

        private static bool LooksLikeIp(string text) {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            string[] parts = text.Trim().Split('.');
            if (parts.Length != 4)
                return false;

            foreach (string part in parts) {
                if (!int.TryParse(part, out int n))
                    return false;

                if (n < 0 || n > 255)
                    return false;
            }

            return true;
        }
    }
}