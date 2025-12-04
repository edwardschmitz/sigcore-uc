using SigCoreCommon;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace SigCoreTestClient {
    public class PIDGroup {
        private readonly SigCoreSystem _client;
        private readonly Window _owner;

        private readonly TextBox[] _spBoxes;
        private readonly TextBox[] _pvBoxes;
        private readonly TextBox[] _outputBoxes;
        private readonly Button[] _autoButtons;
        private readonly Button[] _configButtons;
        private readonly Button[] _resetButtons;
        private readonly TextBlock[] _labels;

        private readonly bool[] _isEditingSP = new bool[4];
        private readonly bool[] _isEditingOutput = new bool[4];   // NEW
        private readonly bool[] _autoState = new bool[4];

        public PIDGroup(
            SigCoreSystem client,
            Window owner,
            TextBox[] spBoxes,
            TextBox[] pvBoxes,
            TextBox[] outputBoxes,
            Button[] autoButtons,
            Button[] configButtons,
            TextBlock[] labels,
            Button[] resetButtons
        ) {
            _client = client;
            _owner = owner;
            _spBoxes = spBoxes;
            _pvBoxes = pvBoxes;
            _outputBoxes = outputBoxes;
            _autoButtons = autoButtons;
            _configButtons = configButtons;
            _resetButtons = resetButtons;
            _labels = labels;

            HookUIEvents();
            HookClientEvents();
        }

        // ========================================
        // Initialization
        // ========================================

        public async Task InitConfigAsync() {
            List<Task> tasks = new List<Task>();
            for (uint ch = 0; ch < 4; ch++) {
                uint localCh = ch;
                tasks.Add(Task.Run(async () => {
                    PID_LOOP.Config cfg = await _client.GetPIDConfigAsync(localCh);
                    _owner.Dispatcher.Invoke(() => {
                        _labels[localCh].Text = cfg.Title;
                        _autoState[localCh] = cfg.Auto;
                    });
                }));
            }
            await Task.WhenAll(tasks);
        }

        // ========================================
        // UI → Logic
        // ========================================

        private void HookUIEvents() {
            // SP (Setpoint)
            for (uint ch = 0; ch < _spBoxes.Length; ch++) {
                uint localCh = ch;
                _spBoxes[localCh].GotFocus += (s, e) => _isEditingSP[localCh] = true;
                _spBoxes[localCh].LostFocus += (s, e) => {
                    _isEditingSP[localCh] = false;
                    Commit(localCh, true);
                };
                _spBoxes[localCh].KeyDown += (s, e) => {
                    if (e.Key == Key.Enter) {
                        _isEditingSP[localCh] = false;
                        Commit(localCh, true);
                        _spBoxes[localCh].MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                    }
                };
            }

            // Output boxes
            for (uint ch = 0; ch < _outputBoxes.Length; ch++) {
                uint localCh = ch;
                _outputBoxes[localCh].GotFocus += (s, e) => _isEditingOutput[localCh] = true;   
                _outputBoxes[localCh].LostFocus += (s, e) => {
                    _isEditingOutput[localCh] = false;                                        
                    Commit(localCh, false);
                };
                _outputBoxes[localCh].KeyDown += (s, e) => {
                    if (e.Key == Key.Enter) {
                        _isEditingOutput[localCh] = false;                                    
                        Commit(localCh, false);
                        _outputBoxes[localCh].MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                    }
                };
            }

            // Auto buttons
            for (uint ch = 0; ch < _autoButtons.Length; ch++) {
                uint localCh = ch;
                _autoButtons[localCh].Click += async (s, e) => await ToggleAutoAsync(localCh);
            }

            // Config buttons
            for (uint ch = 0; ch < _configButtons.Length; ch++) {
                uint localCh = ch;
                _configButtons[localCh].Click += async (s, e) => await ConfigurePIDAsync(localCh);
            }

            // Reset buttons
            for (uint ch = 0; ch < _resetButtons.Length; ch++) {
                uint localCh = ch;
                _resetButtons[localCh].Click += async (s, e) => await ResetPIDAsync(localCh);
            }
        }

        // ========================================
        // Client → UI
        // ========================================

        private void HookClientEvents() {
            _client.PIDSPChanged += SPChanged;
            _client.PIDOutputChanged += OutputChanged;
            _client.PIDTolChanged += TolChanged;
            _client.PIDAutoChanged += AutoChanged;
            _client.PIDPVChanged += PVChanged;
            _client.PIDDevChanged += DevChanged;
        }

        private void DevChanged(uint ch, double dev) {
        }

        private void PVChanged(uint ch, double pv) {
             _owner.Dispatcher.Invoke (() => { _pvBoxes[ch].Text = pv.ToString("F3"); });
        }

        private void AutoChanged(uint ch, bool auto) {
            _owner.Dispatcher.Invoke(() => {
                _spBoxes[ch].IsReadOnly = !auto;
                _outputBoxes[ch].IsReadOnly = auto;
                _spBoxes[ch].Background = !auto ? Brushes.LightGray : Brushes.White;
                _outputBoxes[ch].Background = auto ? Brushes.LightGray : Brushes.White;
                _autoButtons[ch].Content = auto ? "AUTO" : "MAN";
                _autoButtons[ch].Background = auto ? Brushes.LightGreen : Brushes.LightCoral;
            });
        }

        private void TolChanged(uint ch, double tol) {
        }

        private void OutputChanged(uint ch, double output) {
            if (!_isEditingOutput[ch])                                    
                _owner.Dispatcher.Invoke (() => { _outputBoxes[ch].Text = output.ToString("F3"); });
        }

        private void SPChanged(uint ch, double sp) {
            if (!_isEditingSP[ch])
                _owner.Dispatcher.Invoke(() => {
                    _spBoxes[ch].Text = sp.ToString("F3");
                });
        }

        private void Commit(uint ch, bool isSP) {
            if (ch >= 4 || !_client.IsConnected)
                return;

            bool auto = _autoState[ch];
            if ((auto && !isSP) || (!auto && isSP))
                return;

            double sp = SafeParse(_spBoxes[ch].Text);
            double output = SafeParse(_outputBoxes[ch].Text);
            double tol = 0;

            try {
                _ = _client.SetPIDAutoAsync(ch, auto);
                _ = _client.SetPIDOutputAsync(ch, output);
                _ = _client.SetPIDSPAsync(ch, sp);
                _ = _client.SetPIDTolAsync(ch, tol);

                Console.WriteLine($"PIDGroup: Commit ch:{ch} sp:{sp:F3} out:{output:F3} auto:{auto}");
            } catch (Exception ex) {
                Console.WriteLine($"PIDGroup: Commit error ch:{ch} - {ex.Message}");
            }
        }

        // ========================================
        // Auto / Manual Toggle
        // ========================================

        private async Task ToggleAutoAsync(uint ch) {
            if (!_client.IsConnected)
                return;

            try {
                PID_LOOP.Config config = await _client.GetPIDConfigAsync(ch);
                config.Auto = !config.Auto;
                _autoState[ch] = config.Auto;

                AutoChanged(ch, config.Auto);

                await _client.SetPIDConfigAsync(ch, config);
                Console.WriteLine($"PIDGroup: ToggleAuto ch:{ch} → {config.Auto}");
            } catch (Exception ex) {
                Console.WriteLine($"PIDGroup: ToggleAuto ch:{ch} error - {ex.Message}");
            }
        }

        // ========================================
        // Configuration
        // ========================================

        private async Task ConfigurePIDAsync(uint channel) {
            if (!_client.IsConnected)
                return;

            try {
                PID_LOOP.Config config = await _client.GetPIDConfigAsync(channel);
                bool? result = null;
                _owner.Dispatcher.Invoke(() => {
                    PIDConfigDlg dlg = new PIDConfigDlg(config);
                    result = dlg.ShowDialog();
                });
                if (result == true) {
                    await _client.SetPIDConfigAsync(channel, config);
                    _owner.Dispatcher.Invoke(() => _labels[channel].Text = config.Title);
                }
            } catch (Exception ex) {
                Console.WriteLine($"PIDGroup: Config ch:{channel} error - {ex.Message}");
            }
        }

        // ========================================
        // Reset PID Loop
        // ========================================

        private async Task ResetPIDAsync(uint ch) {
            if (!_client.IsConnected)
                return;

            try {
                _client.ResetPID(ch);
                _resetButtons[ch].Background = Brushes.LightGreen;
                await Task.Delay(200);
                _resetButtons[ch].Background = Brushes.LightCoral;
                Console.WriteLine($"PIDGroup: Reset ch:{ch}");
            } catch (Exception ex) {
                Console.WriteLine($"PIDGroup: Reset error ch:{ch} - {ex.Message}");
            }
        }

        // ========================================
        // Helpers
        // ========================================

        private static double SafeParse(string text) {
            double val;
            return double.TryParse(text, out val) ? val : 0;
        }
    }
}
