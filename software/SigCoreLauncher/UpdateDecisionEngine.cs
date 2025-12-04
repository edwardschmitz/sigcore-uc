using System;
using SigCoreCommon.Update;

namespace SigCoreLauncher {
    public static class UpdateDecisionEngine {
        public enum UpdateDecision {
            NoUpdate,
            UpdateAvailable,
            WrongHardwareRevision,
            ManifestInvalid
        }

        public static UpdateDecision Decide(
            string installedRevision,
            string installedVersion,
            ManifestModel manifest) {
            if (manifest == null)
                return UpdateDecision.ManifestInvalid;

            // revision mismatch
            if (!String.Equals(installedRevision, manifest.HardwareRevision, StringComparison.OrdinalIgnoreCase))
                return UpdateDecision.WrongHardwareRevision;

            if (manifest.Versions == null || manifest.Versions.Count == 0)
                return UpdateDecision.ManifestInvalid;

            ManifestModel.VersionEntry latest = manifest.Versions[0];

            int cmp = VersionComparer.Compare(installedVersion, latest.Version);

            if (cmp >= 0)
                return UpdateDecision.NoUpdate;

            return UpdateDecision.UpdateAvailable;
        }
    }
}
