using SigCoreCommon;
using SigCoreCommon.Update;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SigCoreUpdater {
    public sealed class Updater {
        private const string SigCoreRoot = "/opt/sigcore";
        private const string VersionsRoot = "/opt/sigcore/versions";
        private const string ReleasesPath = "/opt/sigcore/releases.json";
        private const string PublicKeyPath = "/opt/sigcore/keys/sigcore_public.pem";
        private const string TempPath = "/tmp/sigcore";

        private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(6);

        private readonly Log log = new Log();

        public async Task RunAsync(CancellationToken token) {
            Directory.CreateDirectory(SigCoreRoot);
            Directory.CreateDirectory(VersionsRoot);
            Directory.CreateDirectory(TempPath);

            ReleaseConfig currentConfig = ReleaseConfig.Load(
                Path.Combine(AppContext.BaseDirectory, "config.json")
            );

            // One-time tasks belong to the already-active release. Run them once
            // per Updater startup; incomplete tasks retry on the next startup.
            try {
                await new InstallationTaskRunner().RunAsync(currentConfig, token);
            } catch (OperationCanceledException) when (token.IsCancellationRequested) {
                return;
            } catch (Exception ex) {
                log.Error("Installation", "TaskProcessingFailed",
                    "Unable to process installation tasks; normal updates will continue.", ex);
            }

            DeviceConfig deviceConfig = DeviceConfig.Load();
            ReleaseStateStore releaseStore = new ReleaseStateStore(ReleasesPath);

            while (!token.IsCancellationRequested) {
                try {
                    await CheckAndPrepareUpdateAsync(
                        currentConfig,
                        deviceConfig,
                        releaseStore,
                        token
                    );
                } catch (OperationCanceledException) when (token.IsCancellationRequested) {
                    break;
                } catch (Exception ex) {
                    log.Error(
                        "Update",
                        "PreparationFailed",
                        "Update preparation failed; the current release remains active.",
                        ex);
                }

                if (token.IsCancellationRequested)
                    break;

                try {
                    await Task.Delay(CheckInterval, token);
                } catch (OperationCanceledException) when (token.IsCancellationRequested) {
                    break;
                }
            }
        }

        private async Task CheckAndPrepareUpdateAsync(
            ReleaseConfig currentConfig,
            DeviceConfig deviceConfig,
            ReleaseStateStore releaseStore,
            CancellationToken token) {

            string manifestLocal = Path.Combine(TempPath, "manifest.json");
            string manifestSigLocal = Path.Combine(TempPath, "manifest.sig");

            await ManifestDownloader.DownloadAsync(
                deviceConfig.ManifestUrl,
                deviceConfig.ManifestSigUrl,
                manifestLocal,
                manifestSigLocal,
                token
            );

            if (!ManifestValidator.Validate(
                    manifestLocal,
                    manifestSigLocal,
                    PublicKeyPath)) {
                return;
            }

            ManifestModel manifest = ManifestModel.Load(manifestLocal);

            UpdateDecisionEngine.Result decision = UpdateDecisionEngine.Decide(
                currentConfig.Revision,
                manifest
            );

            if (decision.Decision == UpdateDecisionEngine.UpdateDecision.WrongHardwareRevision) {
                log.Warn(
                    "Update",
                    "HardwareRevisionMismatch",
                    "Manifest hardware revision does not match this release.");
                return;
            }

            if (decision.Decision == UpdateDecisionEngine.UpdateDecision.ManifestInvalid) {
                log.Warn(
                    "Update",
                    "ManifestInvalid",
                    "Manifest does not describe a usable target release.");
                return;
            }

            ManifestModel.VersionEntry target = decision.Target;
            string targetVersion = target.Version;

            if (!Version.TryParse(currentConfig.Version, out Version installedVersion) ||
                !Version.TryParse(targetVersion, out Version availableVersion)) {
                log.Warn(
                    "Update",
                    "InvalidVersion",
                    $"Unable to compare installed version {currentConfig.Version} with available version {targetVersion}.");
                return;
            }

            // Automatic updates are confined to the currently installed major
            // version. Crossing a major-version boundary is a deliberate manual
            // system upgrade because it may include Launcher, OS, service, or
            // other compatibility changes outside the normal release payload.
            if (availableVersion.Major != installedVersion.Major) {
                log.Warn(
                    "Update",
                    "ManualMajorUpgradeRequired",
                    $"Automatic update blocked. Installed version is {currentConfig.Version}; " +
                    $"available version is {targetVersion}. A manual system upgrade is required " +
                    $"before {availableVersion.Major}.x automatic updates can resume.");
                return;
            }

            ReleaseEntry existing = releaseStore.FindVersion(targetVersion);

            if (existing != null && existing.Disposition != ReleaseDisposition.Staging) {
                return;
            }

            if (existing == null) {
                releaseStore.AddVersion(targetVersion, ReleaseDisposition.Staging);
            } else {
                releaseStore.SetDisposition(targetVersion, ReleaseDisposition.Staging);
            }

            string releaseDirectory = Path.Combine(VersionsRoot, targetVersion);
            string stagingDirectory = Path.Combine(VersionsRoot, ".staging-" + targetVersion);

            VersionInstaller.CleanIncomplete(releaseDirectory, stagingDirectory);

            string scuLocal = Path.Combine(TempPath, "update.scu");
            string scuSigLocal = Path.Combine(TempPath, "update.scu.sig");

            await ScuVerifier.DownloadAsync(
                deviceConfig.Url,
                deviceConfig.Signature,
                scuLocal,
                scuSigLocal,
                token
            );

            if (!ScuVerifier.Validate(
                    scuLocal,
                    scuSigLocal,
                    PublicKeyPath,
                    target.HashSha256)) {
                log.Error(
                    "Update",
                    "PackageVerificationFailed",
                    $"SCU verification failed for version {targetVersion}.");
                throw new InvalidDataException(
                    $"SCU verification failed for version {targetVersion}.");
            }

            VersionInstaller.Prepare(
                scuLocal,
                stagingDirectory,
                releaseDirectory,
                targetVersion,
                currentConfig.Revision
            );

            releaseStore.SetDisposition(targetVersion, ReleaseDisposition.Ready);

            log.Info(
                "Update",
                "ReleaseReady",
                $"Version {targetVersion} prepared successfully and is READY.");
        }
    }
}
