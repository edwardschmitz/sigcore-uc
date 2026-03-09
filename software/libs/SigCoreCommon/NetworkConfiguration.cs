using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace SigCoreCommon {

    //public sealed class NetworkConfiguration {

    //    private const string ConfigPath = "/etc/wpa_supplicant/wpa_supplicant.conf";
    //    private const string CountryCode = "US";

    //    // ===============================
    //    // Wi-Fi credentials (authoritative)
    //    // ===============================

    //    public string Ssid { get; private set; }
    //    public string Psk { get; private set; }

    //    // ===============================
    //    // Ethernet (eth0)
    //    // ===============================

    //    public bool EthDhcpEnabled { get; private set; }
    //    public string EthIpAddress { get; private set; }
    //    public string EthSubnetMask { get; private set; }
    //    public string EthGateway { get; private set; }
    //    public string EthDns { get; private set; }

    //    // ===============================
    //    // Wi-Fi (wlan0)
    //    // ===============================

    //    public bool WifiDhcpEnabled { get; private set; }
    //    public string WifiIpAddress { get; private set; }
    //    public string WifiSubnetMask { get; private set; }
    //    public string WifiGateway { get; private set; }
    //    public string WifiDns { get; private set; }

    //    // ===============================
    //    // Load everything
    //    // ===============================

    //    public static NetworkConfiguration Load() {
    //        NetworkConfiguration cfg = new NetworkConfiguration();

    //        cfg.LoadWifiCredentials();
    //        cfg.LoadLanStatus();
    //        cfg.LoadWifiStatus();

    //        return cfg;
    //    }

    //    // ===============================
    //    // Wi-Fi credentials
    //    // ===============================

    //    private void LoadWifiCredentials() {
    //        if (!File.Exists(ConfigPath))
    //            return;

    //        string text = File.ReadAllText(ConfigPath);

    //        Ssid = Extract(text, @"ssid\s*=\s*""([^""]+)""");
    //        Psk = Extract(text, @"psk\s*=\s*""([^""]+)""");
    //    }

    //    // ===============================
    //    // Ethernet (eth0)
    //    // ===============================

    //    private void LoadLanStatus() {
    //        if (!InterfaceExists("eth0"))
    //            return;

    //        EthDhcpEnabled = GetDhcpMode("eth0");

    //        string ip;
    //        string mask;
    //        string gateway;
    //        string dns;

    //        LoadInterfaceRuntime(
    //            "eth0",
    //            out ip,
    //            out mask,
    //            out gateway,
    //            out dns
    //        );

    //        EthIpAddress = ip;
    //        EthSubnetMask = mask;
    //        EthGateway = gateway;
    //        EthDns = dns;
    //    }

    //    // ===============================
    //    // Wi-Fi (wlan0)
    //    // ===============================

    //    private void LoadWifiStatus() {
    //        if (!InterfaceExists("wlan0"))
    //            return;

    //        WifiDhcpEnabled = GetDhcpMode("wlan0");

    //        string ip;
    //        string mask;
    //        string gateway;
    //        string dns;

    //        LoadInterfaceRuntime(
    //            "wlan0",
    //            out ip,
    //            out mask,
    //            out gateway,
    //            out dns
    //        );

    //        WifiIpAddress = ip;
    //        WifiSubnetMask = mask;
    //        WifiGateway = gateway;
    //        WifiDns = dns;
    //    }

    //    // ===============================
    //    // Runtime interface parsing
    //    // ===============================

    //    private static void LoadInterfaceRuntime(
    //        string iface,
    //        out string ip,
    //        out string mask,
    //        out string gateway,
    //        out string dns
    //    ) {
    //        ip = mask = gateway = dns = null;

    //        string output = RunCapture(
    //            $"ip -4 addr show {iface} && ip route show dev {iface}");

    //        Match ipMatch = Regex.Match(output, @"inet\s+([\d\.]+)/(\d+)");
    //        if (ipMatch.Success) {
    //            ip = ipMatch.Groups[1].Value;
    //            mask = PrefixToMask(int.Parse(ipMatch.Groups[2].Value));
    //        }

    //        Match gwMatch = Regex.Match(output, @"default\s+via\s+([\d\.]+)");
    //        if (gwMatch.Success)
    //            gateway = gwMatch.Groups[1].Value;

    //        if (File.Exists("/etc/resolv.conf")) {
    //            MatchCollection dnsMatches =
    //                Regex.Matches(File.ReadAllText("/etc/resolv.conf"),
    //                              @"nameserver\s+([\d\.]+)");

    //            if (dnsMatches.Count > 0) {
    //                StringBuilder sb = new StringBuilder();
    //                foreach (Match m in dnsMatches) {
    //                    if (sb.Length > 0) sb.Append(", ");
    //                    sb.Append(m.Groups[1].Value);
    //                }
    //                dns = sb.ToString();
    //            }
    //        }
    //    }

    //    // ===============================
    //    // DHCP detection
    //    // ===============================

    //    private static bool GetDhcpMode(string iface) {
    //        string result = RunCapture(
    //            $"nmcli -g ipv4.method device show {iface}");

    //        return result.Trim()
    //                     .Equals("auto", StringComparison.OrdinalIgnoreCase);
    //    }

    //    // ===============================
    //    // Helpers
    //    // ===============================

    //    private static string Extract(string text, string pattern) {
    //        Match m = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
    //        return m.Success ? m.Groups[1].Value : null;
    //    }

    //    private static bool InterfaceExists(string iface) {
    //        return Directory.Exists($"/sys/class/net/{iface}");
    //    }

    //    private static string PrefixToMask(int prefix) {
    //        uint mask = prefix == 0 ? 0 : uint.MaxValue << (32 - prefix);
    //        return string.Join(".",
    //            (mask >> 24) & 0xFF,
    //            (mask >> 16) & 0xFF,
    //            (mask >> 8) & 0xFF,
    //            mask & 0xFF);
    //    }

    //    private static string RunCapture(string cmd) {
    //        ProcessStartInfo psi = new ProcessStartInfo {
    //            FileName = "bash",
    //            Arguments = $"-c \"{cmd}\"",
    //            RedirectStandardOutput = true,
    //            UseShellExecute = false
    //        };

    //        using Process p = Process.Start(psi);
    //        return p.StandardOutput.ReadToEnd();
    //    }
    //}
}
