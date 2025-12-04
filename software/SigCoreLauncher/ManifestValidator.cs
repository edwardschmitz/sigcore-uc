using System;
using SigCoreCommon.Update;

namespace SigCoreLauncher {
    public static class ManifestValidator {

        public static bool Validate(
            string manifestPath,
            string manifestSigPath,
            string publicKeyPemPath) {

            Console.WriteLine("VerifyFile");
            bool sigOk = SignatureHelper.VerifyFile(publicKeyPemPath, manifestPath, manifestSigPath);
            if (!sigOk)
                return false;

            ManifestModel manifest = ManifestModel.Load(manifestPath);

            if (string.IsNullOrWhiteSpace(manifest.HardwareRevision))
                return false;
            if (string.IsNullOrWhiteSpace(manifest.LatestVersion))
                return false;
            if (string.IsNullOrWhiteSpace(manifest.PublicKeyFingerprint))
                return false;
            if (manifest.Versions == null || manifest.Versions.Count == 0)
                return false;

            foreach (var v in manifest.Versions) {
                if (string.IsNullOrWhiteSpace(v.Version))
                    return false;
                if (string.IsNullOrWhiteSpace(v.ReleaseDate))
                    return false;
                if (string.IsNullOrWhiteSpace(v.HashSha256))
                    return false;
                if (string.IsNullOrWhiteSpace(v.Signature))
                    return false;
                if (string.IsNullOrWhiteSpace(v.Url))
                    return false;
            }

            string localFingerprint = FingerprintHelper.Compute(publicKeyPemPath);

            Console.WriteLine("Local Fingerprint");
            if (!string.Equals(manifest.PublicKeyFingerprint, localFingerprint, StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }
    }
}
