using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SigCoreCommon.Update;
using System.Diagnostics;

namespace SigCoreLauncher {
    public class UpdateSupervisor {

        private readonly string manifestUrl;
        private readonly string manifestSigUrl;
        private readonly string publicKeyPath;

        private readonly string installedRevision;
        private readonly string installedVersion;

        private readonly string tempPath;

        public UpdateSupervisor(
            string manifestUrl,
            string manifestSigUrl,
            string publicKeyPath,
            string installedRevision,
            string installedVersion,
            string tempPath
            ) {

            this.manifestUrl = manifestUrl;
            this.manifestSigUrl = manifestSigUrl;
            this.publicKeyPath = publicKeyPath;

            this.installedRevision = installedRevision;
            this.installedVersion = installedVersion;
            this.tempPath = tempPath;
        }

        public async Task<bool> CheckAndApplyUpdatesAsync(CancellationToken token) {

            Console.WriteLine("UpdateSupervisor: Checking installed version.");
            Console.WriteLine("  Installed Revision: " + installedRevision);
            Console.WriteLine("  Installed Version: " + installedVersion);
            Console.WriteLine("  Temp Path: " + tempPath);

            if (!Directory.Exists(tempPath))
                Directory.CreateDirectory(tempPath);

            string manifestLocal = Path.Combine(tempPath, "manifest.json");
            string manifestSigLocal = Path.Combine(tempPath, "manifest.sig");

            Console.WriteLine("UpdateSupervisor: Downloading manifest.");

            await ManifestDownloader.DownloadAsync(
                manifestUrl,
                manifestSigUrl,
                manifestLocal,
                manifestSigLocal,
                token
            );

            Console.WriteLine("UpdateSupervisor: Validating manifest.");

            bool manifestValid = ManifestValidator.Validate(
                manifestLocal,
                manifestSigLocal,
                publicKeyPath
            );

            if (!manifestValid) {
                Console.WriteLine("UpdateSupervisor: Manifest validation failed.");
                return false;
            }

            ManifestModel manifest = ManifestModel.Load(manifestLocal);

            Console.WriteLine("UpdateSupervisor: Deciding update.");

            UpdateDecisionEngine.UpdateDecision decision =
                UpdateDecisionEngine.Decide(installedRevision, installedVersion, manifest);

            if (decision == UpdateDecisionEngine.UpdateDecision.NoUpdate) {
                Console.WriteLine("UpdateSupervisor: No update needed.");
                return true;
            }

            if (decision == UpdateDecisionEngine.UpdateDecision.WrongHardwareRevision) {
                Console.WriteLine("UpdateSupervisor: Hardware revision mismatch.");
                return false;
            }

            Console.WriteLine("UpdateSupervisor: Update available.");

            Launcher.Instance.PauseForUpdate();

            UpdateRunner runner = new UpdateRunner(
                manifestUrl,
                manifestSigUrl,
                publicKeyPath,
                tempPath
            );

            bool ok = await runner.RunAsync(token);

            Launcher.Instance.ResumeAfterUpdate();

            if (!ok) {
                Console.WriteLine("UpdateSupervisor: Update failed.");
                return false;
            }

            Console.WriteLine("UpdateSupervisor: Update applied successfully.");

            if (manifest.RequiresReboot) {
                Console.WriteLine("UpdateSupervisor: Reboot required. Rebooting now...");
                Process.Start("/sbin/reboot");
            }

            return true;
        }
    }
}
