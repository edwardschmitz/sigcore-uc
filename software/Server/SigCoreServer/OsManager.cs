using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.Net;

namespace SigCoreServer {
    public class OsManager {

        private const string Connection = "'Wired connection 1'";

        // ──────────────────────────────────────────────────────────────
        // PUBLIC METHODS
        // ──────────────────────────────────────────────────────────────

        public CommandResult ApplyNetworkConfig(bool useDhcp, string ip, string subnet, string gateway, string dns) {
            Console.WriteLine($"ApplyNetworkConfig >>> DHCP: {useDhcp}, IP: {ip}");

            if (!ShouldApplyNetworkConfig(useDhcp, ip, subnet, gateway, dns)) {
                Console.WriteLine("Network configuration unchanged — skipping apply.");
                return new CommandResult { ExitCode = 0, StdOut = "No change", StdErr = "" };
            }

            if (useDhcp) {
                RunCommand($"nmcli con mod {Connection} ipv4.method auto");
            } else {
                if (!string.IsNullOrEmpty(ip) && !string.IsNullOrEmpty(subnet)) {
                    int prefix = MaskToPrefix(subnet);
                    RunCommand($"nmcli con mod {Connection} ipv4.addresses {ip}/{prefix}");
                }

                if (!string.IsNullOrEmpty(gateway))
                    RunCommand($"nmcli con mod {Connection} ipv4.gateway {gateway}");

                if (!string.IsNullOrEmpty(dns))
                    RunCommand($"nmcli con mod {Connection} ipv4.dns \"{dns}\"");

                RunCommand($"nmcli con mod {Connection} ipv4.method manual");
            }

            return RunCommand($"nmcli con down {Connection} && sleep 2 && nmcli con up {Connection}");
        }

        public bool ShouldApplyNetworkConfig(bool useDhcp, string ip, string subnet, string gateway, string dns) {
            var current = GetCurrentNetworkConfig();

            Console.WriteLine($"Current Config: DHCP={current.UseDhcp}, IP={current.IP}, " +
                              $"Subnet={current.Subnet}, Gateway={current.Gateway}, DNS={current.DNS}");
            Console.WriteLine($"Requested Config: DHCP={useDhcp}, IP={ip}, " +
                              $"Subnet={subnet}, Gateway={gateway}, DNS={dns}");

            if (current.UseDhcp != useDhcp)
                return true;

            if (!useDhcp) {
                if (!string.Equals(current.IP, ip, StringComparison.OrdinalIgnoreCase)) return true;
                if (!string.Equals(current.Subnet, subnet, StringComparison.OrdinalIgnoreCase)) return true;
                if (!string.Equals(current.Gateway, gateway, StringComparison.OrdinalIgnoreCase)) return true;
                if (!string.Equals(current.DNS, dns, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }

        public CommandResult RestartDevice() {
            Console.WriteLine("System Restart Request");
            return RunCommand("sudo reboot");
        }

        public CommandResult ShutdownDevice() {
            return RunCommand("sudo shutdown -h now");
        }

        public CommandResult GetNetworkStatus() {
            return RunCommand($"nmcli -t -f ipv4.method,ipv4.addresses,ipv4.gateway,ipv4.dns con show {Connection}");
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
            sections.Add(BuildNetworkSection());
        }

        // ────────────────────────────────
        // Section Builders
        // ────────────────────────────────

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

        private JObject BuildNetworkSection() {
            JArray items = new JArray();

            // Fetch network configuration details
            JObject net = GetNetworkConfigDetails();

            Console.WriteLine("*** Network details\n"+net);

            // Ensure the correct values are assigned for each key
            items.Add(MakePair("MAC Address", net["mac"]?.ToString() ?? ""));
            items.Add(MakePair("Mode", net["mode"]?.ToString() ?? "Unknown"));  // Ensure mode is assigned correctly
            items.Add(MakePair("IP Address", net["ip"]?.ToString() ?? ""));
            items.Add(MakePair("Subnet Mask", net["subnet"]?.ToString() ?? ""));
            items.Add(MakePair("Gateway", net["gateway"]?.ToString() ?? ""));
            items.Add(MakePair("DNS", net["dns"]?.ToString() ?? ""));  // Ensure DNS is displayed correctly
            items.Add(MakePair("Hostname", RunCommand("hostname").StdOut.Trim()));
            items.Add(MakePair("Interface", net["interface"]?.ToString() ?? "eth0"));
            items.Add(MakePair("Protocol", "IPv4"));

            return MakeSection("Network Configuration", items);
        }

        // ──────────────────────────────────────────────────────────────
        // PRIVATE HELPERS
        // ──────────────────────────────────────────────────────────────

        private NetworkConfig GetCurrentNetworkConfig() {
            CommandResult result = RunCommand($"nmcli -t -f ipv4.method,ipv4.addresses,ipv4.gateway,ipv4.dns con show {Connection}");

            NetworkConfig config = new NetworkConfig();

            if (result.ExitCode == 0) {
                string[] lines = result.StdOut.Split('\n');
                foreach (string line in lines) {
                    string[] parts = line.Split(':');
                    if (parts.Length < 2) continue;
                    string key = parts[0].Trim();
                    string value = parts[1].Trim();

                    switch (key) {
                        case "ipv4.method":
                            config.UseDhcp = value.Equals("auto", StringComparison.OrdinalIgnoreCase);
                            break;
                        case "ipv4.addresses":
                            if (value.Contains("/")) {
                                string[] addrParts = value.Split('/');
                                config.IP = addrParts[0];
                                config.Subnet = PrefixToMask(int.Parse(addrParts[1]));
                            } else {
                                config.IP = value;
                            }
                            break;
                        case "ipv4.gateway":
                            config.Gateway = value;
                            break;
                        case "ipv4.dns":
                            config.DNS = value;
                            break;
                    }
                }
            } else {
                Console.WriteLine("Failed to read network config: " + result.StdErr);
            }

            return config;
        }

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

        private JObject GetNetworkConfigDetails() {
            JObject net = new JObject {
                ["interface"] = "eth0"  // default to eth0
            };

            // Run the first command to get detailed info about the network
            CommandResult result = RunCommand("nmcli -t -f GENERAL.HWADDR,IP4.ADDRESS,IP4.GATEWAY,IP4.DNS dev show eth0");

            if (result.ExitCode != 0) {
                net["error"] = "Unable to read network status";
                return net;
            }

            // Parse the output of the first command
            string[] lines = result.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Console.WriteLine($"**** command 1: {result.StdOut}");
            foreach (string line in lines) {
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
                            string[] ipParts = val.Split('/');
                            net["ip"] = ipParts[0];
                            net["subnet"] = PrefixToMask(int.Parse(ipParts[1]));
                        } else {
                            net["ip"] = val;
                        }
                        break;
                    case "IP4.GATEWAY":
                        net["gateway"] = val;
                        break;
                    case "IP4.DNS[1]":
                    case "IP4.DNS[2]":
                        // Handle multiple DNS entries
                        if (net["dns"] == null)
                            net["dns"] = val;
                        else
                            net["dns"] = net["dns"] + ", " + val;
                        break;
                }
            }

            // Now fetch the mode (DHCP or Manual) using the second command
            result = RunCommand("nmcli -t -f ipv4.method con show 'Wired connection 1'");

            if (result.ExitCode == 0) {
                Console.WriteLine($"**** command 2: {result.StdOut}");

                string[] modeLines = result.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in modeLines) {
                    Console.WriteLine($"Line: {line}");
                    string[] parts = line.Split(':', 2);
                    if (parts.Length != 2) continue;

                    string key = parts[0].Trim();
                    string val = parts[1].Trim();

                    Console.WriteLine($"key: {key}, val:{val}");
                    if (key == "ipv4.method") {
                        net["mode"] = val.Equals("auto", StringComparison.OrdinalIgnoreCase) ? "DHCP" : "Manual";
                    }
                }
            } else {
                net["mode"] = "Unknown";  // Set mode to Unknown if fetching the second command fails
            }

            return net;
        }

