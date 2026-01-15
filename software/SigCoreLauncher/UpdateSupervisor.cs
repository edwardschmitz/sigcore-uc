using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SigCoreCommon.Update;
using System.Diagnostics;
using SigCoreCommon;

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
            string tempPath) {
            LogFile log = new LogFile();

            log.Info("Initializing UpdateSupervisor");
            log.Info($"manifestUrl        = '{manifestUrl}'");
            log.Info($"manifestSigUrl     = '{manifestSigUrl}'");
            log.Info($"publicKeyPath      = '{publicKeyPath}'");
            log.Info($"installedRevision  = '{installedRevision}'");
            log.Info($"installedVersion   = '{installedVersion}'");
            log.Info($"tempPath           = '{tempPath}'");

            this.manifestUrl = manifestUrl;
            this.manifestSigUrl = manifestSigUrl;
            this.publicKeyPath = publicKeyPath;

            this.installedRevision = installedRevision;
            this.installedVersion = installedVersion;
            this.tempPath = tempPath;
        }

        public async Task<bool> CheckAndApplyUpdatesAsync(CancellationToken token) {

            LogFile log = new LogFile();
            log.Info("UpdateSupervisor: Checking installed version.");
            log.Info("  Installed Revision: " + installedRevision);
            log.Info("  Installed Version: " + installedVersion);
            log.Info("  Temp Path: " + tempPath);

            if (!Directory.Exists(tempPath))
                Directory.CreateDirectory(tempPath);

            string manifestLocal = Path.Combine(tempPath, "manifest.json");
            string manifestSigLocal = Path.Combine(tempPath, "manifest.sig");

            log.Info("UpdateSupervisor: Downloading manifest.");

            log.Info($"   {manifestUrl}");
            log.Info($"   {manifestSigUrl}");
            log.Info($"   {manifestLocal}");
            log.Info($"   {manifestSigLocal}");

            await ManifestDownloader.DownloadAsync(
                manifestUrl,
                manifestSigUrl,
                manifestLocal,
                manifestSigLocal,
                token
            );

            log.Info("UpdateSupervisor: Validating manifest.");
            log.Info($"    manifestLocal: {manifestLocal}\n    manifestSigLocal: {manifestSigLocal}");

            bool manifestValid = ManifestValidator.Validate(
                manifestLocal,
                manifestSigLocal,
                publicKeyPath
            );

            if (!manifestValid) {
                log.Info("UpdateSupervisor: Manifest validation failed.");
                return false;
            }

            ManifestModel manifest = ManifestModel.Load(manifestLocal);

            log.Info("UpdateSupervisor: Deciding update.");

            UpdateDecisionEngine.UpdateDecision decision =
                UpdateDecisionEngine.Decide(installedRevision, installedVersion, manifest);

            if (decision == UpdateDecisionEngine.UpdateDecision.NoUpdate) {
                log.Info("UpdateSupervisor: No update needed.");
                return true;
            }

            if (decision == UpdateDecisionEngine.UpdateDecision.WrongHardwareRevision) {
                log.Info("UpdateSupervisor: Hardware revision mismatch.");
                return false;
            }

            log.Info("UpdateSupervisor: Update available.");

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
                log.Info("UpdateSupervisor: Update failed.");
                return false;
            }

            log.Info("UpdateSupervisor: Update applied successfully.");

            if (manifest.RequiresReboot) {
                log.Info("UpdateSupervisor: Update applied. Exiting to allow launcher restart.");
                Environment.Exit(0);
            }

            return true;
        }
    }
}
