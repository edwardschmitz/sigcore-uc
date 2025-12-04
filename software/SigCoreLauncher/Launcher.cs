using SigCoreCommon.Update;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SigCoreLauncher {
    public class Launcher {

        public static Launcher Instance { get; private set; }

        private Process serverProcess;
        private Process bridgeProcess;
        private List<Proc> processes = new List<Proc>();

        private bool pausedForUpdate = false;

        LauncherConfig config;

        public Launcher() {
            Instance = this;
        }

        public async Task RunAsync(CancellationToken token) {
            HandleRollback();

            config = LauncherConfig.Load();

            Console.WriteLine("Launcher: Starting update check.");
            await RunUpdateSupervisorAsync(token);


            Console.WriteLine("Launcher: Starting core functions.");

            RunApps(config.Executables);

            Console.WriteLine("Launcher: Entering supervision loop.");

            while (!token.IsCancellationRequested) {
                await Task.Delay(2000, token);

                foreach (Proc p in processes) {
                    if (!pausedForUpdate && !ProcessAlive(p.proc)) {
                        Console.WriteLine($"Launcher: {p.name} not running. Restarting.");
                        p.proc = StartProcess(p.name);
                    }
                }
            }

            Console.WriteLine("Launcher: Shutdown requested. Stopping processes.");
            await StopAsync();
            Console.WriteLine("Launcher: Supervision loop exited.");
        }

        private void HandleRollback() {
            Console.WriteLine("Launcher: Check failed update");
            if (RollbackUtil.PendingMarkerExists("/opt/sigcore/pending-update")) {
                Console.WriteLine("Launcher: Failed update found. Rolling back.");

                string latestBackup = RollbackUtil.GetMostRecentBackup("/opt/sigcore/backup");
                if (latestBackup != null) {
                    RollbackUtil.RestoreDirectory("/opt/sigcore", latestBackup);
                }

                RollbackUtil.RemovePendingMarker("/opt/sigcore/pending-update");
                Console.WriteLine("Launcher: Rollback complete.");
            }
        }

        private async Task RunUpdateSupervisorAsync(CancellationToken token) {
            UpdateSupervisor supervisor = new UpdateSupervisor(
                config.ManifestUrl,
                config.ManifestSigUrl,
                config.Public,
                config.Revision,
                config.Version,
                config.TempPath
                );

            bool ok = await supervisor.CheckAndApplyUpdatesAsync(token);

            if (ok)
                Console.WriteLine("Launcher: Update check complete.");
            else
                Console.WriteLine("Launcher: Update check failed or no update applied.");
        }

        public void PauseForUpdate() {
            pausedForUpdate = true;
            StopAsync().Wait();
        }

        public void ResumeAfterUpdate() {
            pausedForUpdate = false;
        }

        private void RunApps(string[] apps) {
            foreach (string app in apps) {
                Proc p = new Proc();
                p.proc = StartProcess(app);
                p.name = app;
                processes.Add(p);
            }
        }

        private Process StartProcess(string exePath) {
            Process process = null;

            if (!File.Exists(exePath)) {
                Console.WriteLine("Launcher: Missing executable: " + exePath);
            } else {

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = exePath;
                psi.Arguments = "";
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = false;
                psi.RedirectStandardError = false;
                psi.CreateNoWindow = true;
                psi.WorkingDirectory = Path.GetDirectoryName(exePath);

                process = Process.Start(psi);
                Console.WriteLine($"Launcher: started {exePath}, pid={process.Id}");
            }
            return process;
        }

        private bool ProcessAlive(Process p) {
            if (p == null)
                return false;

            try {
                return !p.HasExited;
            } catch {
                return false;
            }
        }

        public async Task StopAsync() {
            foreach (Proc p in processes) {
                if (p.proc != null) {
                    StopProcess(p.proc);
                }
            }
            await Task.Delay(800);
        }

        private void StopProcess(Process p) {
            if (p == null)
                return;

            try {
                if (!p.HasExited)
                    p.Kill();
            } catch {
            }
        }

        class Proc {
            public Process proc { get; set; }
            public string name { get; set; }
        }
    }
}
