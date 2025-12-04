using System;
using System.IO;

namespace SigCoreLauncher {
    public static class VersionStampWriter {
        // =====================================================================
        // Writes the installed version and revision to their respective files
        // =====================================================================
        public static void WriteInstalledVersion(
            string revision,
            string version,
            string revisionFilePath,
            string versionFilePath) {
            WriteSafe(revisionFilePath, revision);
            WriteSafe(versionFilePath, version);
        }

        // =====================================================================
        // Atomic-safe write
        // =====================================================================
        private static void WriteSafe(string path, string value) {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string tmp = path + ".new";
            string bak = path + ".bak";

            // Clean old .new/.bak
            if (File.Exists(tmp)) File.Delete(tmp);
            if (File.Exists(bak)) File.Delete(bak);

            // 1. Write new
            File.WriteAllText(tmp, value);

            // 2. Move old → .bak (if exists)
            if (File.Exists(path))
                File.Move(path, bak);

            // 3. Move new → official
            File.Move(tmp, path);

            // 4. Clean .bak
            if (File.Exists(bak))
                File.Delete(bak);
        }
    }
}
