using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;

namespace SigCoreCommon.Update {

    public class LauncherConfig {
        private static readonly Log EventLog = new Log();

        private static readonly string path = "/opt/sigcore/config.json";

        [JsonProperty("executables")]
        public string[] Executables { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("revision")]
        public string Revision { get; set; }

        [JsonProperty("public")]
        public string Public { get; set; }

        [JsonProperty("tmp_path")]
        public string TempPath { get; set; }

        // ---------------------------------------------------------
        // LOAD
        // ---------------------------------------------------------
        public static LauncherConfig Load(string filename = "") {
            string pathname = string.IsNullOrEmpty(filename) ? path : filename;

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
                cfg.TempPath = (string?)obj["tmp_path"] ?? cfg.TempPath;

            } catch (Exception ex) {
                EventLog.Error(
                    "Configuration",
                    "LauncherConfigLoadFailure",
                    "Launcher configuration is corrupt; defaults will be used.",
                    ex);
            }

            return cfg;
        }

        // ---------------------------------------------------------
        // SAVE
        // ---------------------------------------------------------
        public void Save(string filename = "") {
            string pathname = string.IsNullOrEmpty(filename) ? path : filename;
            string json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(pathname, json);
        }

        // ---------------------------------------------------------
        // DEFAULTS
        // ---------------------------------------------------------
        public LauncherConfig() {
            Executables = new[] {
                "/opt/sigcore/sigcoreserver",
                "/opt/sigcore/WebBridge"
            };
            Version = "1.0.0";
            Revision = "revA";
            Public = "/opt/sigcore/keys/sigcore_public.pem";
            TempPath = "/tmp/sigcore";
        }
    }
}
