using Newtonsoft.Json.Linq;
using SigCoreCommon;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Zeroconf;

namespace SigCoreTestClient {

    public partial class MainWindow : Window {
        private SigCoreSystem _client;
        private RelayGroup _relays;
        private DInGroup _dIns;
        private AInGroup _aIns;
        private AOutGroup _aOuts;
        private PIDGroup _pids;

        public MainWindow() {
            InitializeComponent();

            Console.SetOut(new ConsoleWriter(AppendLog));
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e) {
            _client = new SigCoreSystem();
            IsEnabled = false;
            Cursor = Cursors.Wait;

            try {
                await _client.ConnectAndInitializeAsync("SigCoreUC.local");
                _client.Subscribe();

                Console.WriteLine("=== Begin Subsystem Initialization ===");

                List<Task> initTasks = new List<Task>();

                // === Instantiate subsystem UI managers ===
                Console.WriteLine("Initializing RelayGroup");
                _relays = new RelayGroup(
                    _client,
                    this,
                    new[] { Relay1Btn, Relay2Btn, Relay3Btn, Relay4Btn, Relay5Btn, Relay6Btn, Relay7Btn, Relay8Btn },
                    new[] { Relay1Label, Relay2Label, Relay3Label, Relay4Label, Relay5Label, Relay6Label, Relay7Label, Relay8Label },
                    new[] { Relay1Configure, Relay2Configure, Relay3Configure, Relay4Configure, Relay5Configure, Relay6Configure, Relay7Configure, Relay8Configure },
                    AllON,
                    AllOFF
                );
                initTasks.Add(_relays.InitConfigAsync());
                Console.WriteLine("RelayGroup initialization started.");

                Console.WriteLine("Initializing DInGroup");
                _dIns = new DInGroup(
                    _client,
                    this,
                    new[] { DIn1Btn, DIn2Btn, DIn3Btn, DIn4Btn, DIn5Btn, DIn6Btn, DIn7Btn, DIn8Btn },
                    new[] { DIn1Label, DIn2Label, DIn3Label, DIn4Label, DIn5Label, DIn6Label, DIn7Label, DIn8Label },
                    new[] { DIn1Configure, DIn2Configure, DIn3Configure, DIn4Configure, DIn5Configure, DIn6Configure, DIn7Configure, DIn8Configure }
                );
                initTasks.Add(_dIns.InitConfigAsync());
                Console.WriteLine("DInGroup initialization started.");

                Console.WriteLine("Initializing AInGroup");
                _aIns = new AInGroup(
                    _client,
                    this,
                    new[] { AIn1Val, AIn2Val, AIn3Val, AIn4Val },
                    new[] { AIn1Stab, AIn2Stab, AIn3Stab, AIn4Stab },
                    new[] { AIn1Label, AIn2Label, AIn3Label, AIn4Label },
                    new[] { AIn1Configure, AIn2Configure, AIn3Configure, AIn4Configure }
                );
                initTasks.Add(_aIns.InitConfigAsync());
                Console.WriteLine("AInGroup initialization started.");

                Console.WriteLine("Initializing AOutGroup");
                _aOuts = new AOutGroup(
                    _client,
                    this,
                    new[] { AOut1Val, AOut2Val, AOut3Val, AOut4Val },
                    new[] { AOut1Label, AOut2Label, AOut3Label, AOut4Label },
                    new[] { AOut1Btn, AOut2Btn, AOut3Btn, AOut4Btn },
                    new[] { AOut1Configure, AOut2Configure, AOut3Configure, AOut4Configure }
                );
                initTasks.Add(_aOuts.InitConfigAsync());
                Console.WriteLine("AOutGroup initialization started.");

                Console.WriteLine("Initializing PIDGroup");
                _pids = new PIDGroup(
                    _client,
                    this,
                    new[] { PID1SP, PID2SP, PID3SP, PID4SP },
                    new[] { PID1PV, PID2PV, PID3PV, PID4PV },
                    new[] { PID1Output, PID2Output, PID3Output, PID4Output },
                    new[] { PID1AutoBtn, PID2AutoBtn, PID3AutoBtn, PID4AutoBtn },
                    new[] { PID1ConfigBtn, PID2ConfigBtn, PID3ConfigBtn, PID4ConfigBtn },
                    new[] { PID1Label, PID2Label, PID3Label, PID4Label },
                    new[] { PID1ResetBtn, PID2ResetBtn, PID3ResetBtn, PID4ResetBtn }
                );
                initTasks.Add(_pids.InitConfigAsync());
                Console.WriteLine("PIDGroup initialization started.");

                Console.WriteLine("Waiting for all subsystem initialization to complete...");
                try {
                    await Task.WhenAll(initTasks);
                    Console.WriteLine("=== Initialization Complete ===");
                } catch (Exception ex) {
                    Console.WriteLine($"Initialization failed: {ex.Message}");
                }

                //AnalogOutputStressTest stressTest = new AnalogOutputStressTest(_client);
                //_ = stressTest.RunAsync();
            } catch (Exception ex) {
                MessageBox.Show($"Initialization failed:\n{ex.Message}", "Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            } finally {
                Cursor = Cursors.Arrow;
                IsEnabled = true;
            }
        }

        // -----------------------------
        // Window-level actions
        // -----------------------------
        private void AppendLog(string line) {
            Dispatcher.Invoke(() => {
                Log.AppendText(line + Environment.NewLine);
                Log.ScrollToEnd();
            });
        }

        private void Close_Click(object sender, RoutedEventArgs e) {
            _client?.Close();
        }

        private void Ping_Click(object sender, RoutedEventArgs e) {
            _client.PingPong(success =>
            {
                if (success)
                    Console.WriteLine("pong");
                else
                    Console.WriteLine("ping timeout");
            });
        }

        private async void GlobalConfig_Click(object sender, RoutedEventArgs e) {
            HardwareManager.Config config = await _client.GetGlobalConfigAsync();
            bool? result = null;
            Dispatcher.Invoke(() => {
                SystemConfigDlg dlg = new SystemConfigDlg(config);
                result = dlg.ShowDialog();
            });
            if (result == true)
                await _client.SetGlobalConfigAsync(config);
        }

        private void Discovery_Click(object sender, RoutedEventArgs e) {
            StartDiscovery();
        }

        private void StartDiscovery() {
            SigCoreDiscovery discovery = new SigCoreDiscovery();
            discovery.DeviceDiscovered += (IZeroconfHost host) => {
                Console.WriteLine($"Found device: {host.DisplayName} @ {host.IPAddress}");
                // Optionally auto-connect:
                // _ = _client.ConnectAndInitializeAsync(host.DisplayName, host.IPAddress);
            };
            discovery.Start();
        }
        private async void Open_Click(object sender, RoutedEventArgs e) {
            try {
                Console.WriteLine("Open: attempting connection to SigCoreUC.local");

                await _client.ConnectAndInitializeAsync("SigCoreUC.local");
            } catch (Exception ex) {
                Console.WriteLine($"Open error: {ex.Message}");
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e) {
            Console.WriteLine("Clear Stability pressed");
            _aIns?.ClearStats();
        }

        private void GetFRAM_Click(object sender, RoutedEventArgs e) {
            _client.GetFRAM(payload =>
            {
                if (payload == null) {
                    Dispatcher.Invoke(() =>
                        MessageBox.Show("FRAM request failed or timed out.", "Error"));
                    return;
                }

                string framText = payload.ToString(Newtonsoft.Json.Formatting.Indented);

                Dispatcher.Invoke(() =>
                {
                    Clipboard.SetText(framText);
                    MessageBox.Show(framText, "FRAM Dump");
                });
            });
        }

    }
    public class ConsoleWriter : TextWriter {
        private readonly Action<string> _writeAction;

        public ConsoleWriter(Action<string> writeAction) {
            _writeAction = writeAction;
        }

        public override void WriteLine(string? value) {
            _writeAction(value ?? string.Empty);
        }

        public override Encoding Encoding => Encoding.UTF8;
    }
}
