using System;
using System.IO;
using Newtonsoft.Json;

namespace SigCoreUC {
    public class UserPrefs {
        // ------------------------------------------------------
        // USER SETTINGS
        // ------------------------------------------------------
        public bool AutoReconnect { get; set; }
        public string LastIp { get; set; }
        public bool PidViewOpen { get; set; }

        // ------------------------------------------------------
        // FILE LOCATION
        // ------------------------------------------------------
        private static string PrefsPath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SigCoreUC",
                "userprefs.json");

        // ------------------------------------------------------
        // LOAD
        // ------------------------------------------------------
        public static UserPrefs Load() {
            try {
                if (File.Exists(PrefsPath)) {
                    string json = File.ReadAllText(PrefsPath);
                    UserPrefs prefs = JsonConvert.DeserializeObject<UserPrefs>(json);

                    if (prefs != null)
                        return prefs;
                }
            } catch {
                // Ignore corruption or invalid JSON
            }

            // Defaults when nothing exists or file is bad
            return new UserPrefs {
                AutoReconnect = false,
                LastIp = "",
                PidViewOpen = false
            };
        }

        // ------------------------------------------------------
        // SAVE
        // ------------------------------------------------------
        public void Save() {
            try {
                string folder = Path.GetDirectoryName(PrefsPath);
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                string json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(PrefsPath, json);
            } catch {
                // Intentionally silent: prefs should never break the app
            }
        }
    }
}
