using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace SigCoreLauncher {
    internal class Program {
        private static readonly CancellationTokenSource shutdownSource =
            new CancellationTokenSource();

        static async Task Main(string[] args) {
            Console.WriteLine(">>>=== SigCore Launcher Service (updated) ===<<<");

            RegisterSignalHandlers();

            Launcher launcher = new Launcher();

            try {
                await launcher.RunAsync(shutdownSource.Token);
            } catch (OperationCanceledException) {
            } catch (Exception ex) {
                Console.WriteLine("Launcher encountered a fatal error:");
                Console.WriteLine(ex.ToString());
            }

            Console.WriteLine("Launcher exiting.");
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
