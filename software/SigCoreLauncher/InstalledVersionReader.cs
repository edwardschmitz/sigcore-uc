using System;
using System.IO;

namespace SigCoreLauncher {
    public static class InstalledVersionReader {
        public static string ReadRevision(string path) {
            if (!File.Exists(path))
                return "unknown";

            string text = File.ReadAllText(path).Trim();
            return text;
        }

        public static string ReadVersion(string path) {
            if (!File.Exists(path))
                return "0.0.0";

            string text = File.ReadAllText(path).Trim();
            return text;
        }
    }
}
