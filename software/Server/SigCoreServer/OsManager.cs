using Newtonsoft.Json.Linq;
using SigCoreCommon;
using SigCoreCommon.Payloads;
using System;
using System.Diagnostics;
using System.Net;

namespace SigCoreServer {

    public class OsManager {
        private static readonly Log _log = new Log();


        // ──────────────────────────────────────────────────────────────
        // PUBLIC METHODS
        // ──────────────────────────────────────────────────────────────

        public CommandResult ApplyNetworkConfig(NetworkSettingsPayload payload) {

            ApplyEthernetConfig(
                payload.EthDhcpEnabled,
                payload.EthIpAddress,
                payload.EthSubnetMask,
                payload.EthGateway,
                payload.EthDns
            );

            ApplySSID(payload.WifiSsid, payload.WifiPsk);

            ApplyWifiConfig(
                payload.WifiDhcpEnabled,
                payload.WifiIpAddress,
                payload.WifiSubnetMask,
                payload.WifiGateway,
                payload.WifiDns
            );

            return new CommandResult { ExitCode = 0 };
        }


        public CommandResult RestartDevice() {
            return RunCommand("sudo reboot");
        }

        public CommandResult ShutdownDevice() {
            return RunCommand("sudo shutdown -h now");
        }

        public CommandResult GetNetworkStatus() {
            string ethConn = GetConnectionForDevice("eth0");
            return RunCommand(
                $"nmcli -t -f ipv4.method,ipv4.addresses,ipv4.gateway,ipv4.dns con show {ethConn}"
            );
        }

        // ──────────────────────────────────────────────────────────────
        // FACTORY RESET
        // ──────────────────────────────────────────────────────────────

        public CommandResult FactoryReset(string hostName) {
            if (string.IsNullOrWhiteSpace(hostName)) {
                _log.Error("FactorySettings", "HostnameRequired", "Factory reset requested without a hostname.");
                return new CommandResult {
                    ExitCode = 1,
                    StdOut = "",
                    StdErr = "Hostname required"
                };
            }

            string cmd1 = $"sudo hostnamectl set-hostname {hostName}";
            CommandResult r1 = RunCommand(cmd1);
            LogCommandFailure("FactorySettings", "HostnameUpdateFailed", "hostnamectl", r1);
            if (r1.ExitCode != 0) return r1;

            string cmd2 = $"echo \"{hostName}\" | sudo tee /etc/hostname";
            CommandResult r2 = RunCommand(cmd2);
            LogCommandFailure("FactorySettings", "HostnameFileUpdateFailed", "/etc/hostname", r2);
            if (r2.ExitCode != 0) return r2;

            string cmd3 =
                $"sudo sed -i 's|^127\\.0\\.1\\.1.*|127.0.1.1       {hostName}|' /etc/hosts";
            CommandResult r3 = RunCommand(cmd3);
            LogCommandFailure("FactorySettings", "HostsFileUpdateFailed", "/etc/hosts", r3);
            return r3;
        }

        private void LogCommandFailure(string category, string eventName, string step, CommandResult result) {
            if (result.ExitCode == 0)
                return;

            string detail = $"{step} failed. ExitCode={result.ExitCode}.";
            if (!string.IsNullOrWhiteSpace(result.StdErr))
                detail += $" Error={result.StdErr.Trim()}";

            _log.Error(category, eventName, detail);
        }

        // ──────────────────────────────────────────────────────────────
        // STATUS REPORTING
        // ──────────────────────────────────────────────────────────────

        public void GetSystemStatus(JObject status) {
            AddSystemStatusSections(status);
        }

        public void AddSystemStatusSections(JObject status) {
            JArray sections = (JArray)status["sections"];

            sections.Add(BuildDeviceInfoSection());
            sections.Add(BuildPerformanceSection());

            sections.Add(BuildNetworkSection("eth0", "Wired connection 1"));
            sections.Add(BuildNetworkSection("wlan0", "wlan0"));
        }

        // ──────────────────────────────────────────────────────────────
        // APPLY LOGIC
        // ──────────────────────────────────────────────────────────────

