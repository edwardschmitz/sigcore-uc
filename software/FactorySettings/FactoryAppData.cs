using Newtonsoft.Json;
using System;
using System.IO;

namespace FactorySettings {

    public class FactoryAppData {

        private const string FolderName = "SigCoreFactorySettings";
        private const string FileName = "factory.json";

        public int LastSerial { get; set; } = 0;
        public string LastRev { get; set; } = "";
        public string LastVer { get; set; } = "";

        public static string GetFolderPath() {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, FolderName);
        }

        public static string GetFilePath() {
            return Path.Combine(GetFolderPath(), FileName);
        }

        public static FactoryAppData Load() {
            string folder = GetFolderPath();
            string path = GetFilePath();

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            if (!File.Exists(path)) {
                FactoryAppData blank = new FactoryAppData();
                blank.Save();
                return blank;
            }

            string json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<FactoryAppData>(json)
                   ?? new FactoryAppData();
        }

        public void Save() {
            string folder = GetFolderPath();
            string path = GetFilePath();

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            string json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(path, json);
        }
    }
}
