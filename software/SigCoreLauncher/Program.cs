using SigCoreCommon;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SigCoreLauncher {
    internal class Program {
        private static readonly CancellationTokenSource shutdownSource =
            new CancellationTokenSource();

        private static readonly Log log = new Log();

        static async Task Main(string[] args) {
            Log.Initialize("SigCoreLauncher", ownsSession: true);

            RegisterSignalHandlers();

            Launcher launcher = new Launcher();

            try {
                await launcher.RunAsync(shutdownSource.Token);
            } catch (OperationCanceledException) {
            } catch (Exception ex) {
                log.Error(
                    "Lifecycle",
                    "FatalError",
                    "Launcher terminated because of an unhandled exception.",
                    ex);
            }
        }

        private static void RegisterSignalHandlers() {
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            Console.CancelKeyPress += OnCancelKeyPress;
        }

        private static void OnProcessExit(object sender, EventArgs e) {
            shutdownSource.Cancel();
        }

        private static void OnCancelKeyPress(object sender, ConsoleCancelEventArgs e) {
            e.Cancel = true;
            shutdownSource.Cancel();
        }
    }
}
