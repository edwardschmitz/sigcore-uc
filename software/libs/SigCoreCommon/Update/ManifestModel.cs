using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

namespace SigCoreCommon.Update {
    public class ManifestModel {

        [JsonProperty("hardware_rev")]
        public string HardwareRevision { get; set; }

        [JsonProperty("latest_version")]
        public string LatestVersion { get; set; }

        [JsonProperty("public_key_fingerprint")]
        public string PublicKeyFingerprint { get; set; }

        [JsonProperty("requires_reboot")]
        public bool RequiresReboot { get; set; } = false;

        [JsonProperty("versions")]
        public List<VersionEntry> Versions { get; set; }

        public class VersionEntry {

            [JsonProperty("version")]
            public string Version { get; set; }

            [JsonProperty("release_date")]
            public string ReleaseDate { get; set; }

            [JsonProperty("hash_sha256")]
            public string HashSha256 { get; set; }

            [JsonProperty("signature")]
            public string Signature { get; set; }

            [JsonProperty("url")]
            public string Url { get; set; }

            [JsonProperty("notes")]
            public string Notes { get; set; }

            [JsonProperty("executables")]
            public List<string> Executables { get; set; }
        }

        public static ManifestModel Load(string path) {
            string json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<ManifestModel>(json);
        }

        public void Save(string path) {
            string json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(path, json);
        }
    }
}
 