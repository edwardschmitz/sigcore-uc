using SigCoreCommon;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SigCoreTestClient {
    public class DInGroup {
        private readonly SigCoreSystem _client;
        private readonly Window _owner;
        private readonly Button[] _buttons;
        private readonly TextBlock[] _labels;
        private readonly Button[] _configButtons;

        public DInGroup(
            SigCoreSystem client,
            Window owner,
            Button[] buttons,
            TextBlock[] labels,
            Button[] configButtons) {

            _client = client;
            _owner = owner;
            _buttons = buttons;
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

            for (uint ch = 0; ch < _buttons.Length; ch++) {
                uint localCh = ch;
                tasks.Add(Task.Run(async () => {
                    D_IN.DInConfig cfg = await _client.GetDInConfigAsync(localCh);
                    _owner.Dispatcher.Invoke(() => ConfigureDIn(localCh, cfg));
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
                _configButtons[localCh].Click += async (s, e) => await ConfigureDInAsync(localCh);
            }
        }

        // ========================================
        // Client → UI
        // ========================================

        private void HookClientEvents() {
            _client.DigitalInChanged += (bool[] states) =>
                _owner.Dispatcher.Invoke(() => UpdateInputs(states));
        }

        // ========================================
        // Input Handling
        // ========================================

        private void UpdateInputs(bool[] states) {
            for (uint i = 0; i < states.Length && i < _buttons.Length; i++) {
                bool state = states[i];
                Button btn = _buttons[i];

                btn.Content = state ? "On" : "Off";
                btn.Background = state ? Brushes.LightGreen : Brushes.LightGray;
            }
        }

        // ========================================
        // Configuration
        // ========================================

        private async Task ConfigureDInAsync(uint channel) {
            if (!_client.IsConnected)
                return;

            try {
                D_IN.DInConfig config = await _client.GetDInConfigAsync(channel);

                bool? result = null;
                _owner.Dispatcher.Invoke(() => {
                    DInConfigDlg dlg = new DInConfigDlg(config);
                    result = dlg.ShowDialog();
                });

                if (result == true) {
                    await _client.SetDInConfigAsync(channel, config);
                    _owner.Dispatcher.Invoke(() => ConfigureDIn(channel, config));
                }
            } catch (Exception ex) {
                Console.WriteLine($"DInGroup: Config ch:{channel} error - {ex.Message}");
            }
        }

        public void ConfigureDIn(uint channel, D_IN.DInConfig config) {
            if (channel < _labels.Length)
                _labels[channel].Text = config.Name;
        }
    }
}