        private bool ApplyEthernetConfig(
            bool useDhcp,
            string ip,
            string subnet,
            string gateway,
            string dns
        ) {
            const string ethConn = "eth0";
            bool ok = true;

            CommandResult r;

            // 0) Ensure the connection exists
            r = RunCommand($"sudo nmcli con show {ethConn} || sudo nmcli con add type ethernet ifname eth0 con-name {ethConn}");
            ok &= (r.ExitCode == 0);

            if (useDhcp) {
                r = RunCommand($"sudo nmcli con mod {ethConn} ipv4.method auto");
                ok &= (r.ExitCode == 0);

                r = RunCommand($"sudo nmcli con mod {ethConn} ipv4.addresses \"\"");
                ok &= (r.ExitCode == 0);

                r = RunCommand($"sudo nmcli con mod {ethConn} ipv4.gateway \"\"");
                ok &= (r.ExitCode == 0);

                r = RunCommand($"sudo nmcli con mod {ethConn} ipv4.dns \"\"");
                ok &= (r.ExitCode == 0);
            } else {
                int prefix = MaskToPrefix(subnet);

                r = RunCommand($"sudo nmcli con mod {ethConn} ipv4.method manual");
                ok &= (r.ExitCode == 0);

                r = RunCommand($"sudo nmcli con mod {ethConn} ipv4.addresses {ip}/{prefix}");
                ok &= (r.ExitCode == 0);

                r = RunCommand($"sudo nmcli con mod {ethConn} ipv4.gateway {gateway}");
                ok &= (r.ExitCode == 0);

                r = RunCommand($"sudo nmcli con mod {ethConn} ipv4.dns \"{dns}\"");
                ok &= (r.ExitCode == 0);
            }

            return ok;
        }


        // ──────────────────────────────────────────────────────────────
        // STATUS BUILDERS / HELPERS (UNCHANGED)
        // ──────────────────────────────────────────────────────────────

        private JObject BuildDeviceInfoSection() {
            JArray items = new JArray();

            items.Add(MakePair("Hostname", RunCommand("hostname").StdOut.Trim()));
            items.Add(MakePair("Kernel", RunCommand("uname -r").StdOut.Trim()));

            string uptime = RunCommand("uptime -p").StdOut.Trim();
            if (uptime.StartsWith("up ")) uptime = uptime.Substring(3);
            items.Add(MakePair("Uptime", uptime));

            JObject cpuInfo = GetCpuInfo();
            items.Add(MakePair("Model", cpuInfo["model"]?.ToString() ?? ""));
            items.Add(MakePair("Revision", cpuInfo["revision"]?.ToString() ?? ""));
            items.Add(MakePair("Serial", cpuInfo["serial"]?.ToString() ?? ""));

            return MakeSection("Device Information", items);
        }

        private JObject BuildPerformanceSection() {
            JArray items = new JArray();

            // CPU Temperature
            double cpuTemp = GetCpuTemp();
            items.Add(MakePair("CPU Temperature", $"{cpuTemp:F1} °C"));

            // Memory Usage
            string memRaw = RunCommand("free | awk '/Mem:/ {print $3\" \" $2}'").StdOut.Trim();
            var memParts = memRaw.Split(' ');
            if (memParts.Length == 2 &&
                double.TryParse(memParts[0], out double usedKb) &&
                double.TryParse(memParts[1], out double totalKb)) {

                double usedMb = usedKb / 1024.0;
                double totalMb = totalKb / 1024.0;
                double pct = Math.Round(usedMb / totalMb * 100.0, 1);

                items.Add(MakePair("Memory Usage", $"{(int)usedMb}/{(int)totalMb} MB ({pct}%)"));
            } else {
                items.Add(MakePair("Memory Usage", "Unknown"));
            }

            // Disk Usage
            string disk = RunCommand("df -h / | awk 'NR==2{print $5}'").StdOut.Trim();
            items.Add(MakePair("Disk Usage", disk));

            // CPU Load (15-minute average as %)
            string load15Str = RunCommand("cat /proc/loadavg | awk '{print $3}'").StdOut.Trim();
            if (double.TryParse(load15Str, out double load15)) {
                int cores = Environment.ProcessorCount;
                double pct = Math.Round((load15 / cores) * 100.0, 1);
                items.Add(MakePair("CPU Load", $"{pct}%"));
            } else {
                items.Add(MakePair("CPU Load", "Unknown"));
            }

            return MakeSection("Performance", items);
        }

        private JObject BuildNetworkSection(string iface, string connName) {

            JArray items = new JArray();
            JObject net = GetNetworkConfigDetails(iface, connName);

            items.Add(MakePair("MAC Address", net["mac"]?.ToString() ?? ""));
            items.Add(MakePair("Mode", net["mode"]?.ToString() ?? "Unknown"));
            items.Add(MakePair("IP Address", net["ip"]?.ToString() ?? ""));
            items.Add(MakePair("Subnet Mask", net["subnet"]?.ToString() ?? ""));
            items.Add(MakePair("Gateway", net["gateway"]?.ToString() ?? ""));
            items.Add(MakePair("DNS", net["dns"]?.ToString() ?? ""));
            items.Add(MakePair("Hostname", RunCommand("hostname").StdOut.Trim()));
            items.Add(MakePair("Interface", iface));
            items.Add(MakePair("Protocol", "IPv4"));

            return MakeSection($"Network Configuration ({iface})", items);
        }

        // ──────────────────────────────────────────────────────────────
        // PRIVATE HELPERS
        // ──────────────────────────────────────────────────────────────



