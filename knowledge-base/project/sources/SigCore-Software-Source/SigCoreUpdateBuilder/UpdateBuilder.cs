using Newtonsoft.Json;
using SigCoreCommon.Update;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SigCoreUpdateBuilder {
    public class UpdateBuilder {

        private readonly string revision;
        private readonly string version;
        private readonly string notes;
        private readonly string privateKeyPath;

        private readonly string serverName = "sigcoreserver";
        private readonly string bridgeName = "WebBridge";
        private readonly string updaterName = "SigCoreUpdater";

        private readonly string serverSrc;
        private readonly string bridgeSrc;
        private readonly string updaterSrc;
        private readonly string updaterFilesSrc;
        private readonly string webSrc;
        private readonly string publicKeyPath;

        // Base resolved paths
        private readonly string exeDir;
        private readonly string repoRoot;
        private readonly string buildRoot;

        public UpdateBuilder(
            string revision,
            string version,
            string notes,
            string privateKeyPath
        ) {
            this.revision = revision;
            this.version = version;
            this.notes = notes ?? "";
            this.privateKeyPath = privateKeyPath;

            exeDir = AppDomain.CurrentDomain.BaseDirectory;
            repoRoot = Path.GetFullPath(Path.Combine(exeDir, @"..\..\..\..\"));
            buildRoot = Path.Combine(repoRoot, "build");

            serverSrc = Path.Combine(buildRoot, @"server\publish\sigcoreserver");
            bridgeSrc = Path.Combine(buildRoot, @"webbridge\publish\WebBridge");
            updaterSrc = Path.Combine(buildRoot, @"updater\publish\SigCoreUpdater");
            updaterFilesSrc = Path.Combine(repoRoot, @"software\UpdaterFiles");
            webSrc = Path.Combine(updaterFilesSrc, "www");

            publicKeyPath = Path.Combine(repoRoot, @"keys\sigcore_public.pem");
        }

        public void Run() {

            Console.WriteLine();
            Console.WriteLine("============================================================");
            Console.WriteLine(" SigCore Update Builder");
            Console.WriteLine($" Revision: {revision}");
            Console.WriteLine($" Version : {version}");
            Console.WriteLine("============================================================");

            ReportAction("Resolve build paths");
            Console.WriteLine($"  Executable directory : {exeDir}");
            Console.WriteLine($"  Repository root      : {repoRoot}");
            Console.WriteLine($"  Build root           : {buildRoot}");
            Console.WriteLine($"  Server source        : {serverSrc}");
            Console.WriteLine($"  WebBridge source     : {bridgeSrc}");
            Console.WriteLine($"  Updater source       : {updaterSrc}");
            Console.WriteLine($"  Additional files     : {updaterFilesSrc}");
            Console.WriteLine($"  Website source       : {webSrc}");
            Console.WriteLine($"  Public key           : {publicKeyPath}");
            Console.WriteLine($"  Private key          : {privateKeyPath}");

            // The SCU root is now the root of ONE version directory. It is no longer
            // a filesystem image containing /opt, /var, Launcher, or live paths.
            string tempRoot = Path.Combine(Path.GetTempPath(), "scu_build_" + Guid.NewGuid());
            ReportAction("Create temporary release payload");
            Console.WriteLine($"  {tempRoot}");
            Directory.CreateDirectory(tempRoot);

            // config.json belongs to this version and contains only version-runtime data.
            // Executable paths are relative to the selected version directory.
            ReportAction("Create release config");
            var config = new {
                executables = new string[] {
                    serverName,
                    bridgeName,
                    updaterName
                },
                version = version,
                revision = revision
            };

            string builderConfigPath = Path.Combine(tempRoot, "config.json");
            Console.WriteLine($"  Write: {builderConfigPath}");
            File.WriteAllText(
                builderConfigPath,
                JsonConvert.SerializeObject(config, Formatting.Indented)
            );

            ReportAction("Copy executable payload");
            CopyFile(serverSrc, Path.Combine(tempRoot, serverName), "SigCore Server");
            CopyFile(bridgeSrc, Path.Combine(tempRoot, bridgeName), "WebBridge");
            CopyFile(updaterSrc, Path.Combine(tempRoot, updaterName), "SigCore Updater");
            // Include all additional release files (including www and installation-tasks.json).
            // Do not allow them to overwrite generated config.json or published executables.
            ReportAction("Copy additional release files");
            CopyDirectory(updaterFilesSrc, tempRoot, rejectExisting: true);

            // Preserve the existing secondary website export used by the repository/hosting workflow.
            string secondaryWebRoot = Path.Combine(
                repoRoot,
                @"software\Web\Website\Website\root\update",
                revision
            );

            ReportAction("Copy website to secondary export folder");
            Console.WriteLine($"  Destination: {secondaryWebRoot}");
            Directory.CreateDirectory(secondaryWebRoot);
            CopyDirectory(webSrc, secondaryWebRoot);

            // Update output location
            string stagingRoot = Path.Combine(buildRoot, @"update\updates");
            string stagingDir = Path.Combine(stagingRoot, revision, version);
            ReportAction("Prepare update output folder");
            Console.WriteLine($"  {stagingDir}");
            Directory.CreateDirectory(stagingDir);

            string scuPath = Path.Combine(stagingDir, "update.scu");

            // This report is intentionally before archive creation. It shows exactly
            // what will be packed into update.scu and is useful for catching duplicate payloads.
            ReportPayload(tempRoot);

            ReportAction("Create SCU archive");
            Console.WriteLine($"  Source : {tempRoot}");
            Console.WriteLine($"  Output : {scuPath}");
            ScuArchive.Create(tempRoot, scuPath);
            Console.WriteLine($"  Archive size: {FormatBytes(new FileInfo(scuPath).Length)} ({new FileInfo(scuPath).Length:N0} bytes)");

            // Verify the exact archive that will be distributed.
            // Keep the extracted payload beside the update artifacts so it can be
            // inspected after the build completes.
            string verifyPath = Path.Combine(stagingDir, "verify_extract");

            ReportAction("Prepare verification folder");
            if (Directory.Exists(verifyPath)) {
                Console.WriteLine($"  Delete existing: {verifyPath}");
                Directory.Delete(verifyPath, true);
            }
            Directory.CreateDirectory(verifyPath);

            ReportAction("Verify SCU by extracting it");
            Console.WriteLine($"  Extract to: {verifyPath}");
            ScuArchive.Extract(scuPath, verifyPath);
            ReportDirectorySummary("Verified payload", verifyPath);
            ValidateExtractedPayload(verifyPath);

            Console.WriteLine($"  Verification folder retained: {verifyPath}");

            ReportAction("Sign SCU");
            string scuSig = scuPath + ".sig";
            Console.WriteLine($"  Signature: {scuSig}");
            SignatureHelper.SignFile(privateKeyPath, scuPath, scuSig);

            ReportAction("Build manifest.json");
            ManifestModel manifest = new ManifestModel {
                HardwareRevision = revision,
                LatestVersion = version,
                PublicKeyFingerprint = FingerprintHelper.Compute(publicKeyPath),

                // The release is prepared while the current release continues running.
                // It becomes eligible for activation only on a later system startup.
                RequiresReboot = true,

                Versions = new List<ManifestModel.VersionEntry> {
                    new ManifestModel.VersionEntry {
                        Version = version,
                        ReleaseDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                        HashSha256 = HashHelper.Sha256File(scuPath),
                        Notes = notes,
                        Executables = new List<string> {
                            serverName,
                            bridgeName,
                            updaterName
                        }
                    }
                }
            };

            string manifestPath = Path.Combine(stagingDir, "manifest.json");
            Console.WriteLine($"  Write: {manifestPath}");
            manifest.Save(manifestPath);

            ReportAction("Sign manifest");
            string manifestSigPath = Path.Combine(stagingDir, "manifest.sig");
            Console.WriteLine($"  Signature: {manifestSigPath}");
            SignatureHelper.SignFile(privateKeyPath, manifestPath, manifestSigPath);

            Console.WriteLine();
            Console.WriteLine("============================================================");
            Console.WriteLine(" Update build complete");
            Console.WriteLine("============================================================");
            ReportOutputFile(scuPath);
            ReportOutputFile(scuSig);
            ReportOutputFile(manifestPath);
            ReportOutputFile(manifestSigPath);
            Console.WriteLine();
        }

        private void ValidateExtractedPayload(string root) {
            ReportAction("Validate extracted release payload");

            string configPath = Path.Combine(root, "config.json");
            string serverPath = Path.Combine(root, serverName);
            string bridgePath = Path.Combine(root, bridgeName);
            string updaterPath = Path.Combine(root, updaterName);

            RequireFile(configPath, "config.json");
            RequireFile(serverPath, serverName);
            RequireFile(bridgePath, bridgeName);
            RequireFile(updaterPath, updaterName);
            RequireFile(Path.Combine(root, "installation-tasks.json"), "installation-tasks.json");
            if (!Directory.Exists(Path.Combine(root, "www"))) {
                throw new DirectoryNotFoundException("Required release website directory is missing: www");
            }

            dynamic? config = JsonConvert.DeserializeObject(File.ReadAllText(configPath));
            if (config == null) {
                throw new InvalidDataException("Extracted config.json could not be parsed.");
            }

            string? configVersion = (string?)config.version;
            string? configRevision = (string?)config.revision;

            if (!string.Equals(configVersion, version, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidDataException(
                    $"Extracted config version mismatch. Expected={version}, Actual={configVersion}"
                );
            }

            if (!string.Equals(configRevision, revision, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidDataException(
                    $"Extracted config revision mismatch. Expected={revision}, Actual={configRevision}"
                );
            }

            Console.WriteLine("  Release payload validation passed.");
        }
        private void RequireFile(string path, string description) {
            if (!File.Exists(path)) {
                throw new FileNotFoundException(
                    $"Required release payload file is missing: {description}",
                    path
                );
            }
        }

        private void ReportAction(string action) {
            Console.WriteLine();
            Console.WriteLine($"[BUILD] {action}");
        }

        private void CopyFile(string src, string dest, string description) {
            FileInfo sourceInfo = new FileInfo(src);

            if (!sourceInfo.Exists) {
                throw new FileNotFoundException(
                    $"Required build artifact not found: {description}",
                    src
                );
            }

            Console.WriteLine($"  {description}");
            Console.WriteLine($"    FROM: {src}");
            Console.WriteLine($"    TO  : {dest}");
            Console.WriteLine($"    SIZE: {FormatBytes(sourceInfo.Length)} ({sourceInfo.Length:N0} bytes)");

            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(src, dest, true);
        }

        private void CopyDirectory(string src, string dest, bool rejectExisting = false) {
            if (!Directory.Exists(src)) {
                throw new DirectoryNotFoundException($"Source directory not found: {src}");
            }

            string[] files = Directory.GetFiles(src, "*", SearchOption.AllDirectories);
            long totalBytes = 0;

            Console.WriteLine($"  FROM: {src}");
            Console.WriteLine($"  TO  : {dest}");
            Console.WriteLine($"  Files: {files.Length}");

            foreach (string file in files) {
                string rel = file.Substring(src.Length).TrimStart(Path.DirectorySeparatorChar)
                                 .Replace("\\", "/");

                string outPath = Path.Combine(dest, rel.Replace('/', Path.DirectorySeparatorChar));
                if (rejectExisting && File.Exists(outPath)) {
                    throw new InvalidDataException($"Additional release file conflicts with generated payload: {rel}");
                }
                long size = new FileInfo(file).Length;
                totalBytes += size;

                Console.WriteLine($"    COPY {rel}  [{FormatBytes(size)}]");

                Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
                File.Copy(file, outPath, true);
            }

            Console.WriteLine($"  Directory copy complete: {files.Length} files, {FormatBytes(totalBytes)} ({totalBytes:N0} bytes)");
        }

        private void ReportPayload(string root) {
            ReportAction("SCU payload report - exact files that will be archived");

            string[] files = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                                      .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                                      .ToArray();

            long totalBytes = 0;

            foreach (string file in files) {
                long size = new FileInfo(file).Length;
                totalBytes += size;

                string rel = Path.GetRelativePath(root, file).Replace("\\", "/");
                Console.WriteLine($"  {FormatBytes(size),10}  {rel}");
            }

            Console.WriteLine();
            Console.WriteLine($"  Payload files: {files.Length}");
            Console.WriteLine($"  Payload size : {FormatBytes(totalBytes)} ({totalBytes:N0} bytes)");
        }

        private void ReportDirectorySummary(string label, string root) {
            string[] files = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
            long totalBytes = files.Sum(f => new FileInfo(f).Length);

            Console.WriteLine($"  {label}: {files.Length} files, {FormatBytes(totalBytes)} ({totalBytes:N0} bytes)");
        }

        private void ReportOutputFile(string path) {
            FileInfo info = new FileInfo(path);
            Console.WriteLine($"  {info.Name,-18} {FormatBytes(info.Length),10}  {path}");
        }

        private string FormatBytes(long bytes) {
            const double KB = 1024.0;
            const double MB = KB * 1024.0;
            const double GB = MB * 1024.0;

            if (bytes >= GB) {
                return $"{bytes / GB:F2} GiB";
            }

            if (bytes >= MB) {
                return $"{bytes / MB:F2} MiB";
            }

            if (bytes >= KB) {
                return $"{bytes / KB:F2} KiB";
            }

            return $"{bytes} B";
        }
    }
}
