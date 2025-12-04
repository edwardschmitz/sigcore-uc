//using System;
//using System.Diagnostics;
//using System.IO;
//using System.Threading.Tasks;
//using Newtonsoft.Json;
//using SigCoreCommon.Update;

//namespace SigCoreLauncher {

//    public class LauncherManager {

//        private readonly string PublicKeyPath = "/opt/sigcore/keys/sigcore_public.pem";
//        private readonly string ServerPath = "/opt/sigcore/sigcoreserver";
//        private readonly string BridgePath = "/opt/sigcore/WebBridge";
//        private readonly string IdentityPath = "/opt/sigcore/identity.json";

//        private Process serverProcess;
//        private Process bridgeProcess;

//        public async Task RunAsync() {

//            Console.CancelKeyPress += (sender, e) => {
//                e.Cancel = true;
//                Console.WriteLine("Ctrl+C received — shutting down launcher...");
//                StopAll();
//                Environment.Exit(0);
//            };

//            try {
//                Console.WriteLine("Launcher starting...");

//                LauncherConfig config = new LauncherConfig();

//                if (!File.Exists(config.Public)) {
//                    Console.WriteLine($"ERROR: Missing {config.Public}");
//                    return;
//                }

//                string publicKey = File.ReadAllText(config.Public);

//                // -----------------------------
//                // RUN UPDATE SUPERVISOR
//                // -----------------------------
//                UpdateSupervisor supervisor = new UpdateSupervisor(
//                    config.ManifestUrl,
//                    config.ManifestSigUrl,
//                    config.Public,
//                    config.Revision,
//                    config.Version,
//                    config.TempPath
//                );

//                Console.WriteLine("Checking for updates...");
//                await supervisor.CheckAndApplyUpdatesAsync(CancellationToken.None);

//                // -----------------------------
//                // START PROCESSES
//                // -----------------------------
//                serverProcess = StartProcess(ServerPath);

//                await Task.Delay(3000);

//                bridgeProcess = StartProcess(BridgePath);

//                Console.WriteLine("Launcher complete.");
//            } finally {
//                Console.WriteLine("Launcher shutting down...");
//                StopAll();
//            }
//        }

//        private static async Task<string> DownloadToTemp(string url) {
//            string tmp = Path.GetTempFileName();
//            using HttpClient client = new HttpClient();
//            await File.WriteAllBytesAsync(tmp, await client.GetByteArrayAsync(url));
//            return tmp;
//        }

//        private Process StartProcess(string exe) {
//            if (!File.Exists(exe)) {
//                Console.WriteLine($"Missing executable: {exe}");
//                return null;
//            }

//            Console.WriteLine($"Starting: {Path.GetFileName(exe)}");
//            return Process.Start(new ProcessStartInfo {
//                FileName = exe,
//                UseShellExecute = false
//            });
//        }

//        private void StopAll() {
//            Kill(bridgeProcess, "WebBridge");
//            Kill(serverProcess, "SigCoreServer");
//        }

//        private void Kill(Process proc, string name) {
//            try {
//                if (proc != null && !proc.HasExited) {
//                    Console.WriteLine($"Stopping {name}...");
//                    proc.Kill();
//                    proc.WaitForExit();
//                }
//            } catch (Exception ex) {
//                Console.WriteLine($"Error stopping {name}: {ex.Message}");
//            }
//        }


//    }
//}
