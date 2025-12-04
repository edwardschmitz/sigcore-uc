using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace SigCoreLauncher {
    public static class RollbackUtil {

        public static void CreatePendingMarker(string markerPath) {
            string directory = Path.GetDirectoryName(markerPath);
            if (!Directory.Exists(directory)) {
                Directory.CreateDirectory(directory);
            }
            if (!File.Exists(markerPath)) {
                File.WriteAllText(markerPath, string.Empty);
            }
        }

        public static void RemovePendingMarker(string markerPath) {
            if (File.Exists(markerPath)) {
                File.Delete(markerPath);
            }
        }

        public static bool PendingMarkerExists(string markerPath) {
            return File.Exists(markerPath);
        }

        public static string CreateBackupRoot(string baseBackupPath) {
            if (!Directory.Exists(baseBackupPath)) {
                Directory.CreateDirectory(baseBackupPath);
            }

            // CHANGED: Use LOCAL TIME instead of UTC
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

            string backupRoot = Path.Combine(baseBackupPath, timestamp);
            Directory.CreateDirectory(backupRoot);
            return backupRoot;
        }

        public static void BackupDirectory(string liveRoot, string backupRoot) {
            RecursiveCopy(liveRoot, backupRoot);
        }

        public static void RestoreDirectory(string liveRoot, string backupRoot) {
            RecursiveDelete(liveRoot);
            RecursiveCopy(backupRoot, liveRoot);
        }

        public static string GetMostRecentBackup(string baseBackupPath) {
            if (!Directory.Exists(baseBackupPath)) {
                return null;
            }
            string[] directories = Directory.GetDirectories(baseBackupPath);
            if (directories.Length == 0) {
                return null;
            }
            string[] ordered = directories.OrderBy(d => d).ToArray();
            return ordered[ordered.Length - 1];
        }

        private static void RecursiveCopy(string sourceDir, string destDir) {
            DirectoryInfo sourceInfo = new DirectoryInfo(sourceDir);

            if (IsExcluded(sourceInfo.Name)) {
                return;
            }

            if (!Directory.Exists(destDir)) {
                Directory.CreateDirectory(destDir);
            }

            FileInfo[] files = sourceInfo.GetFiles();
            for (int i = 0; i < files.Length; i++) {
                FileInfo file = files[i];
                string destFile = Path.Combine(destDir, file.Name);
                file.CopyTo(destFile, true);
            }

            DirectoryInfo[] dirs = sourceInfo.GetDirectories();
            for (int i = 0; i < dirs.Length; i++) {
                DirectoryInfo dir = dirs[i];
                if (IsExcluded(dir.Name)) {
                    continue;
                }
                string newDest = Path.Combine(destDir, dir.Name);
                RecursiveCopy(dir.FullName, newDest);
            }
        }

        private static void RecursiveDelete(string path) {
            DirectoryInfo di = new DirectoryInfo(path);

            DirectoryInfo[] dirs = di.GetDirectories();
            for (int i = 0; i < dirs.Length; i++) {
                DirectoryInfo dir = dirs[i];
                if (IsExcluded(dir.Name)) {
                    continue;
                }
                RecursiveDelete(dir.FullName);
                Directory.Delete(dir.FullName, true);
            }

            FileInfo[] files = di.GetFiles();
            for (int i = 0; i < files.Length; i++) {
                FileInfo file = files[i];
                if (IsExcluded(file.Name)) {
                    continue;
                }
                file.Delete();
            }
        }

        private static bool IsExcluded(string name) {
            if (string.Equals(name, "keys", StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
            if (string.Equals(name, "backup", StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
            if (string.Equals(name, "pending-update", StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
            return false;
        }
    }
}
