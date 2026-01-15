using System;
using SigCoreCommon;
using SigCoreCommon.Update;

namespace SigCoreLauncher {
    public static class ManifestValidator {
        public static bool Validate(
            string manifestPath,
            string manifestSigPath,
            string publicKeyPemPath) {
            bool isValid = true;
            LogFile log = new LogFile();

            log.Info("Starting manifest validation");

            bool sigOk = SignatureHelper.VerifyFile(
                publicKeyPemPath,
                manifestPath,
                manifestSigPath
            );

            if (!sigOk) {
                log.Error("Manifest signature verification failed");
                isValid = false;
            }

            ManifestModel manifest = null;

            if (isValid) {
                manifest = ManifestModel.Load(manifestPath);
                log.Info("Manifest loaded successfully");
            }

            if (isValid && string.IsNullOrWhiteSpace(manifest.HardwareRevision)) {
                log.Error("Manifest missing HardwareRevision");
                isValid = false;
            }

            if (isValid && string.IsNullOrWhiteSpace(manifest.LatestVersion)) {
                log.Error("Manifest missing LatestVersion");
                isValid = false;
            }

            if (isValid && string.IsNullOrWhiteSpace(manifest.PublicKeyFingerprint)) {
                log.Error("Manifest missing PublicKeyFingerprint");
                isValid = false;
            }

            if (isValid && (manifest.Versions == null || manifest.Versions.Count == 0)) {
                log.Error("Manifest contains no Versions");
                isValid = false;
            }

            if (isValid) {
                foreach (var v in manifest.Versions) {
                    if (string.IsNullOrWhiteSpace(v.Version)) {
                        log.Error("Manifest version entry missing Version");
                        isValid = false;
                        break;
                    }

                    if (string.IsNullOrWhiteSpace(v.ReleaseDate)) {
                        log.Error($"Manifest version {v.Version} missing ReleaseDate");
                        isValid = false;
                        break;
                    }

                    if (string.IsNullOrWhiteSpace(v.HashSha256)) {
                        log.Error($"Manifest version {v.Version} missing HashSha256");
                        isValid = false;
                        break;
                    }
                }
            }

            if (isValid) {
                string localFingerprint = FingerprintHelper.Compute(publicKeyPemPath);

                log.Info("Computed local public key fingerprint");

                if (!string.Equals(
                        manifest.PublicKeyFingerprint,
                        localFingerprint,
                        StringComparison.OrdinalIgnoreCase)) {
                    log.Error(
                        $"Public key fingerprint mismatch. " +
                        $"Manifest={manifest.PublicKeyFingerprint}, Local={localFingerprint}"
                    );
                    isValid = false;
                }
            }

            if (isValid) {
                log.Info("Manifest validation succeeded");
            } else {
                log.Warn("Manifest validation failed");
            }

            return isValid;
        }
    }
}
