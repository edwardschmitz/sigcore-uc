using ControlPanel;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Zeroconf;

namespace DeviceDiscovery {
    internal class MainWindowVM : ViewModelBase {
        private const string ServiceType = "_sigcore._tcp.local.";
        private const int SigCoreServerPort = 7020;
        private const int PingTimeoutMs = 1000;
        private const int TcpTimeoutMs = 1000;

        public ObservableCollection<DiscoveredDeviceVM> Devices { get; } =
            new ObservableCollection<DiscoveredDeviceVM>();

        public RelayCommand DiscoverCommand { get; }
        public RelayCommand CloseCommand { get; }

        private bool isBusy;
        public bool IsBusy {
            get => isBusy;
            set {
                if (isBusy == value) {
                    return;
                }

                isBusy = value;
                OnPropertyChanged();
                DiscoverCommand?.RaiseCanExecuteChanged();
                CloseCommand?.RaiseCanExecuteChanged();
            }
        }

        public MainWindowVM() {
            DiscoverCommand = new RelayCommand(async () => await DiscoverAsync(), () => !IsBusy);
            CloseCommand = new RelayCommand(Close, () => !IsBusy);
        }

        private async Task DiscoverAsync() {
            if (IsBusy) {
                return;
            }

            IsBusy = true;

            try {
                Devices.Clear();

                var results = await ZeroconfResolver.ResolveAsync(ServiceType);

                foreach (var host in results) {
                    Devices.Add(new DiscoveredDeviceVM {
                        DeviceName = host.DisplayName,
                        Address = host.IPAddress
                    });
                }

                string report = await BuildReportAsync(results);
                Clipboard.SetText(report);

                MessageBox.Show(
                    "Your discovery information is on the clipboard.\r\n\r\n" +
                    "Please paste it into an email to send to the developer.",
                    "Discovery Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            } catch (Exception ex) {
                MessageBox.Show(
                    ex.ToString(),
                    "Discovery Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            } finally {
                IsBusy = false;
            }
        }

        private async Task<string> BuildReportAsync(IReadOnlyList<IZeroconfHost> hosts) {
            var sb = new StringBuilder();

            sb.AppendLine("SigCore UC Discovery Report");
            sb.AppendLine("===========================");
            sb.AppendLine();

            sb.AppendLine("Report Time");
            sb.AppendLine("-----------");
            sb.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine();

            sb.AppendLine("Machine Information");
            sb.AppendLine("-------------------");
            sb.AppendLine($"Machine Name: {Environment.MachineName}");
            sb.AppendLine($"User Name: {Environment.UserName}");
            sb.AppendLine($"OS Version: {Environment.OSVersion}");
            sb.AppendLine($".NET Version: {Environment.Version}");
            sb.AppendLine($"64-bit OS: {Environment.Is64BitOperatingSystem}");
            sb.AppendLine($"64-bit Process: {Environment.Is64BitProcess}");
            sb.AppendLine();

            AppendNetworkInfo(sb);
            AppendDnsInfo(sb);
            AppendZeroconfInfo(sb, hosts);
            await AppendHostnameResolutionAsync(sb, hosts);
            await AppendAddressVerificationAsync(sb, hosts);

            return sb.ToString();
        }

        private void AppendNetworkInfo(StringBuilder sb) {
            sb.AppendLine("Windows Network Interfaces");
            sb.AppendLine("--------------------------");

            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces()) {
                sb.AppendLine($"Name: {ni.Name}");
                sb.AppendLine($"Description: {ni.Description}");
                sb.AppendLine($"Type: {ni.NetworkInterfaceType}");
                sb.AppendLine($"Status: {ni.OperationalStatus}");
                sb.AppendLine($"MAC: {ni.GetPhysicalAddress()}");

                var props = ni.GetIPProperties();

                foreach (var addr in props.UnicastAddresses) {
                    sb.AppendLine($"Address: {addr.Address}");
                    sb.AppendLine($"  Family: {addr.Address.AddressFamily}");
                    sb.AppendLine($"  Prefix Length: {addr.PrefixLength}");
                }

                foreach (var gw in props.GatewayAddresses) {
                    sb.AppendLine($"Gateway: {gw.Address}");
                }

                foreach (var dns in props.DnsAddresses) {
                    sb.AppendLine($"DNS: {dns}");
                }

                sb.AppendLine();
            }
        }

        private void AppendDnsInfo(StringBuilder sb) {
            sb.AppendLine("Local DNS Information");
            sb.AppendLine("---------------------");

            try {
                string hostName = Dns.GetHostName();
                sb.AppendLine($"DNS Host Name: {hostName}");

                foreach (var addr in Dns.GetHostAddresses(hostName)) {
                    sb.AppendLine($"Local Host Address: {addr}");
                }
            } catch (Exception ex) {
                sb.AppendLine($"DNS Error: {ex.Message}");
            }

            sb.AppendLine();
        }

        private void AppendZeroconfInfo(StringBuilder sb, IReadOnlyList<IZeroconfHost> hosts) {
            sb.AppendLine("Zeroconf Discovery");
            sb.AppendLine("------------------");
            sb.AppendLine($"Service Type: {ServiceType}");
            sb.AppendLine($"Device Count: {hosts.Count}");
            sb.AppendLine();

            int index = 1;

            foreach (var host in hosts) {
                sb.AppendLine($"Device #{index}");
                sb.AppendLine("---------");
                sb.AppendLine($"Display Name: {host.DisplayName}");
                sb.AppendLine($"IP Address: {host.IPAddress}");
                sb.AppendLine($"Id: {host.Id}");

                sb.AppendLine("IP Addresses:");
                foreach (string address in GetHostAddresses(host)) {
                    sb.AppendLine($"  {address}");
                }

                sb.AppendLine();

                sb.AppendLine("Raw Host Object");
                sb.AppendLine("---------------");
                AppendObjectDump(sb, host, 0);
                sb.AppendLine();

                index++;
            }
        }

        private async Task AppendHostnameResolutionAsync(StringBuilder sb, IReadOnlyList<IZeroconfHost> hosts) {
            sb.AppendLine("Hostname Resolution");
            sb.AppendLine("-------------------");

            HashSet<string> hostNames = new();

            foreach (var host in hosts) {
                foreach (var service in host.Services.Values) {
                    string serviceName = service.ServiceName;

                    int start = serviceName.IndexOf("(scuc-");
                    if (start < 0) {
                        continue;
                    }

                    start++;

                    int end = serviceName.IndexOf(')', start);
                    if (end < 0) {
                        continue;
                    }

                    string hostName = serviceName.Substring(start, end - start) + ".local";
                    hostNames.Add(hostName);
                }
            }

            if (hostNames.Count == 0) {
                sb.AppendLine("No hostnames discovered.");
                sb.AppendLine();
                return;
            }

            foreach (string hostName in hostNames.OrderBy(h => h)) {
                sb.AppendLine($"Host: {hostName}");

                try {
                    IPAddress[] addresses = await Dns.GetHostAddressesAsync(hostName);

                    if (addresses.Length == 0) {
                        sb.AppendLine("  No addresses returned.");
                    } else {
                        foreach (IPAddress address in addresses) {
                            sb.AppendLine($"  {address}");
                        }
                    }
                } catch (Exception ex) {
                    sb.AppendLine($"  Resolution Failed: {ex.Message}");
                }

                sb.AppendLine();
            }
        }

        private async Task AppendAddressVerificationAsync(StringBuilder sb, IReadOnlyList<IZeroconfHost> hosts) {
            sb.AppendLine("Address Verification");
            sb.AppendLine("--------------------");
            sb.AppendLine($"Ping Timeout: {PingTimeoutMs} ms");
            sb.AppendLine($"TCP Connect Timeout: {TcpTimeoutMs} ms");
            sb.AppendLine($"TCP Connect Port: {SigCoreServerPort}");
            sb.AppendLine();

            var addresses = hosts
                .SelectMany(GetHostAddresses)
                .Distinct()
                .OrderBy(a => a)
                .ToList();

            if (addresses.Count == 0) {
                sb.AppendLine("No addresses found.");
                sb.AppendLine();
                return;
            }

            foreach (string address in addresses) {
                sb.AppendLine($"Address: {address}");

                bool pingOk = await PingAddressAsync(address);
                sb.AppendLine($"  Ping: {(pingOk ? "PASS" : "FAIL")}");

                bool tcpOk = await TcpConnectAsync(address, SigCoreServerPort);
                sb.AppendLine($"  TCP {SigCoreServerPort}: {(tcpOk ? "PASS" : "FAIL")}");

                sb.AppendLine();
            }
        }

        private IEnumerable<string> GetHostAddresses(IZeroconfHost host) {
            var addresses = new List<string>();

            if (!string.IsNullOrWhiteSpace(host.IPAddress)) {
                addresses.Add(host.IPAddress);
            }

            if (host.IPAddresses != null) {
                foreach (string address in host.IPAddresses) {
                    if (!string.IsNullOrWhiteSpace(address)) {
                        addresses.Add(address);
                    }
                }
            }

            return addresses.Distinct();
        }

        private async Task<bool> PingAddressAsync(string address) {
            try {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(address, PingTimeoutMs);
                return reply.Status == IPStatus.Success;
            } catch {
                return false;
            }
        }

        private async Task<bool> TcpConnectAsync(string address, int port) {
            try {
                using var client = new TcpClient();

                Task connectTask = client.ConnectAsync(address, port);
                Task timeoutTask = Task.Delay(TcpTimeoutMs);

                Task completedTask = await Task.WhenAny(connectTask, timeoutTask);

                if (completedTask != connectTask) {
                    return false;
                }

                await connectTask;
                return client.Connected;
            } catch {
                return false;
            }
        }

        private void AppendObjectDump(StringBuilder sb, object obj, int indent) {
            if (obj == null) {
                sb.AppendLine($"{Indent(indent)}<null>");
                return;
            }

            Type type = obj.GetType();
            sb.AppendLine($"{Indent(indent)}Type: {type.FullName}");

            foreach (PropertyInfo prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)) {
                if (prop.GetIndexParameters().Length > 0) {
                    continue;
                }

                object value;

                try {
                    value = prop.GetValue(obj);
                } catch (Exception ex) {
                    sb.AppendLine($"{Indent(indent)}{prop.Name}: <error reading property: {ex.Message}>");
                    continue;
                }

                if (value == null) {
                    sb.AppendLine($"{Indent(indent)}{prop.Name}: <null>");
                } else if (value is string || value.GetType().IsValueType) {
                    sb.AppendLine($"{Indent(indent)}{prop.Name}: {value}");
                } else if (value is IDictionary dict) {
                    sb.AppendLine($"{Indent(indent)}{prop.Name}:");

                    foreach (DictionaryEntry entry in dict) {
                        sb.AppendLine($"{Indent(indent + 1)}{entry.Key} = {entry.Value}");
                    }
                } else if (value is IEnumerable enumerable && value is not string) {
                    sb.AppendLine($"{Indent(indent)}{prop.Name}:");

                    foreach (var item in enumerable) {
                        if (item == null) {
                            sb.AppendLine($"{Indent(indent + 1)}<null>");
                        } else if (item is string || item.GetType().IsValueType) {
                            sb.AppendLine($"{Indent(indent + 1)}{item}");
                        } else {
                            AppendObjectDump(sb, item, indent + 1);
                        }
                    }
                } else {
                    sb.AppendLine($"{Indent(indent)}{prop.Name}: {value}");
                }
            }
        }

        private string Indent(int level) {
            return new string(' ', level * 2);
        }

        private void Close() {
            Application.Current.MainWindow?.Close();
        }
    }

    internal class DiscoveredDeviceVM : ViewModelBase {
        private string deviceName;
        public string DeviceName {
            get => deviceName;
            set {
                deviceName = value;
                OnPropertyChanged();
            }
        }

        private string address;
        public string Address {
            get => address;
            set {
                address = value;
                OnPropertyChanged();
            }
        }
    }
}