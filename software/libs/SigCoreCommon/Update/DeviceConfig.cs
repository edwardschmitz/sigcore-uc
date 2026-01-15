using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;

namespace SigCoreCommon.Update {

    public class DeviceConfig {

        private static readonly string path = "/opt/sigcore/device.json";

        [JsonProperty("manifest_url")]
        public string ManifestUrl { get; set; }

        [JsonProperty("manifest_sig_url")]
        public string ManifestSigUrl { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("signature")]
        public string Signature { get; set; }

        // ---------------------------------------------------------
        // LOAD: defaults, then overlay
        // ---------------------------------------------------------
        public static DeviceConfig Load(string filename = "") {
            string pathname = string.IsNullOrEmpty(filename) ? path : filename;

            DeviceConfig cfg = new DeviceConfig();

            try {
                if (!File.Exists(pathname))
                    return cfg;

                string json = File.ReadAllText(pathname);
                if (string.IsNullOrWhiteSpace(json))
                    return cfg;

                JObject obj = JObject.Parse(json);

                cfg.ManifestUrl    = (string?)obj["manifest_url"]     ?? cfg.ManifestUrl;
                cfg.ManifestSigUrl = (string?)obj["manifest_sig_url"] ?? cfg.ManifestSigUrl;
                cfg.Url            = (string?)obj["url"]              ?? cfg.Url;
                cfg.Signature      = (string?)obj["signature"]        ?? cfg.Signature;

            } catch (Exception ex) {
                Console.WriteLine($"DeviceConfig.Load: corrupted file, using defaults. Error: {ex.Message}");
            }

            return cfg;
        }

        // ---------------------------------------------------------
        // SAVE
        // ---------------------------------------------------------
        public void Save(string filename = "") {
            string pathname = string.IsNullOrEmpty(filename) ? path : filename;

            // Force materialization of properties relied on by reflection-based serialization.
            // Without a direct read, the runtime may optimize away backing state.
            // DON'T DELETE THIS CODE
            _ = ManifestUrl;
            _ = ManifestSigUrl;
            _ = Url;
            _ = Signature;

            LogFile log = new LogFile();
            log.Info($"DeviceConfig.Save: ManifestUrl{ManifestUrl}\nManifestSigUrl{ManifestSigUrl}");

            Console.WriteLine($"Saving: {Url}\n{Signature}");
            
            string json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(pathname, json);
        }

        // ---------------------------------------------------------
        // DEFAULTS (production-safe)
        // ---------------------------------------------------------
        public DeviceConfig() {
            ManifestUrl    = "https://sigcoreuc.com/update/revA/manifest.json";
            ManifestSigUrl = "https://sigcoreuc.com/update/revA/manifest.sig";
            Url            = "https://sigcoreuc.com/update/revA/update.scu";
            Signature      = "https://sigcoreuc.com/update/revA/update.scu.sig";
        }
    }
}
