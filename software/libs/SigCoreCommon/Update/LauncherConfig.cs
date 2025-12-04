using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;

namespace SigCoreCommon.Update {
    public class LauncherConfig {
        static private readonly string path = "/opt/sigcore/config.json";

        [JsonProperty("executables")]
        public string[] Executables { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("revision")]
        public string Revision { get; set; }

        [JsonProperty("public")]
        public string Public { get; set; }

        [JsonProperty("manifest_url")]
        public string ManifestUrl { get; set; }

        [JsonProperty("manifest_sig_url")]
        public string ManifestSigUrl { get; set; }

        [JsonProperty("tmp_path")]
        public string TempPath { get; set; }

        // ---------------------------------------------------------
        // LOAD: create default config, then overlay file values
        // ---------------------------------------------------------
        public static LauncherConfig Load(string filename = "") {
            string pathname;
            if (filename == "") {
                pathname = path;
            } else {
                pathname = filename;
            }

            // Always start with defaults
            LauncherConfig cfg = new LauncherConfig();

            try {
                if (!File.Exists(pathname))
                    return cfg;

                string json = File.ReadAllText(pathname);

                if (string.IsNullOrWhiteSpace(json))
                    return cfg;

                JObject obj = JObject.Parse(json);

                cfg.Executables = obj["executables"]?.ToObject<string[]>() ?? cfg.Executables;
                cfg.Version = (string?)obj["version"] ?? cfg.Version;
                cfg.Revision = (string?)obj["revision"] ?? cfg.Revision;
                cfg.Public = (string?)obj["public"] ?? cfg.Public;
                cfg.ManifestUrl = (string?)obj["manifest_url"] ?? cfg.ManifestUrl;
                cfg.ManifestSigUrl = (string?)obj["manifest_sig_url"] ?? cfg.ManifestSigUrl;
                cfg.TempPath = (string?)obj["tmp_path"] ?? cfg.TempPath;
            } catch (Exception ex) {
                Console.WriteLine($"LauncherConfig.Load: corrupted file, using defaults. Error: {ex.Message}");
                // cfg already contains full defaults, so just return it
            }

            return cfg;
        }

        // ---------------------------------------------------------
        // SAVE: just serialize the whole current object
        // ---------------------------------------------------------
        public void Save(string filename="") {
            string pathname;
            if (filename == "") {
                pathname = path;
            } else {
                pathname = filename;
            }
            string json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(pathname, json);
        }

        // ---------------------------------------------------------
        // DEFAULTS: full, healthy config
        // ---------------------------------------------------------
        public LauncherConfig() {
            Executables = new string[] {
                "/opt/sigcore/sigcoreserver",
                "/opt/sigcore/WebBridge"
            };
            Version = "1.0.0";
            Revision = "revA";
            Public = "/opt/sigcore/keys/sigcore_public.pem";
            ManifestUrl = "https://sigcoreuc.com/update/revA/manifest.json";
            ManifestSigUrl = "https://sigcoreuc.com/update/revA/manifest.sig";
            TempPath = "/tmp/sigcore";
        }
    }
}
