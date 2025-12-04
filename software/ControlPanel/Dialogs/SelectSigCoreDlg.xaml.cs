using SigCoreUC;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Windows;
using Zeroconf;

namespace ControlPanel.Dialogs {

    // =====================================================================
    // SHIM: Treat an ARP-discovered device like a real IZeroconfHost
    // =====================================================================
    public class SimpleHost : IZeroconfHost {

        public SimpleHost(string ip, string name) {
            IPAddress = ip;
            DisplayName = name;

            // Zeroconf normally reports multiple IPs — we give only one
            IPAddresses = new List<string> { ip };

            // Must match your Zeroconf version EXACTLY:
            // IReadOnlyDictionary<string, IService>
            Services = new Dictionary<string, IService>();
        }

        public string Id => IPAddress;

        public string DisplayName { get; }

        public string IPAddress { get; }

        public IReadOnlyList<string> IPAddresses { get; }

        // EXACT signature your compiler requires
        public IReadOnlyDictionary<string, IService> Services { get; }
    }


    // =====================================================================
    // UNIFIED LIST ITEM
    // =====================================================================
    public class DeviceListItem {
        public string DisplayName { get; set; }
        public IZeroconfHost Host { get; set; }
    }


    // =====================================================================
    // SELECT DIALOG
    // =====================================================================
    public partial class SelectSigCoreDlg : Window {

        public IZeroconfHost SelectedHost { get; private set; }

        private readonly List<DeviceListItem> arpList = new List<DeviceListItem>();

        public SelectSigCoreDlg(IEnumerable<IZeroconfHost> hosts) {
            InitializeComponent();

            List<DeviceListItem> list = new List<DeviceListItem>();

            foreach (IZeroconfHost h in hosts) {
                list.Add(new DeviceListItem {
                    Host = h,
                    DisplayName = $"{h.DisplayName} ({h.IPAddress})"
                });
            }

            DeviceList.ItemsSource = list;

            UserPrefs prefs = UserPrefs.Load();
            AutoConnect.IsChecked = prefs.AutoReconnect;
            IPAddress.Text = prefs.LastIp;
        }


        // -----------------------------------------------------------------
        // BUTTONS
        // -----------------------------------------------------------------
        private void Ok_Click(object sender, RoutedEventArgs e) {
            string ipAddress = "";

            if (DeviceList.SelectedItem is DeviceListItem item) {
                ipAddress = item.Host.IPAddress;
                SelectedHost = item.Host;
                DialogResult = true;
            } else {
                string typed = IPAddress.Text.Trim();

                if (!string.IsNullOrWhiteSpace(typed)) {
                    ipAddress = typed;
                    SelectedHost = new SimpleHost(typed, typed);
                    DialogResult = true;
                }
            }

            // Persist user preferences
            UserPrefs prefs = UserPrefs.Load();
            prefs.AutoReconnect = AutoConnect.IsChecked == true;
            prefs.LastIp = ipAddress;
            prefs.Save();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) {
            DialogResult = false;
        }


        // -----------------------------------------------------------------
        // SUBNET SCAN
        // -----------------------------------------------------------------
        private void ScanSubnet_Click(object sender, RoutedEventArgs e) {
            ScanSubnet();
        }

        private List<string> GetArpAddresses() {
            List<string> results = new List<string>();

            ProcessStartInfo psi = new ProcessStartInfo {
                FileName = "arp",
                Arguments = "-a",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            Process p = Process.Start(psi);
            if (p == null) return results;

            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();

            string[] lines = output.Split(
                new[] { '\n', '\r' },
                StringSplitOptions.RemoveEmptyEntries);

            foreach (string line in lines) {
                if (line.Contains("dynamic") || line.Contains("static")) {
                    string[] parts = line.Split(
                        new[] { ' ' },
                        StringSplitOptions.RemoveEmptyEntries);

                    if (parts.Length >= 2)
                        results.Add(parts[0].Trim());
                }
            }

            return results;
        }


        private bool IsSigCoreServer(string ip) {
            try {
                TcpClient client = new TcpClient();
                IAsyncResult ar = client.BeginConnect(ip, 7020, null, null);
                bool success = ar.AsyncWaitHandle.WaitOne(200);
                if (!success) return false;

                client.EndConnect(ar);
                client.Close();
                return true;

            } catch {
                return false;
            }
        }


        private void ScanSubnet() {
            List<string> ips = GetArpAddresses();

            foreach (string ip in ips) {

                if (!IsSigCoreServer(ip)) continue;

                bool exists =
                    ((IEnumerable<DeviceListItem>)DeviceList.ItemsSource)
                        .Any(i => i.Host.IPAddress == ip) ||
                    arpList.Any(a => a.Host.IPAddress == ip);

                if (!exists) {
                    arpList.Add(new DeviceListItem {
                        Host = new SimpleHost(ip, $"SigCore UC ({ip})"),
                        DisplayName = $"SigCore UC ({ip})"
                    });
                }
            }

            RefreshDeviceList();
        }


        private void RefreshDeviceList() {
            List<DeviceListItem> all = new List<DeviceListItem>();

            foreach (DeviceListItem item in (IEnumerable<DeviceListItem>)DeviceList.ItemsSource)
                all.Add(item);

            foreach (DeviceListItem item in arpList)
                all.Add(item);

            DeviceList.ItemsSource = null;
            DeviceList.ItemsSource = all;
        }


        // -----------------------------------------------------------------
        // DOUBLE CLICK
        // -----------------------------------------------------------------
        private void DeviceList_MouseDoubleClick(
            object sender,
            System.Windows.Input.MouseButtonEventArgs e) {

            if (DeviceList.SelectedItem is DeviceListItem item) {
                SelectedHost = item.Host;
                DialogResult = true;
            }
        }


        private void DeviceList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) {
            if (DeviceList.SelectedItem is DeviceListItem item) {
                IPAddress.Text = item.Host.IPAddress;
                OkButton.IsEnabled = true;
            } else {
                OkButton.IsEnabled = LooksLikeIp(IPAddress.Text);
            }
        }
        private void IPAddress_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) {
            // Clear list selection (Option 1 behavior)
            if (DeviceList.SelectedItem != null)
                DeviceList.SelectedItem = null;

            // Enable OK whenever user has typed something
            OkButton.IsEnabled = !string.IsNullOrWhiteSpace(IPAddress.Text);
        }
        private bool LooksLikeIp(string text) {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            string t = text.Trim();

            string[] parts = t.Split('.');
            if (parts.Length != 4)
                return false;

            foreach (string p in parts) {
                if (!int.TryParse(p, out int n))
                    return false;

                if (n < 0 || n > 255)
                    return false;
            }

            return true;
        }
    }
}
