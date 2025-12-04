using SigCoreCommon;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SigCoreTestClient {
    public class AInGroup {
        private readonly SigCoreSystem _client;
        private readonly Window _owner;
        private readonly TextBox[] _valueBoxes;
        private readonly TextBox[] _stabBoxes;
        private readonly TextBlock[] _labels;
        private readonly Button[] _configButtons;

        // Tracking statistics per channel
        private readonly double[] _sumVals = new double[4];
        private readonly int[] _samples = new int[4];
        private readonly double[] _deviations = new double[4];

        public AInGroup(
            SigCoreSystem client,
            Window owner,
            TextBox[] valueBoxes,
            TextBox[] stabBoxes,
            TextBlock[] labels,
            Button[] configButtons) {

            _client = client;
            _owner = owner;
            _valueBoxes = valueBoxes;
            _stabBoxes = stabBoxes;
            _labels = labels;
            _configButtons = configButtons;

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
                    A_IN.AInConfig cfg = await _client.GetAInConfigAsync(localCh);
                    double val = await _client.GetAnalogInputAsync(localCh);

                    _owner.Dispatcher.Invoke(() => {
                        _labels[localCh].Text = cfg.Name;
                        _valueBoxes[localCh].Text = $"{val:0.0000}";
                    });
                }));
            }

            await Task.WhenAll(tasks);
        }

        // ========================================
        // UI → Logic
        // ========================================

        private void HookUIEvents() {
            for (uint ch = 0; ch < _configButtons.Length; ch++) {
                uint localCh = ch;
                _configButtons[localCh].Click += async (s, e) => await ConfigureAInAsync(localCh);
            }
        }

        // ========================================
        // Client → UI
        // ========================================

        private void HookClientEvents() {
            _client.AnalogInChanged += (double[] values) =>
                _owner.Dispatcher.Invoke(() => UpdateAnalogInputs(values));
        }

        // ========================================
        // Analog Input Updates
        // ========================================

        private void UpdateAnalogInputs(double[] values) {
            for (ushort ch = 0; ch < values.Length && ch < _valueBoxes.Length; ch++) {
                double val = values[ch];

                // Compute deviation based on rolling mean
                _samples[ch]++;
                _sumVals[ch] += val;
                double avg = _sumVals[ch] / _samples[ch];
                double curDev = Math.Abs(val - avg);
                if (curDev > _deviations[ch])
                    _deviations[ch] = curDev;

                // Update UI
                _valueBoxes[ch].Text = $"{val:0.0000}";
                _stabBoxes[ch].Text = $"{_deviations[ch]:0.0000}";
            }
        }

        // ========================================
        // Configuration
        // ========================================

        private async Task ConfigureAInAsync(uint channel) {
            if (!_client.IsConnected)
                return;

            try {
                A_IN.AInConfig config = await _client.GetAInConfigAsync(channel);

                bool? result = null;
                _owner.Dispatcher.Invoke(() => {
                    AInConfigDlg dlg = new AInConfigDlg(config);
                    result = dlg.ShowDialog();
                });

                if (result == true) {
                    await _client.SetAInConfigAsync(channel, config);
                    _owner.Dispatcher.Invoke(() => _labels[channel].Text = config.Name);
                }
            } catch (Exception ex) {
                Console.WriteLine($"AInGroup: Config ch:{channel} error - {ex.Message}");
            }
        }

        // ========================================
        // Utility
        // ========================================

        public void ClearStats() {
            for (int ch = 0; ch < 4; ch++) {
                _samples[ch] = 0;
                _sumVals[ch] = 0;
                _deviations[ch] = 0;
                _stabBoxes[ch].Text = "0.0000";
            }
        }
    }
}
