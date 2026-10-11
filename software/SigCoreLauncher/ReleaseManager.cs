using SigCoreCommon.Update;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace SigCoreLauncher {
    internal sealed class ReleaseManager {
        public const string VersionsRoot = "/opt/sigcore/versions";
        public const string CurrentLinkPath = "/opt/sigcore/current";

        private readonly ReleaseStateStore stateStore;

        public ReleaseManager(ReleaseStateStore stateStore) {
            this.stateStore = stateStore;
        }

        public ReleaseEntry SelectHighestEligible() {
            return SelectHighest(
                ReleaseDisposition.Ready,
                ReleaseDisposition.Good
            );
        }

        public ReleaseEntry SelectHighestGood() {
            return SelectHighest(ReleaseDisposition.Good);
        }

        public void Activate(ReleaseEntry release) {
            if (release == null) {
                throw new ArgumentNullException(nameof(release));
            }

            Version parsedVersion = ParseVersion(release.Version);
            string targetDirectory = Path.Combine(VersionsRoot, parsedVersion.ToString());

            if (!Directory.Exists(targetDirectory)) {
                throw new DirectoryNotFoundException(
                    $"Release directory does not exist: {targetDirectory}");
            }

            EnsureCurrentPathIsSafe();

            string tempLink = CurrentLinkPath + ".new." + Guid.NewGuid().ToString("N");

            try {
                Directory.CreateSymbolicLink(tempLink, targetDirectory);

                if (rename(tempLink, CurrentLinkPath) != 0) {
                    int error = Marshal.GetLastWin32Error();
                    throw new IOException(
                        $"Unable to atomically switch {CurrentLinkPath} to {targetDirectory}. errno={error}");
                }
            } finally {
                TryDeleteLink(tempLink);
            }
        }

        private ReleaseEntry SelectHighest(params ReleaseDisposition[] allowed) {
            HashSet<ReleaseDisposition> allowedSet = allowed.ToHashSet();

            ReleaseEntry? selected = stateStore
                .GetVersions()
                .Where(r => allowedSet.Contains(r.Disposition))
                .OrderByDescending(r => ParseVersion(r.Version))
                .FirstOrDefault();

            if (selected == null) {
                throw new InvalidOperationException(
                    $"No installed release is eligible for selection ({string.Join(", ", allowed)}).");
            }

            return selected;
        }

        private static Version ParseVersion(string value) {
            if (!Version.TryParse(value, out Version? parsed)) {
                throw new InvalidOperationException(
                    $"Invalid installed release version '{value}'.");
            }

            return parsed;
        }

        private static void EnsureCurrentPathIsSafe() {
            if (!Directory.Exists(CurrentLinkPath) && !File.Exists(CurrentLinkPath)) {
                return;
            }

            DirectoryInfo current = new DirectoryInfo(CurrentLinkPath);
            if (current.LinkTarget == null) {
                throw new InvalidOperationException(
                    $"{CurrentLinkPath} exists but is not a symbolic link. Refusing to replace it.");
            }
        }

        private static void TryDeleteLink(string path) {
            try {
                if (File.Exists(path)) {
                    File.Delete(path);
                    return;
                }

                DirectoryInfo info = new DirectoryInfo(path);
                if (info.LinkTarget != null) {
                    info.Delete();
                }
            } catch {
            }
        }

        [DllImport("libc", SetLastError = true, EntryPoint = "rename")]
        private static extern int rename(string oldPath, string newPath);
    }
}
