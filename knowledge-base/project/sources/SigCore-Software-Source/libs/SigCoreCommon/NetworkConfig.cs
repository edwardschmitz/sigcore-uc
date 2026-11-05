using Newtonsoft.Json.Linq;

namespace SigCoreCommon {

    //public sealed class NetworkConfig {
    //    public string Hostname { get; set; }

    //    public EthernetConfig Ethernet { get; set; } = new EthernetConfig();
    //    public WifiConfig Wifi { get; set; } = new WifiConfig();

    //    public JObject ToPayload() {
    //        JObject obj = new JObject();
    //        obj["hostname"] = Hostname;
    //        obj["ethernet"] = Ethernet.ToPayload();
    //        obj["wifi"] = Wifi.ToPayload();
    //        return obj;
    //    }

    //    public void FromPayload(JObject root) {
    //        if (root == null)
    //            return;

    //        Hostname = (string)root["hostname"] ?? Hostname;

    //        JObject eth = root["ethernet"] as JObject;
    //        if (eth != null)
    //            Ethernet.FromPayload(eth);

    //        JObject wifi = root["wifi"] as JObject;
    //        if (wifi != null)
    //            Wifi.FromPayload(wifi);
    //    }
    //}

    //// ─────────────────────────────────────────────

    //public class AdapterConfig {
    //    public bool DhcpEnabled { get; set; }
    //    public string IpAddress { get; set; }
    //    public string SubnetMask { get; set; }
    //    public string Gateway { get; set; }
    //    public string Dns { get; set; }

    //    public JObject ToPayload() {
    //        JObject obj = new JObject();
    //        obj["dhcpEnabled"] = DhcpEnabled;
    //        obj["ipAddress"] = IpAddress;
    //        obj["subnetMask"] = SubnetMask;
    //        obj["gateway"] = Gateway;
    //        obj["dns"] = Dns;
    //        return obj;
    //    }

    //    public void FromPayload(JObject root) {
    //        if (root == null)
    //            return;

    //        DhcpEnabled = (bool?)root["dhcpEnabled"] ?? DhcpEnabled;
    //        IpAddress = (string)root["ipAddress"] ?? IpAddress;
    //        SubnetMask = (string)root["subnetMask"] ?? SubnetMask;
    //        Gateway = (string)root["gateway"] ?? Gateway;
    //        Dns = (string)root["dns"] ?? Dns;
    //    }
    //}

    //// ─────────────────────────────────────────────

    //public sealed class EthernetConfig : AdapterConfig {
    //    // No extras (for now)
    //}

    //// ─────────────────────────────────────────────

    //public sealed class WifiConfig : AdapterConfig {
    //    public string SSID { get; set; }
    //    public string PSK { get; set; }   // write-only

    //    public new JObject ToPayload() {
    //        JObject obj = base.ToPayload();
    //        obj["ssid"] = SSID;
    //        obj["psk"] = PSK;
    //        return obj;
    //    }

    //    public new void FromPayload(JObject root) {
    //        base.FromPayload(root);
    //        if (root == null)
    //            return;

    //        SSID = (string)root["ssid"] ?? SSID;
    //        PSK = (string)root["psk"] ?? PSK;
    //    }
    //}
}