        private CommandResult RunCommand(string command) {
            var psi = new ProcessStartInfo("/bin/bash", "-c \"" + command + "\"") {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            psi.EnvironmentVariables["DBUS_SYSTEM_BUS_ADDRESS"] = "unix:path=/run/dbus/system_bus_socket";

            using (var proc = Process.Start(psi)) {
                string stdout = proc.StandardOutput.ReadToEnd();
                string stderr = proc.StandardError.ReadToEnd();
                proc.WaitForExit();

                var result = new CommandResult {
                    ExitCode = proc.ExitCode,
                    StdOut = stdout.Trim(),
                    StdErr = stderr.Trim()
                };

                Console.WriteLine("CMD: " + command);
                Console.WriteLine("OUT: " + result.StdOut);
                Console.WriteLine("ERR: " + result.StdErr);
                Console.WriteLine("EXIT: " + result.ExitCode);

                return result;
            }
        }

        public void FactoryReset(string hostName) {
            if (string.IsNullOrWhiteSpace(hostName)) {
                Console.WriteLine("FactoryReset: No hostname provided.");
                return;
            }

            Console.WriteLine("FactoryReset: Setting hostname to " + hostName);

            // 1. Set system hostname immediately
            RunCommand($"sudo hostnamectl set-hostname {hostName}");

            // 2. Update /etc/hostname
            RunCommand($"echo \"{hostName}\" | sudo tee /etc/hostname");

            // 3. Update /etc/hosts (safe sed command)
            string hostsUpdateCmd =
                $"sudo sed -i 's|^127\\.0\\.1\\.1.*|127.0.1.1       {hostName}|' /etc/hosts";

            RunCommand(hostsUpdateCmd);

            Console.WriteLine("FactoryReset: Hostname updated.");
        }

        // ──────────────────────────────────────────────────────────────
        // PURE HELPERS (STATIC)
        // ──────────────────────────────────────────────────────────────

        private static int MaskToPrefix(string mask) {
            if (IPAddress.TryParse(mask, out IPAddress ip)) {
                byte[] bytes = ip.GetAddressBytes();
                int prefix = 0;
                foreach (byte b in bytes)
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

        private static string PrefixToMask(int prefix) {
            uint mask = 0xffffffff << (32 - prefix);
            byte[] bytes = BitConverter.GetBytes(mask);
            Array.Reverse(bytes);
            return new IPAddress(bytes).ToString();
        }

        // ──────────────────────────────────────────────────────────────
        // STRUCTS
        // ──────────────────────────────────────────────────────────────

        private struct NetworkConfig {
            public bool UseDhcp;
            public string IP;
            public string Subnet;
            public string Gateway;
            public string DNS;
        }

        public struct CommandResult {
            public int ExitCode;
            public string StdOut;
            public string StdErr;
        }
    }
}
