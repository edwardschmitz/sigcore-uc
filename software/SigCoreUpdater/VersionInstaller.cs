using SigCoreCommon.Update;
using System;
using System.Diagnostics;
using System.IO;

namespace SigCoreUpdater {
    public static class VersionInstaller {
        public static void CleanIncomplete(
            string releaseDirectory,
            string stagingDirectory) {

            if (Directory.Exists(stagingDirectory)) {
                Directory.Delete(stagingDirectory, true);
            }

            // A version whose disposition is STAGING is not a valid installed release.
            // If a final directory exists for that same STAGING version, it is residue
            // from an interrupted preparation and must be rebuilt from the signed SCU.
            if (Directory.Exists(releaseDirectory)) {
                Directory.Delete(releaseDirectory, true);
            }
        }

        public static void Prepare(
            string scuFile,
            string stagingDirectory,
            string releaseDirectory,
            string expectedVersion,
            string expectedRevision) {

            if (Directory.Exists(stagingDirectory)) {
                Directory.Delete(stagingDirectory, true);
            }

            Directory.CreateDirectory(stagingDirectory);
            ScuArchive.Extract(scuFile, stagingDirectory);

            string configPath = Path.Combine(stagingDirectory, "config.json");
            ReleaseConfig config = ReleaseConfig.Load(configPath);

            if (!string.Equals(
                    config.Version,
                    expectedVersion,
                    StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidDataException(
                    $"Package version mismatch. Manifest={expectedVersion}, config={config.Version}"
                );
            }

            if (!string.Equals(
                    config.Revision,
                    expectedRevision,
                    StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidDataException(
                    $"Package revision mismatch. Expected={expectedRevision}, config={config.Revision}"
                );
            }

            foreach (string relativeExecutable in config.Executables) {
                string executable = Path.GetFullPath(
                    Path.Combine(stagingDirectory, relativeExecutable)
                );

                string stagingRoot = Path.GetFullPath(stagingDirectory) + Path.DirectorySeparatorChar;
                if (!executable.StartsWith(stagingRoot, StringComparison.Ordinal)) {
                    throw new InvalidDataException(
                        $"Executable escapes release directory: {relativeExecutable}"
                    );
                }

                if (!File.Exists(executable)) {
                    throw new FileNotFoundException(
                        "Release executable not found.",
                        executable
                    );
                }

                MakeExecutable(executable);
            }

            if (Directory.Exists(releaseDirectory)) {
                throw new IOException(
                    $"Final release directory already exists: {releaseDirectory}"
                );
            }

            // stagingDirectory and releaseDirectory are both beneath /opt/sigcore/versions,
            // so this rename stays on the same filesystem and publishes the completed
            // release directory as one directory-entry operation.
            Directory.Move(stagingDirectory, releaseDirectory);
        }

        private static void MakeExecutable(string path) {
            if (OperatingSystem.IsWindows()) {
                return;
            }

            ProcessStartInfo psi = new ProcessStartInfo {
                FileName = "/bin/chmod",
                Arguments = "+x \"" + path + "\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using Process process = Process.Start(psi);
            if (process == null) {
                throw new InvalidOperationException("Unable to start chmod for " + path);
            }

            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0) {
                throw new InvalidOperationException(
                    $"chmod failed for {path}. ExitCode={process.ExitCode}. {stderr} {stdout}".Trim()
                );
            }
        }
    }
}
