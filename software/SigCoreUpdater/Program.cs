using SigCoreCommon;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SigCoreUpdater {
    internal class Program {
        private static readonly CancellationTokenSource shutdownSource =
            new CancellationTokenSource();
        private static readonly Log log = new Log();
        static async Task<int> Main(string[] args) {
            Log.Initialize("SigCoreUpdater");

            RegisterSignalHandlers();

            try {
                Updater updater = new Updater();
                await updater.RunAsync(shutdownSource.Token);
                return 0;
            } catch (OperationCanceledException) when (shutdownSource.IsCancellationRequested) {
                return 0;
            } catch (Exception ex) {
                log.Error(
                    "Lifecycle",
                    "FatalError",
                    "Updater terminated because of an unhandled exception.",
                    ex);
                return 1;
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