        private JObject MakePair(string label, string data) {
            return new JObject {
                ["label"] = label,
                ["data"] = data
            };
        }

        private JObject MakeSection(string sectionLabel, JArray items) {
            return new JObject {
                ["section"] = sectionLabel,
                ["items"] = items
            };
        }

        private double GetCpuTemp() {
            var result = RunCommand("cat /sys/class/thermal/thermal_zone0/temp");
            if (double.TryParse(result.StdOut.Trim(), out double milli))
                return milli / 1000.0;
            return double.NaN;
        }

        private JObject GetCpuInfo() {
            var result = RunCommand("cat /proc/cpuinfo | egrep 'Model|Revision|Serial'");
            JObject info = new JObject();

            string[] lines = result.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines) {
                var parts = line.Split(':', 2);
                if (parts.Length != 2) continue;
                string key = parts[0].Trim().ToLowerInvariant();
                string value = parts[1].Trim();

                switch (key) {
                    case "model": info["model"] = value; break;
                    case "revision": info["revision"] = value; break;
                    case "serial": info["serial"] = value; break;
                }
            }

            return info;
        }

        private JObject GetNetworkConfigDetails(string iface, string connName) {

            JObject net = new JObject {
                ["interface"] = iface
            };

            CommandResult result = RunCommand(
                $"nmcli -t -f GENERAL.HWADDR,IP4.ADDRESS,IP4.GATEWAY,IP4.DNS dev show {iface}"
            );

            if (result.ExitCode != 0) {
                net["error"] = "Unable to read network status";
                return net;
            }

            foreach (string line in result.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries)) {
                var parts = line.Split(':', 2);
                if (parts.Length != 2) continue;

                string key = parts[0].Trim();
                string val = parts[1].Trim();

                switch (key) {
                    case "GENERAL.HWADDR":
                        net["mac"] = val;
                        break;

                    case "IP4.ADDRESS[1]":
                        if (val.Contains('/')) {
                            var ipParts = val.Split('/');
                            net["ip"] = ipParts[0];
                            net["subnet"] = PrefixToMask(int.Parse(ipParts[1]));
                        }
                        break;

                    case "IP4.GATEWAY":
                        net["gateway"] = val;
                        break;

                    case "IP4.DNS[1]":
                    case "IP4.DNS[2]":
                        net["dns"] = net["dns"] == null
                            ? val
                            : net["dns"] + ", " + val;
                        break;
                }
            }

            // mode (DHCP / Manual)
            result = RunCommand(
                $"nmcli -t -f ipv4.method con show '{connName}'"
            );

