using System;
using System.IO;
using SigCoreCommon.Update;

namespace SigCoreLauncher {
    public static class AtomicInstaller {

        public static void Install(
            string scuFile,
            string extractRoot) {

            Console.WriteLine("AtomicInstaller >>> Starting atomic install");

            if (Directory.Exists(extractRoot))
                Directory.Delete(extractRoot, true);

            Console.WriteLine("AtomicInstaller >>> CreateDirectory");

            Directory.CreateDirectory(extractRoot);

            Console.WriteLine("AtomicInstaller >>> Extract");

            ScuArchive.Extract(scuFile, extractRoot);

            Console.WriteLine("AtomicInstaller >>> InstallDirectoryAtomic");

            InstallDirectoryAtomic(extractRoot);

            Console.WriteLine("AtomicInstaller >>> Complete");

        }

        private static void InstallDirectoryAtomic(string extractRoot) {
            string[] files = Directory.GetFiles(extractRoot, "*", SearchOption.AllDirectories);

            for (int i = 0; i < files.Length; i++) {
                string inPath = files[i];
                string rel = inPath.Substring(extractRoot.Length).TrimStart(Path.DirectorySeparatorChar);
                string outPath = Path.DirectorySeparatorChar + rel;

                string dir = Path.GetDirectoryName(outPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                AtomicReplaceFile(inPath, outPath);
            }
        }

        private static void AtomicReplaceFile(string src, string dest) {
            string newPath = dest + ".new";
            string bakPath = dest + ".bak";

            if (File.Exists(newPath))
                File.Delete(newPath);

            if (File.Exists(bakPath))
                File.Delete(bakPath);

            File.Copy(src, newPath, true);

            if (File.Exists(dest))
                File.Move(dest, bakPath);

            File.Move(newPath, dest);

            if (File.Exists(bakPath))
                File.Delete(bakPath);
        }
    }
}
