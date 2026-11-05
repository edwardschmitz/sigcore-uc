using SigCoreCommon;
using SigCoreCommon.Update;
using System;

namespace SigCoreUpdater {
    public static class ManifestValidator {
        private static readonly Log log = new Log();

        public static bool Validate(
            string manifestPath,
            string manifestSigPath,
            string publicKeyPemPath) {

            bool sigOk = SignatureHelper.VerifyFile(
                publicKeyPemPath,
                manifestPath,
                manifestSigPath
            );

            if (!sigOk) {
                log.Error(
                    "Update",
                    "ManifestSignatureInvalid",
                    "Manifest signature verification failed.");
                return false;
            }

            ManifestModel manifest;
            try {
                manifest = ManifestModel.Load(manifestPath);
            } catch (Exception ex) {
                log.Error(
                    "Update",
                    "ManifestLoadFailed",
                    "Manifest could not be loaded.",
                    ex);
                return false;
            }

            if (string.IsNullOrWhiteSpace(manifest.HardwareRevision)) {
                log.Error("Update", "ManifestInvalid", "Manifest missing HardwareRevision.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(manifest.LatestVersion)) {
                log.Error("Update", "ManifestInvalid", "Manifest missing LatestVersion.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(manifest.PublicKeyFingerprint)) {
                log.Error("Update", "ManifestInvalid", "Manifest missing PublicKeyFingerprint.");
                return false;
            }

            if (manifest.Versions == null || manifest.Versions.Count == 0) {
                log.Error("Update", "ManifestInvalid", "Manifest contains no Versions.");
                return false;
            }

            foreach (ManifestModel.VersionEntry version in manifest.Versions) {
                if (string.IsNullOrWhiteSpace(version.Version)) {
                    log.Error("Update", "ManifestInvalid", "Manifest version entry missing Version.");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(version.ReleaseDate)) {
                    log.Error(
                        "Update",
                        "ManifestInvalid",
                        $"Manifest version {version.Version} missing ReleaseDate.");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(version.HashSha256)) {
                    log.Error(
                        "Update",
                        "ManifestInvalid",
                        $"Manifest version {version.Version} missing HashSha256.");
                    return false;
                }
            }

            string localFingerprint = FingerprintHelper.Compute(publicKeyPemPath);

            if (!string.Equals(
                    manifest.PublicKeyFingerprint,
                    localFingerprint,
                    StringComparison.OrdinalIgnoreCase)) {
                log.Error(
                    "Update",
                    "PublicKeyFingerprintMismatch",
                    "Manifest public key fingerprint does not match the installed key.");
                return false;
            }

            return true;
        }
    }
}
