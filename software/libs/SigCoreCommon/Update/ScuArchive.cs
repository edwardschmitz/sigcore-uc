using System;
using System.IO;
using System.Text;

namespace SigCoreCommon.Update {
    public static class ScuArchive {
        private const string Magic = "SCU1";

        // ============================================================================
        // CREATE SCU
        // ============================================================================
        public static void Create(string rootFolder, string outputFile) {
            if (!Directory.Exists(rootFolder))
                throw new Exception("Root folder does not exist: " + rootFolder);

            string[] files = Directory.GetFiles(rootFolder, "*", SearchOption.AllDirectories);

            using (FileStream fs = new FileStream(outputFile, FileMode.Create, FileAccess.Write))
            using (BinaryWriter bw = new BinaryWriter(fs, Encoding.UTF8)) {
                // Write header
                byte[] magicBytes = Encoding.ASCII.GetBytes(Magic);
                bw.Write(magicBytes);

                // File count
                bw.Write((uint)files.Length);

                foreach (string fullPath in files) {
                    string relativePath = GetRelativePath(rootFolder, fullPath);
                    byte[] pathBytes = Encoding.UTF8.GetBytes(relativePath);

                    // Path length
                    bw.Write((ushort)pathBytes.Length);

                    // Path
                    bw.Write(pathBytes);

                    FileInfo info = new FileInfo(fullPath);
                    bw.Write((ulong)info.Length);

                    // Copy file bytes
                    using (FileStream fsIn = new FileStream(fullPath, FileMode.Open, FileAccess.Read)) {
                        fsIn.CopyTo(bw.BaseStream);
                    }
                }
            }
        }

        // ============================================================================
        // EXTRACT SCU
        // ============================================================================
        public static void Extract(string scuFile, string targetFolder) {
            Console.WriteLine("ScuArchive.Extract >>> Starting");

            Console.WriteLine($"ScuArchive.Extract >>> scuFile: {scuFile}");

            if (!File.Exists(scuFile))
                throw new Exception("SCU file does not exist: " + scuFile);

            Console.WriteLine("ScuArchive.Extract >>> Exists / Delete");

            if (Directory.Exists(targetFolder))
                Directory.Delete(targetFolder, true);

            Console.WriteLine("ScuArchive.Extract >>> CreateDirectory");

            Directory.CreateDirectory(targetFolder);

            Console.WriteLine("ScuArchive.Extract >>> Using: FileStream, BinaryReader");

            using (FileStream fs = new FileStream(scuFile, FileMode.Open, FileAccess.Read))
            using (BinaryReader br = new BinaryReader(fs, Encoding.UTF8)) {
                // Header
                Console.WriteLine("ScuArchive.Extract >>> inside using");

                string header = Encoding.ASCII.GetString(br.ReadBytes(4));
                if (header != Magic)
                    throw new Exception("Invalid SCU header");

                uint fileCount = br.ReadUInt32();

                Console.WriteLine("ScuArchive.Extract >>> Starting for loop");

                for (uint i = 0; i < fileCount; i++) {
                    ushort pathLen = br.ReadUInt16();
                    byte[] pathBytes = br.ReadBytes(pathLen);

                    string relativePath = Encoding.UTF8.GetString(pathBytes);
                    ulong size = br.ReadUInt64();

                    string outPath = Path.Combine(targetFolder, relativePath);
                    string dir = Path.GetDirectoryName(outPath);

                    if (!string.IsNullOrEmpty(dir))
                        Directory.CreateDirectory(dir);

                    Console.WriteLine("ScuArchive.Extract >>> using: FileStream fsOut");

                    using (FileStream fsOut = new FileStream(outPath, FileMode.Create, FileAccess.Write)) {
                        const int bufSize = 65536;
                        byte[] buffer = new byte[bufSize];
                        ulong remaining = size;

                        while (remaining > 0) {
                            int toRead = remaining > (ulong)bufSize ? bufSize : (int)remaining;
                            int read = fs.Read(buffer, 0, toRead);
                            if (read <= 0)
                                throw new Exception("Unexpected end of SCU");

                            fsOut.Write(buffer, 0, read);
                            remaining -= (ulong)read;
                        }
                    }
                }
            }
        }

        // ============================================================================
        // PATH UTILITY
        // ============================================================================
        private static string GetRelativePath(string root, string full) {
            Uri rootUri = new Uri(root + "/");
            Uri fileUri = new Uri(full);

            string rel = Uri.UnescapeDataString(rootUri.MakeRelativeUri(fileUri).ToString());
            return rel.Replace("\\", "/");
        }
    }
}
