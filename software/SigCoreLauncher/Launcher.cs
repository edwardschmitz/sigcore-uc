using SigCoreCommon;
using SigCoreCommon.Update;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SigCoreLauncher {
    public class Launcher {
        private const int ProbationSeconds = 30;
        private const string CurrentConfigPath = "/opt/sigcore/current/config.json";

        private readonly Log log = new Log();
        private readonly ReleaseStateStore releaseStateStore = new ReleaseStateStore();
        private readonly ReleaseManager releaseManager;
        private readonly List<Proc> processes = new List<Proc>();

        private LauncherConfig config;
        private ReleaseEntry activeRelease;
        private bool releaseTransitionInProgress;

        public Launcher() {
            releaseManager = new ReleaseManager(releaseStateStore);
        }

        public async Task RunAsync(CancellationToken token) {


            await StartSelectedReleaseAsync(token);

            while (!token.IsCancellationRequested) {
                try {
                    await Task.Delay(2000, token);
                } catch (OperationCanceledException) when (token.IsCancellationRequested) {
                    break;
                }

                if (releaseTransitionInProgress) {
                    continue;
                }

                foreach (Proc p in processes) {
                    if (!ProcessAlive(p.Process)) {
                        string processName = Path.GetFileName(p.ConfiguredPath);


                        p.Process = StartProcess(p.ConfiguredPath);

                        if (ProcessAlive(p.Process)) {
                            log.Warn(
                                "Process",
                                "Restarted",
                                $"{p.ConfiguredPath} exited and was restarted.");
                        } else {
                            log.Error(
                                "Process",
                                "RestartFailed",
                                $"{p.ConfiguredPath} exited and could not be restarted.");
                        }
                    }
                }
            }


            await StopAsync();
        }


        private async Task StartSelectedReleaseAsync(CancellationToken token) {
            ReleaseEntry selected = releaseManager.SelectHighestEligible();
            bool requiresProbation = selected.Disposition == ReleaseDisposition.Ready;

            await ActivateReleaseAsync(selected, token, requiresProbation);
        }

        private async Task ActivateReleaseAsync(
            ReleaseEntry release,
            CancellationToken token,
            bool runProbation) {

            releaseTransitionInProgress = true;

            try {
                try {
                    await StopAllProcessesAsync();

                    // The Launcher selects a release only by changing the release-level
                    // current symlink. It does not know or manipulate release internals.
                    releaseManager.Activate(release);
                    activeRelease = release;

                    config = LauncherConfig.Load(CurrentConfigPath);
                    ValidateSelectedConfig(config, release);


                    RunApps(config.Executables);

                    if (runProbation) {
                        bool passed = await RunProbationAsync(token);

                        if (!passed) {
                            await FailAndFallbackAsync(
                                release,
                                token,
                                "probation process failure");
                            return;
                        }

                        releaseStateStore.SetDisposition(
                            release.Version,
                            ReleaseDisposition.Good,
                            "probation passed"
                        );

                        release.Disposition = ReleaseDisposition.Good;
                        log.Info(
                            "Release",
                            "ProbationPassed",
                            $"Release {release.Version} passed probation and is now GOOD.");
                    }

                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) {
                    throw;
                }
                catch (Exception ex) when (runProbation) {
                    // A READY release that cannot even complete activation has failed
                    // probation just as surely as one whose process exits during it.
                    log.Error(
                        "Release",
                        "ActivationFailed",
                        $"Release {release.Version} failed during activation.",
                        ex);

                    await FailAndFallbackAsync(
                        release,
                        token,
                        $"activation failure: {ex.GetType().Name}");
                }
            } finally {
                releaseTransitionInProgress = false;
            }
        }

        private async Task<bool> RunProbationAsync(CancellationToken token) {

            for (int second = 0; second < ProbationSeconds; second++) {
                // Cancellation is a system shutdown, not evidence that the release failed.
                await Task.Delay(1000, token);

                foreach (Proc p in processes) {
                    if (!ProcessAlive(p.Process)) {
                        log.Error(
                            "Release",
                            "ProbationFailed",
                            $"Probation failed because {p.ConfiguredPath} exited.");
                        return false;
                    }
                }
            }

            return true;
        }

        private async Task FailAndFallbackAsync(
            ReleaseEntry failedRelease,
            CancellationToken token,
            string reason) {

            log.Error(
                "Release",
                "ReleaseFailed",
                $"Release {failedRelease.Version} failed probation: {reason}");



            await StopAllProcessesAsync();

            releaseStateStore.SetDisposition(
                failedRelease.Version,
                ReleaseDisposition.Failed,
                reason
            );

            ReleaseEntry fallback = releaseManager.SelectHighestGood();

            log.Warn(
                "Release",
                "Fallback",
                $"Falling back to last known GOOD release {fallback.Version}.");

            // Fallback is a whole-release transition. Nothing from the failed
            // release is restarted after this point.
            releaseManager.Activate(fallback);
            activeRelease = fallback;

            config = LauncherConfig.Load(CurrentConfigPath);
            ValidateSelectedConfig(config, fallback);


            RunApps(config.Executables);

            // Give the fallback release one supervision interval to prove that
            // every configured process actually started. This is not probation;
            // the release is already known GOOD.
            try {
                await Task.Delay(2000, token);
            } catch (OperationCanceledException) when (token.IsCancellationRequested) {
                return;
            }

            foreach (Proc p in processes) {
                if (!ProcessAlive(p.Process)) {
                    throw new InvalidOperationException(
                        $"Fallback release {fallback.Version} failed to start {p.ConfiguredPath}.");
                }
            }

        }

        private static void ValidateSelectedConfig(
            LauncherConfig selectedConfig,
            ReleaseEntry selectedRelease) {

            if (selectedConfig == null) {
                throw new InvalidOperationException(
                    $"Selected release {selectedRelease.Version} has no usable config.json.");
            }

            if (!string.Equals(
                    selectedConfig.Version,
                    selectedRelease.Version,
                    StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException(
                    $"Selected release state says {selectedRelease.Version}, " +
                    $"but current/config.json says {selectedConfig.Version}.");
            }

            if (selectedConfig.Executables == null || selectedConfig.Executables.Length == 0) {
                throw new InvalidOperationException(
                    $"Release {selectedRelease.Version} contains no configured executables.");
            }

            foreach (string executable in selectedConfig.Executables) {
                if (string.IsNullOrWhiteSpace(executable)) {
                    throw new InvalidOperationException(
                        $"Release {selectedRelease.Version} contains an empty executable entry.");
                }

                if (Path.IsPathRooted(executable)) {
                    throw new InvalidOperationException(
                        $"Release executable paths must be relative. Invalid entry: {executable}");
                }

                string fullPath = ResolveCurrentPath(executable);
                if (!File.Exists(fullPath)) {
                    throw new FileNotFoundException(
                        $"Configured executable is missing: {fullPath}",
                        fullPath);
                }
            }
        }

        private void RunApps(string[] apps) {
            processes.Clear();

            foreach (string app in apps) {
                Proc p = new Proc {
                    ConfiguredPath = app,
                    Process = StartProcess(app)
                };

                processes.Add(p);
            }
        }

        private Process StartProcess(string configuredPath) {
            string exePath = ResolveCurrentPath(configuredPath);

            if (!File.Exists(exePath)) {
                log.Error(
                    "Process",
                    "MissingExecutable",
                    $"Configured executable is missing: {exePath}");
                return null;
            }

            ProcessStartInfo psi = new ProcessStartInfo {
                FileName = exePath,
                Arguments = "",
                UseShellExecute = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                CreateNoWindow = true,
                WorkingDirectory = ReleaseManager.CurrentLinkPath
            };

            Process process = Process.Start(psi);
            return process;
        }

        private static string ResolveCurrentPath(string relativePath) {
            if (Path.IsPathRooted(relativePath)) {
                throw new InvalidOperationException(
                    $"Release path must be relative: {relativePath}");
            }

            string currentRoot = Path.GetFullPath(ReleaseManager.CurrentLinkPath);
            string resolved = Path.GetFullPath(
                Path.Combine(ReleaseManager.CurrentLinkPath, relativePath));

            string prefix = currentRoot.EndsWith(Path.DirectorySeparatorChar)
                ? currentRoot
                : currentRoot + Path.DirectorySeparatorChar;

            if (!resolved.StartsWith(prefix, StringComparison.Ordinal)) {
                throw new InvalidOperationException(
                    $"Release path escapes the current release: {relativePath}");
            }

            return resolved;
        }

        private bool ProcessAlive(Process p) {
            if (p == null) {
                return false;
            }

            try {
                if (p.HasExited) {
                    return false;
                }

                return true;
            } catch (Exception ex) {
                log.Error(
                    "Process",
                    "StateCheckFailed",
                    "Unable to determine child-process state.",
                    ex);
                return false;
            }
        }

        public async Task StopAsync() {
            await StopAllProcessesAsync();
        }

        private async Task StopAllProcessesAsync() {
            if (processes.Count == 0) {
                return;
            }

            foreach (Proc p in processes) {
                if (p.Process != null) {
                    StopProcess(p.Process);
                }
            }

            processes.Clear();
            await Task.Delay(800);
        }

        private void StopProcess(Process p) {
            if (p == null) {
                return;
            }

            try {
                if (!p.HasExited) {
                    p.Kill(entireProcessTree: true);
                    p.WaitForExit(5000);
                }
            } catch (Exception ex) {
                log.Error(
                    "Process",
                    "StopFailed",
                    "Unable to stop a child process cleanly.",
                    ex);
            } finally {
                p.Dispose();
            }
        }

        private sealed class Proc {
            public Process Process { get; set; }
            public string ConfiguredPath { get; set; }
        }
    }
}