            if (result.ExitCode == 0) {
                foreach (string line in result.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries)) {
                    var parts = line.Split(':', 2);
                    if (parts.Length == 2 && parts[0] == "ipv4.method") {
                        net["mode"] =
                            parts[1].Equals("auto", StringComparison.OrdinalIgnoreCase)
                                ? "DHCP"
                                : "Manual";
                    }
                }
            } else {
                net["mode"] = "Unknown";
            }

            return net;
        }


        private CommandResult RunCommand(string command) {

            ProcessStartInfo psi = new ProcessStartInfo("/bin/bash", "-c \"" + command + "\"") {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            psi.EnvironmentVariables["DBUS_SYSTEM_BUS_ADDRESS"] =
                "unix:path=/run/dbus/system_bus_socket";

            using Process proc = Process.Start(psi);
            string stdout = proc.StandardOutput.ReadToEnd();
            string stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            return new CommandResult {
                ExitCode = proc.ExitCode,
                StdOut = stdout.Trim(),
                StdErr = stderr.Trim()
            };
        }

        private static int MaskToPrefix(string mask) {
            if (IPAddress.TryParse(mask, out IPAddress ip)) {
                int prefix = 0;
                foreach (byte b in ip.GetAddressBytes())
                    prefix += CountBits(b);
                return prefix;
            }
            return 24;
        }

        private static int CountBits(byte b) {
            int count = 0;
            while (b != 0) {
                count += b & 1;
                b >>= 1;
            }
            return count;
        }


        public struct CommandResult {
            public int ExitCode;
            public string StdOut;
            public string StdErr;
            public override string ToString() {
                return $"\tExitCode: {ExitCode}\n\tStdOut: {StdOut}\n\tStdErr: {StdErr}";
            }
        }

        private bool ApplyWifiConfig(
            bool useDhcp,
            string ip,
            string subnet,
            string gateway,
            string dns
        ) {
            const string wifiConn = "wlan0";
            bool ok = true;
            CommandResult r;

            r = RunCommand($"sudo nmcli con show {wifiConn}");
            ok &= (r.ExitCode == 0);

            if (useDhcp) {
                r = RunCommand($"sudo nmcli con mod {wifiConn} ipv4.method auto");
                ok &= (r.ExitCode == 0);

                r = RunCommand($"sudo nmcli con mod {wifiConn} ipv4.addresses \"\"");
                ok &= (r.ExitCode == 0);

                r = RunCommand($"sudo nmcli con mod {wifiConn} ipv4.gateway \"\"");
                ok &= (r.ExitCode == 0);

                r = RunCommand($"sudo nmcli con mod {wifiConn} ipv4.dns \"\"");
                ok &= (r.ExitCode == 0);
            } else {
                int prefix = MaskToPrefix(subnet);

                r = RunCommand($"sudo nmcli con mod {wifiConn} ipv4.method manual");
                ok &= (r.ExitCode == 0);

                r = RunCommand($"sudo nmcli con mod {wifiConn} ipv4.addresses {ip}/{prefix}");
                ok &= (r.ExitCode == 0);

                r = RunCommand($"sudo nmcli con mod {wifiConn} ipv4.gateway {gateway}");
                ok &= (r.ExitCode == 0);

                r = RunCommand($"sudo nmcli con mod {wifiConn} ipv4.dns \"{dns}\"");
                ok &= (r.ExitCode == 0);
            }

            // No activation here; applies on reboot or later
            return ok;
        }
        private string GetConnectionForDevice(string device) {
            CommandResult r = RunCommand("nmcli -t -f NAME,DEVICE con show");
            if (r.ExitCode != 0)
                throw new Exception("Failed to list connections");

            foreach (string line in r.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries)) {
                string[] parts = line.Split(':');
                if (parts.Length == 2 && parts[1] == device)
                    return $"'{parts[0]}'";
            }

            throw new Exception($"No connection bound to device {device}");
        }
        private bool ApplySSID(string ssid, string psk) {
            const string wifiConn = "wlan0";

            RemoveOtherWifiConnections();

            if (string.IsNullOrWhiteSpace(ssid)) {
                RunCommand("sudo nmcli connection modify wlan0 connection.autoconnect no");
                RunCommand("sudo nmcli device disconnect wlan0");
                return true;
            }

            bool ok;

            // Ensure Wi-Fi connection exists
            ok = EnsureConnectionExists(
                wifiConn,
                "wifi",
                "wlan0",
                $"ssid \"{ssid}\""
            );
            ok &= RunCommand($"sudo nmcli con mod {wifiConn} connection.interface-name wlan0").ExitCode == 0;

            if (!ok)
                return false;

            ok &= RunCommand(
                $"sudo nmcli con mod {wifiConn} 802-11-wireless.ssid \"{ssid}\""
            ).ExitCode == 0;

            ok &= RunCommand(
                $"sudo nmcli con mod {wifiConn} 802-11-wireless-security.key-mgmt wpa-psk"
            ).ExitCode == 0;

            if (!string.IsNullOrWhiteSpace(psk)) {
                ok &= RunCommand(
                    $"sudo nmcli con mod {wifiConn} 802-11-wireless-security.psk \"{psk}\""
                ).ExitCode == 0;
            }

            ok &= RunCommand(
                $"sudo nmcli con mod {wifiConn} connection.autoconnect yes"
            ).ExitCode == 0;
            ok &= RunCommand($"sudo nmcli con mod {wifiConn} connection.autoconnect-priority 100").ExitCode == 0;

            // Activate the connection immediately
            ok &= RunCommand(
                $"sudo nmcli con up {wifiConn}"
            ).ExitCode == 0;

            return ok;
        }
        private bool EnsureConnectionExists(string connName, string type, string ifName, string extraArgs = "") {
            CommandResult r = RunCommand($"nmcli -t -f NAME connection show | grep -w {connName}");

            if (r.ExitCode == 0)
                return true;

            r = RunCommand($"sudo nmcli connection add type {type} ifname {ifName} con-name {connName} {extraArgs}");

            return r.ExitCode == 0;
        }
        private struct NetworkConfig {
            public bool UseDhcp;
            public string IP;
            public string Subnet;
            public string Gateway;
            public string DNS;
        }
        private static string PrefixToMask(int prefix) {
            uint mask = 0xffffffff << (32 - prefix);
            byte[] bytes = BitConverter.GetBytes(mask);
            Array.Reverse(bytes);
            return new IPAddress(bytes).ToString();
        }
        private void RemoveOtherWifiConnections() {
            CommandResult r = RunCommand("nmcli -t -f NAME,TYPE connection show");

            foreach (string line in r.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries)) {
                string[] parts = line.Split(':');
                if (parts.Length != 2)
                    continue;

                string name = parts[0];
                string type = parts[1];

                if (type == "wifi" && name != "wlan0") {
                    RunCommand($"sudo nmcli connection delete \"{name}\"");
                }
            }
        }
    }
}
