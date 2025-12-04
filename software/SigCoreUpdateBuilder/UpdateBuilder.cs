using Newtonsoft.Json;
using SigCoreCommon.Update;
using System;
using System.IO;
using System.Collections.Generic;

namespace SigCoreUpdateBuilder {
    public class UpdateBuilder {

        private readonly string revision;
        private readonly string version;
        private readonly string notes;
        private readonly string privateKeyPath;

        private readonly string optPath = "/opt/";
        private readonly string exePath = "/opt/sigcore";
        private readonly string wwwPath = "/var/www/html";
        private readonly string serverName = "/sigcoreserver";
        private readonly string bridgeName = "/WebBridge";
        private readonly string launcherName = "/SigCoreLauncher";

        private readonly string launcherSrc;
        private readonly string serverSrc;
        private readonly string bridgeSrc;
        private readonly string webSrc;
        private readonly string publicKeyPath;

        private readonly bool includeLauncher;

        // Base resolved paths
        private readonly string exeDir;
        private readonly string repoRoot;
        private readonly string buildRoot;

        public UpdateBuilder(
            string revision,
            string version,
            string notes,
            string privateKeyPath,
            bool includeLauncher
        ) {
            this.revision = revision;
            this.version = version;
            this.notes = notes ?? "";
            this.privateKeyPath = privateKeyPath;
            this.includeLauncher = includeLauncher;

            exeDir = AppDomain.CurrentDomain.BaseDirectory;
            repoRoot = Path.GetFullPath(Path.Combine(exeDir, @"..\..\..\..\"));
            buildRoot = Path.Combine(repoRoot, "build");

            launcherSrc = Path.Combine(buildRoot, @"launcher\publish\SigCoreLauncher");
            serverSrc = Path.Combine(buildRoot, @"server\publish\sigcoreserver");
            bridgeSrc = Path.Combine(buildRoot, @"webbridge\publish\WebBridge");

            webSrc = Path.Combine(repoRoot, @"software\Web\LocalWebSite\root");

            publicKeyPath = Path.Combine(repoRoot, @"keys\sigcore_public.pem");
        }

        public void Run() {

            Console.WriteLine("Starting SigCore Update Build...");

            // Temp build root
            string tempRoot = Path.Combine(Path.GetTempPath(), "scu_build_" + Guid.NewGuid());
            Directory.CreateDirectory(tempRoot);

            // Target directory roots inside the SCU image
            string optRoot = tempRoot + optPath;
            string optSigCore = tempRoot + exePath;
            string webRoot = tempRoot + wwwPath;

            Directory.CreateDirectory(optRoot);
            Directory.CreateDirectory(optSigCore);
            Directory.CreateDirectory(webRoot);

            // Build launcher config for the SCU image
            LauncherConfig config = new LauncherConfig();
            config.Executables = new string[] {
                    exePath + serverName,
                    exePath + bridgeName
                };
            config.Revision = revision;
            config.Version = version;

            // Write config INTO the SCU filesystem
            string builderConfigPath = Path.Combine(optSigCore, "config.json");
            config.Save(builderConfigPath);

            Console.WriteLine("Copying source files...");

            CopyFile(serverSrc, optSigCore + serverName);
            CopyFile(bridgeSrc, optSigCore + bridgeName);

            if (includeLauncher) {
                Console.WriteLine("Including updated launcher...");
                CopyFile(launcherSrc, optRoot + launcherName);
            }

            // Website → SCU image
            CopyDirectory(webSrc, webRoot);

            // Secondary website export for hosting
            string secondaryWebRoot = Path.Combine(
                repoRoot,
                @"software\Web\Website\Website\root\update",
                revision
            );

            Console.WriteLine("Copying website to secondary export folder...");
            Directory.CreateDirectory(secondaryWebRoot);
            CopyDirectory(webSrc, secondaryWebRoot);

            // Update output location
            string stagingRoot = Path.Combine(buildRoot, @"update\updates");
            string stagingDir = Path.Combine(stagingRoot, revision, version);
            Directory.CreateDirectory(stagingDir);

            string scuPath = Path.Combine(stagingDir, "update.scu");

            Console.WriteLine("Creating SCU archive...");
            ScuArchive.Create(tempRoot, scuPath);

            string verifyPath = Path.Combine(stagingDir, "verify_extract");
            ScuArchive.Extract(scuPath, verifyPath);

            Console.WriteLine("Signing SCU...");
            string scuSig = scuPath + ".sig";
            SignatureHelper.SignFile(privateKeyPath, scuPath, scuSig);

            Console.WriteLine("Writing manifest.json...");
            ManifestModel manifest = new ManifestModel {
                HardwareRevision = revision,
                LatestVersion = version,
                PublicKeyFingerprint = FingerprintHelper.Compute(publicKeyPath),
                RequiresReboot = includeLauncher,

                Versions = new List<ManifestModel.VersionEntry> {
            new ManifestModel.VersionEntry {
                Version = version,
                ReleaseDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                HashSha256 = HashHelper.Sha256File(scuPath),
                Url = $"https://sigcoreuc.com/update/{revision}/update.scu",
                Signature = $"https://sigcoreuc.com/update/{revision}/update.scu.sig",
                Notes = notes,
                Executables = new List<string> {
                    "/opt/sigcore/sigcoreserver",
                    "/opt/sigcore/WebBridge",
                    "/opt/SigCoreLauncher"
                }
            }
        }
            };

            string manifestPath = Path.Combine(stagingDir, "manifest.json");
            manifest.Save(manifestPath);

            Console.WriteLine("Signing manifest...");
            string manifestSigPath = Path.Combine(stagingDir, "manifest.sig");
            SignatureHelper.SignFile(privateKeyPath, manifestPath, manifestSigPath);

            Console.WriteLine();
            Console.WriteLine("Update build complete.");
            Console.WriteLine("  " + scuPath);
            Console.WriteLine("  " + scuSig);
            Console.WriteLine("  " + manifestPath);
            Console.WriteLine("  " + manifestSigPath);
        }

        private void CopyFile(string src, string dest) {
            Console.WriteLine($"src: {src}, dest: {dest}");
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            File.Copy(src, dest, true);
        }

        private void CopyDirectory(string src, string dest) {
            foreach (string file in Directory.GetFiles(src, "*", SearchOption.AllDirectories)) {
                string rel = file.Substring(src.Length).TrimStart(Path.DirectorySeparatorChar)
                                 .Replace("\\", "/");

                string outPath = dest + "/" + rel;

                Directory.CreateDirectory(Path.GetDirectoryName(outPath));
                File.Copy(file, outPath, true);
            }
        }
    }
}
