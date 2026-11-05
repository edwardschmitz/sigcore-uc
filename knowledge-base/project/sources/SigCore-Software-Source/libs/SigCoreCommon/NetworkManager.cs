using Newtonsoft.Json.Linq;
using SigCoreCommon.Payloads;

namespace SigCoreCommon {

    /// <summary>
    /// Owns authoritative network configuration intent.
    /// OS state is observational only and must never be persisted.
    /// </summary>
    public sealed class NetworkManager {

        private NetworkSettingsPayload _payload;

        public NetworkManager() {
        }

        // ─────────────────────────────────────────────
        // FRAM INTEGRATION
        // ─────────────────────────────────────────────

        public void FromFram(JObject networkSection) {
            if (networkSection == null) {
                NetworkSettingsPayload payload = new NetworkSettingsPayload(networkSection);
                FactoryReset(payload.Hostname);
                return;
            }

            _payload = new NetworkSettingsPayload(networkSection);
        }

        public JObject ToFram() {
            return _payload.ToJson();
        }

        // ─────────────────────────────────────────────
        // UI ROUTES
        // ─────────────────────────────────────────────

        public NetworkSettingsPayload GetConfig() {
            return _payload;
        }
        public void SetConfig(NetworkSettingsPayload payload) {
            if (payload == null)
                return;

            _payload = payload;
        }

        // ─────────────────────────────────────────────
        // EXECUTION HANDOFF
        // ─────────────────────────────────────────────

        public NetworkSettingsPayload GetPayload() {
            return _payload;
        }

        // ─────────────────────────────────────────────
        // FACTORY RESET
        // ─────────────────────────────────────────────

        public void FactoryReset(string hostname) {
            _payload = new NetworkSettingsPayload();

            _payload.Hostname = hostname;

            // Ethernet defaults
            _payload.EthDhcpEnabled = true;
            _payload.EthIpAddress = "";
            _payload.EthSubnetMask = "";
            _payload.EthGateway = "";
            _payload.EthDns = "";

            // Wi-Fi defaults
            _payload.WifiSsid = "";
            _payload.WifiPsk = "";
            _payload.WifiDhcpEnabled = true;
            _payload.WifiIpAddress = "";
            _payload.WifiSubnetMask = "";
            _payload.WifiGateway = "";
            _payload.WifiDns = "";
        }
    }
}
