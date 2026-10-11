using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SigCoreUpdater {
    public static class MigrationRunner {
        public const string MigrationRelativePath = "migration/SigCoreMigration";

        public sealed class Result {
            public int ExitCode { get; init; }
            public List<string> Notes { get; init; } = new List<string>();
        }

        public static async Task<Result> RunAsync(
            string releaseDirectory,
            string fromVersion,
            CancellationToken token) {

            string migrationPath = Path.GetFullPath(
                Path.Combine(releaseDirectory, MigrationRelativePath)
            );

            string releaseRoot = Path.GetFullPath(releaseDirectory) + Path.DirectorySeparatorChar;
            if (!migrationPath.StartsWith(releaseRoot, StringComparison.Ordinal)) {
                throw new InvalidDataException("Migration entry point escapes release directory.");
            }

            if (!File.Exists(migrationPath)) {
                throw new FileNotFoundException(
                    "Environment-changing release is missing its migration entry point.",
                    migrationPath
                );
            }

            MakeExecutable(migrationPath);

            ProcessStartInfo psi = new ProcessStartInfo {
                FileName = migrationPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = releaseDirectory
            };
            psi.ArgumentList.Add("--from");
            psi.ArgumentList.Add(fromVersion);

            using Process process = new Process { StartInfo = psi };
            if (!process.Start()) {
                throw new InvalidOperationException(
                    $"Unable to start migration entry point: {migrationPath}"
                );
            }

            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
            Task<string> stderrTask = process.StandardError.ReadToEndAsync();

            try {
                await process.WaitForExitAsync(token);
            } catch (OperationCanceledException) {
                try {
                    if (!process.HasExited) {
                        process.Kill(entireProcessTree: true);
                    }
                } catch {
                    // The updater is shutting down. Preserve STAGING and retry later.
                }

                throw;
            }

            string stdout = await stdoutTask;
            string stderr = await stderrTask;

            List<string> notes = new List<string>();
            AddLines(notes, stdout);
            AddLines(notes, stderr);

            return new Result {
                ExitCode = process.ExitCode,
                Notes = notes
            };
        }

        private static void AddLines(List<string> notes, string text) {
            if (string.IsNullOrWhiteSpace(text)) {
                return;
            }

            using StringReader reader = new StringReader(text);
            while (true) {
                string? line = reader.ReadLine();
                if (line == null) {
                    break;
                }

                if (!string.IsNullOrWhiteSpace(line)) {
                    notes.Add(line.TrimEnd());
                }
            }
        }

        private static void MakeExecutable(string path) {
            if (OperatingSystem.IsWindows()) {
                return;
            }

            ProcessStartInfo psi = new ProcessStartInfo {
                FileName = "/bin/chmod",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("+x");
            psi.ArgumentList.Add(path);

            using Process? process = Process.Start(psi);
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
