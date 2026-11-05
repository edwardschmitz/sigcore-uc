using System;
using System.IO;

namespace SigCoreUpdater {
    public static class ReleaseVersionPolicy {
        public enum ReleaseKind {
            Minor,
            Environment,
            Major
        }

        public static ReleaseKind Classify(string versionText) {
            if (!Version.TryParse(versionText, out Version version) || version.Build < 0) {
                throw new InvalidDataException(
                    $"Release version must use x.y.z format: {versionText}"
                );
            }

            if (version.Minor == 0 && version.Build == 0) {
                return ReleaseKind.Major;
            }

            if (version.Build == 0) {
                return ReleaseKind.Environment;
            }

            return ReleaseKind.Minor;
        }

        public static bool RequiresMigration(string versionText) {
            ReleaseKind kind = Classify(versionText);
            return kind == ReleaseKind.Environment || kind == ReleaseKind.Major;
        }
    }
}
