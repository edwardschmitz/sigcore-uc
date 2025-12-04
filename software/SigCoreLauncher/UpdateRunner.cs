using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using SigCoreCommon.Update;

namespace SigCoreLauncher {
    public class UpdateRunner {
        private readonly string manifestUrl;
        private readonly string manifestSigUrl;
        private readonly string publicKeyPath;

        private readonly string scuDownloadPath;
        private readonly string scuSigDownloadPath;
        private readonly string manifestDownloadPath;
        private readonly string manifestSigDownloadPath;

        private readonly string tempPath;

        public UpdateRunner(
            string manifestUrl,
            string manifestSigUrl,
            string publicKeyPath,
            string tempPath) {

            this.manifestUrl = manifestUrl;
            this.manifestSigUrl = manifestSigUrl;
            this.publicKeyPath = publicKeyPath;
            this.tempPath = tempPath;

            if (!Directory.Exists(tempPath))
                Directory.CreateDirectory(tempPath);

            manifestDownloadPath = Path.Combine(tempPath, "manifest.json");
            manifestSigDownloadPath = Path.Combine(tempPath, "manifest.sig");

            scuDownloadPath = Path.Combine(tempPath, "update.scu");
            scuSigDownloadPath = Path.Combine(tempPath, "update.scu.sig");
        }

        public async Task<bool> RunAsync(CancellationToken token) {
            Console.WriteLine("Starting update check.");

            await ManifestDownloader.DownloadAsync(
                manifestUrl,
                manifestSigUrl,
                manifestDownloadPath,
                manifestSigDownloadPath,
                token
            );

            Console.WriteLine("UpdateRunner.RunAsync >>> Manifest downloaded");

            bool manifestOK = ManifestValidator.Validate(
                manifestDownloadPath,
                manifestSigDownloadPath,
                publicKeyPath
            );

            Console.WriteLine("UpdateRunner.RunAsync >>> Manifest Validated");

            if (!manifestOK)
                return false;

            ManifestModel model = ManifestModel.Load(manifestDownloadPath);
            ManifestModel.VersionEntry latest = model.Versions[0];

            Console.WriteLine("Latest version: " + latest.Version);

            string scuUrl = latest.Url;
            string scuSigUrl = latest.Signature;

            Console.WriteLine("UpdateRunner.RunAsync >>> Downloading SCU");

            await ScuVerifier.DownloadAsync(
                scuUrl,
                scuSigUrl,
                scuDownloadPath,
                scuSigDownloadPath
            );

            Console.WriteLine("UpdateRunner.RunAsync >>> Download success");

            bool scuOK = ScuVerifier.Validate(
                scuDownloadPath,
                scuSigDownloadPath,
                publicKeyPath,
                latest.HashSha256
            );

            Console.WriteLine("UpdateRunner.RunAsync >>> SCU validate success");

            if (!scuOK)
                return false;

            Console.WriteLine("UpdateRunner.RunAsync >>> Preparing Rollback");


            RollbackUtil.CreatePendingMarker("/opt/sigcore/pending-update");
            string backupRoot = RollbackUtil.CreateBackupRoot("/opt/sigcore/backup");
            RollbackUtil.BackupDirectory("/opt/sigcore", backupRoot);

            try {
                Console.WriteLine("UpdateRunner.RunAsync >>> Starting atomic install");

                string extractRoot = Path.Combine(tempPath, "extract");

                AtomicInstaller.Install(
                    scuDownloadPath,
                    extractRoot
                );

                Console.WriteLine("UpdateRunner.RunAsync >>> exe flags");

                ApplyExecutableFlags(latest.Executables);

                Console.WriteLine("UpdateRunner.RunAsync >>> remove rollback marker");

                RollbackUtil.RemovePendingMarker("/opt/sigcore/pending-update");

                Console.WriteLine("Update complete.");
                return true;
            } catch {
                Console.WriteLine("UpdateRunner.RunAsync >>> rollback initiated");

                RollbackUtil.RestoreDirectory("/opt/sigcore", backupRoot);
                RollbackUtil.RemovePendingMarker("/opt/sigcore/pending-update");
                return false;
            }
        }

        private void ApplyExecutableFlags(List<string> executables) {
            if (executables == null)
                return;

            foreach (string exe in executables) {
                try {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
                        FileName = "/bin/chmod",
                        Arguments = "+x " + exe,
                        UseShellExecute = false,
                        RedirectStandardOutput = false,
                        RedirectStandardError = false
                    });
                } catch { }
            }
        }
    }
}
