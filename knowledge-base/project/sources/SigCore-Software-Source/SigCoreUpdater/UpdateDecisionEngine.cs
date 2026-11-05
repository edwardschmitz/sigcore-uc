using SigCoreCommon.Update;
using System;

namespace SigCoreUpdater {
    public static class UpdateDecisionEngine {
        public enum UpdateDecision {
            TargetAvailable,
            WrongHardwareRevision,
            ManifestInvalid
        }

        public sealed class Result {
            public UpdateDecision Decision { get; init; }
            public ManifestModel.VersionEntry Target { get; init; }
        }

        public static Result Decide(
            string installedRevision,
            ManifestModel manifest) {

            if (manifest == null ||
                manifest.Versions == null ||
                manifest.Versions.Count == 0 ||
                string.IsNullOrWhiteSpace(manifest.LatestVersion)) {
                return new Result { Decision = UpdateDecision.ManifestInvalid };
            }

            if (!string.Equals(
                    installedRevision,
                    manifest.HardwareRevision,
                    StringComparison.OrdinalIgnoreCase)) {
                return new Result { Decision = UpdateDecision.WrongHardwareRevision };
            }

            ManifestModel.VersionEntry target = null;

            foreach (ManifestModel.VersionEntry version in manifest.Versions) {
                if (string.Equals(
                        version.Version,
                        manifest.LatestVersion,
                        StringComparison.OrdinalIgnoreCase)) {
                    target = version;
                    break;
                }
            }

            if (target == null || string.IsNullOrWhiteSpace(target.Version)) {
                return new Result { Decision = UpdateDecision.ManifestInvalid };
            }

            // Reject malformed version strings now rather than silently treating
            // bad components as zero.
            if (!Version.TryParse(target.Version, out _)) {
                return new Result { Decision = UpdateDecision.ManifestInvalid };
            }

            return new Result {
                Decision = UpdateDecision.TargetAvailable,
                Target = target
            };
        }
    }
}
