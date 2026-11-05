using Newtonsoft.Json.Linq;

namespace SigCoreCommon.Payloads {

    public sealed class NetworkSettingsPayload {

        private readonly JObject _root;

        public NetworkSettingsPayload(JObject root) {
            _root = root ?? new JObject();
            EnsureStructure();
        }

        public NetworkSettingsPayload() {
            _root = new JObject();
            EnsureStructure();
        }

        private void EnsureStructure() {
            _root["ethernet"] ??= new JObject();
            _root["wifi"] ??= new JObject();
        }

        // ─────────────────────────────────────────────
        // SYSTEM
        // ─────────────────────────────────────────────

        public string Hostname {
            get => (string)_root["hostname"];
            set => _root["hostname"] = value;
        }

        // ─────────────────────────────────────────────
        // ETHERNET
        // ─────────────────────────────────────────────

        public bool EthDhcpEnabled {
            get => (bool?)_root["ethernet"]["dhcpEnabled"] ?? false;
            set => _root["ethernet"]["dhcpEnabled"] = value;
        }

        public string EthIpAddress {
            get => (string)_root["ethernet"]["ipAddress"];
            set => _root["ethernet"]["ipAddress"] = value;
        }

        public string EthSubnetMask {
            get => (string)_root["ethernet"]["subnetMask"];
            set => _root["ethernet"]["subnetMask"] = value;
        }

        public string EthGateway {
            get => (string)_root["ethernet"]["gateway"];
            set => _root["ethernet"]["gateway"] = value;
        }

        public string EthDns {
            get => (string)_root["ethernet"]["dns"];
            set => _root["ethernet"]["dns"] = value;
        }

        // ─────────────────────────────────────────────
        // WIFI
        // ─────────────────────────────────────────────

        public string WifiSsid {
            get => (string)_root["wifi"]["ssid"];
            set => _root["wifi"]["ssid"] = value;
        }

        public string WifiPsk {
            get => (string)_root["wifi"]["psk"];
            set => _root["wifi"]["psk"] = value;
        }

        public bool WifiDhcpEnabled {
            get => (bool?)_root["wifi"]["dhcpEnabled"] ?? false;
            set => _root["wifi"]["dhcpEnabled"] = value;
        }

        public string WifiIpAddress {
            get => (string)_root["wifi"]["ipAddress"];
            set => _root["wifi"]["ipAddress"] = value;
        }

        public string WifiSubnetMask {
            get => (string)_root["wifi"]["subnetMask"];
            set => _root["wifi"]["subnetMask"] = value;
        }

        public string WifiGateway {
            get => (string)_root["wifi"]["gateway"];
            set => _root["wifi"]["gateway"] = value;
        }

        public string WifiDns {
            get => (string)_root["wifi"]["dns"];
            set => _root["wifi"]["dns"] = value;
        }


        public JObject ToJson() => _root;
    }
}