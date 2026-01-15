using SigCoreCommon;
using SigCoreCommon.Update;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SigCoreLauncher {
    public class Launcher {
        public static Launcher Instance { get; private set; }

        private readonly LogFile log = new LogFile();

        private Process serverProcess;
        private Process bridgeProcess;
        private List<Proc> processes = new List<Proc>();

        private bool pausedForUpdate = false;

        LauncherConfig config;
        DeviceConfig deviceConfig;

        public Launcher() {
            Instance = this;
        }

        public async Task RunAsync(CancellationToken token) {
            log.Info($"Launcher starting. PID={Process.GetCurrentProcess().Id}");
            log.Info($"Launcher user={Environment.UserName}");
            log.Info($"Launcher working dir={Environment.CurrentDirectory}");
            log.Info($"Launcher OS={Environment.OSVersion}");

            HandleRollback();

            log.Info("Launcher: Loading configuration");
            config = LauncherConfig.Load();
            deviceConfig = DeviceConfig.Load();

            log.Info("Launcher: Configuration loaded");
            log.Info($"  Revision={config.Revision}");
            log.Info($"  Version={config.Version}");
            log.Info($"  Executables={string.Join(", ", config.Executables)}");

            log.Info("Launcher: Starting update check");
            await RunUpdateSupervisorAsync(token);

            log.Info("Launcher: Starting core functions");
            RunApps(config.Executables);

            log.Info("Launcher: Entering supervision loop");

            int heartbeat = 0;

            while (!token.IsCancellationRequested) {
                await Task.Delay(2000, token);

                if (++heartbeat % 30 == 0) {
                    log.Info("Launcher: supervision heartbeat");
                }

                foreach (Proc p in processes) {
                    if (!pausedForUpdate && !ProcessAlive(p.proc)) {
                        log.Warn($"Launcher: {p.name} not running. Restarting.");
                        p.proc = StartProcess(p.name);
                    }
                }
            }

            log.Warn("Launcher: Cancellation requested");
            log.Info("Launcher: Shutdown requested. Stopping processes");

            await StopAsync();

            log.Info("Launcher: Supervision loop exited");
        }

        private void HandleRollback() {
            log.Info("Launcher: Checking for failed update rollback");

            if (RollbackUtil.PendingMarkerExists("/opt/sigcore/pending-update")) {
                log.Warn("Launcher: Pending update marker found. Rolling back");

                string latestBackup = RollbackUtil.GetMostRecentBackup("/opt/sigcore/backup");
                if (latestBackup != null) {
                    log.Warn($"Launcher: Restoring backup {latestBackup}");
                    RollbackUtil.RestoreDirectory("/opt/sigcore", latestBackup);
                } else {
                    log.Error("Launcher: No backup found for rollback");
                }

                RollbackUtil.RemovePendingMarker("/opt/sigcore/pending-update");
                log.Info("Launcher: Rollback complete");
            }
        }

        private async Task RunUpdateSupervisorAsync(CancellationToken token) {
            log.Info("Launcher: Creating UpdateSupervisor");

            UpdateSupervisor supervisor = new UpdateSupervisor(
                deviceConfig.ManifestUrl,
                deviceConfig.ManifestSigUrl,
                config.Public,
                config.Revision,
                config.Version,
                config.TempPath
            );

            bool ok = await supervisor.CheckAndApplyUpdatesAsync(token);

            if (ok) {
                log.Info("Launcher: Update check completed successfully");
            } else {
                log.Warn("Launcher: Update check failed or no update applied");
            }
        }

        public void PauseForUpdate() {
            pausedForUpdate = true;
            log.Warn("Launcher: State -> PausedForUpdate");
            StopAsync().Wait();
        }

        public void ResumeAfterUpdate() {
            pausedForUpdate = false;
            log.Info("Launcher: State -> Supervising");
        }

        private void RunApps(string[] apps) {
            log.Info("Launcher: Starting configured applications");

            foreach (string app in apps) {
                Proc p = new Proc();
                p.name = app;
                p.proc = StartProcess(app);
                processes.Add(p);
            }
        }

        private Process StartProcess(string exePath) {
            Process process = null;

            if (!File.Exists(exePath)) {
                log.Error($"Launcher: Missing executable: {exePath}");
            } else {
                ProcessStartInfo psi = new ProcessStartInfo {
                    FileName = exePath,
                    Arguments = "",
                    UseShellExecute = false,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(exePath)
                };

                process = Process.Start(psi);
                log.Info($"Launcher: started {exePath}, pid={process.Id}");
            }

            return process;
        }

        private bool ProcessAlive(Process p) {
            if (p == null) {
                return false;
            }

            try {
                if (p.HasExited) {
                    log.Warn($"Launcher: process exited pid={p.Id}, code={p.ExitCode}");
                    return false;
                }

                return true;
            } catch (Exception ex) {
                log.Error("Launcher: error checking process state", ex);
                return false;
            }
        }

        public async Task StopAsync() {
            log.Info("Launcher: Stopping all child processes");

            foreach (Proc p in processes) {
                if (p.proc != null) {
                    StopProcess(p.proc);
                }
            }

            await Task.Delay(800);
        }

        private void StopProcess(Process p) {
            if (p == null) {
                return;
            }

            try {
                if (!p.HasExited) {
                    log.Info($"Launcher: killing process pid={p.Id}");
                    p.Kill();
                }
            } catch (Exception ex) {
                log.Error("Launcher: error stopping process", ex);
            }
        }

        class Proc {
            public Process proc { get; set; }
            public string name { get; set; }
        }
    }
}
