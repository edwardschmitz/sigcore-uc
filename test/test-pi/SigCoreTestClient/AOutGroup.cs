using SigCoreCommon;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SigCoreTestClient {
    public class AOutGroup {
        private readonly SigCoreSystem _client;
        private readonly Window _owner;
        private readonly TextBox[] _valueBoxes;
        private readonly TextBlock[] _labels;
        private readonly Button[] _setButtons;
        private readonly Button[] _configButtons;
        private readonly bool[] _isEditing = new bool[4];
        private readonly bool[] _autoState = new bool[4];

        public AOutGroup(
            SigCoreSystem client,
            Window owner,
            TextBox[] valueBoxes,
            TextBlock[] labels,
            Button[] setButtons,
            Button[] configButtons) {

            _client = client;
            _owner = owner;
            _valueBoxes = valueBoxes;
            _labels = labels;
            _setButtons = setButtons;
            _configButtons = configButtons;

            HookUIEvents();
            HookClientEvents();
        }

        // ========================================
        // Initialization
        // ========================================

        public async Task InitConfigAsync() {
            List<Task> tasks = new List<Task>();

            for (uint ch = 0; ch < _valueBoxes.Length; ch++) {
                uint localCh = ch;
                tasks.Add(Task.Run(async () => {
                    A_OUT.AnalogOutChannelConfig cfg = await _client.GetAOutConfigAsync(localCh);
                    _owner.Dispatcher.Invoke(() => ConfigureAOut(localCh, cfg));
                }));
            }

            await Task.WhenAll(tasks);
        }

        // ========================================
        // UI → Logic
        // ========================================

        private void HookUIEvents() {
            // "Set" buttons for manual value updates
            for (uint ch = 0; ch < _setButtons.Length; ch++) {
                uint localCh = ch;
                _setButtons[localCh].Click += async (s, e) => await SetOutputAsync(localCh);
            }
            for (uint ch = 0; ch < _valueBoxes.Length; ch++) {
                uint localCh = ch;

                _valueBoxes[localCh].GotFocus += (s, e) => _isEditing[localCh] = true;
                _valueBoxes[localCh].LostFocus += (s, e) => _isEditing[localCh] = false;
            }

            // Config buttons
            for (uint ch = 0; ch < _configButtons.Length; ch++) {
                uint localCh = ch;
                _configButtons[localCh].Click += async (s, e) => await ConfigureAOutAsync(localCh);
            }
        }

        // ========================================
        // Client → UI
        // ========================================

        private void HookClientEvents() {
            _client.AnalogOutChanged += (uint ch, double value, bool auto) =>
                _owner.Dispatcher.Invoke(() => UpdateOutput(ch, value, auto));
        }

        // ========================================
        // Output Value Updates
        // ========================================

        private void UpdateOutput(uint channel, double value, bool auto) {
            if (channel >= _valueBoxes.Length)
                return;

            TextBox box = _valueBoxes[channel];
            _autoState[channel] = auto;

            // Update text only when not being edited
            if (!_isEditing[channel]) {
                box.Text = $"{value:0.0000}";
            }

            if (auto) {
                box.IsEnabled = false;
                box.Background = Brushes.LightGray;
            } else {
                box.IsEnabled = true;
                box.Background = Brushes.White;
            }
        }

        // ========================================
        // Write to Device
        // ========================================

        private async Task SetOutputAsync(uint channel) {
            if (!_client.IsConnected)
                return;
            if (_autoState[channel])
                return;  // ignore manual writes in auto mode

            try {
                double value;
                if (double.TryParse(_valueBoxes[channel].Text, out value)) {
                    _client.SetAOutValue(channel, value);
                    Console.WriteLine($"AOutGroup: Set ch:{channel} → {value}");
                } else {
                    Console.WriteLine($"AOutGroup: Invalid numeric value in ch:{channel}");
                }
            } catch (Exception ex) {
                Console.WriteLine($"AOutGroup: Error setting ch:{channel}: {ex.Message}");
            }
        }

        // ========================================
        // Configuration
        // ========================================

        private async Task ConfigureAOutAsync(uint channel) {
            if (!_client.IsConnected)
                return;

            try {
                A_OUT.AnalogOutChannelConfig config = await _client.GetAOutConfigAsync(channel);

                bool? result = null;
                _owner.Dispatcher.Invoke(() => {
                    AOutConfigDlg dlg = new AOutConfigDlg(config);
                    result = dlg.ShowDialog();
                });

                if (result == true) {
                    await _client.SetAOutConfigAsync(channel, config);
                    _owner.Dispatcher.Invoke(() => ConfigureAOut(channel, config));
                }
            } catch (Exception ex) {
                Console.WriteLine($"AOutGroup: Config ch:{channel} error - {ex.Message}");
            }
        }

        public void ConfigureAOut(uint channel, A_OUT.AnalogOutChannelConfig config) {
            if (channel < _labels.Length)
                _labels[channel].Text = config.Name;
        }
    }
}
