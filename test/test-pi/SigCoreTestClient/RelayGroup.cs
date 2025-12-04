using SigCoreCommon;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SigCoreTestClient {
    public class RelayGroup {
        private readonly SigCoreSystem _client;
        private readonly Window _owner;
        private readonly Button[] _buttons;
        private readonly TextBlock[] _labels;
        private readonly Button[] _configButtons;
        private readonly Button _allOnButton;
        private readonly Button _allOffButton;

        public RelayGroup(
            SigCoreSystem client,
            Window owner,
            Button[] relayButtons,
            TextBlock[] relayLabels,
            Button[] configButtons,
            Button allOnButton,
            Button allOffButton) {

            _client = client;
            _owner = owner;
            _buttons = relayButtons;
            _labels = relayLabels;
            _configButtons = configButtons;
            _allOnButton = allOnButton;
            _allOffButton = allOffButton;

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
                    RELAY_OUT.RelayConfig cfg = await _client.GetRelayConfigAsync(localCh);
                    bool state = await _client.GetRelayAsync(localCh);

                    _owner.Dispatcher.Invoke(() => {
                        UpdateConfig(localCh, cfg);
                        UpdateRelayState(localCh, state);
                    });
                }));
            }

            await Task.WhenAll(tasks);
        }

        public void UpdateConfig(uint ch, RELAY_OUT.RelayConfig config) {
            _labels[ch].Text = config.Name;
        }
        // ========================================
        // UI → Logic
        // ========================================

        private void HookUIEvents() {
            for (uint ch = 0; ch < _buttons.Length; ch++) {
                uint localCh = ch;
                _buttons[ch].Click += async (s, e) => await ToggleRelayAsync(localCh);
            }
            for (uint ch = 0; ch < _configButtons.Length; ch++) {
                uint localCh = ch;
                _configButtons[ch].Click += async (s, e) => await  ConfigureRelayAsync(localCh);
            }

            // Group ON/OFF buttons
            if (_allOnButton != null)
                _allOnButton.Click += async (s, e) => await AllOnAsync();

            if (_allOffButton != null)
                _allOffButton.Click += async (s, e) => await AllOffAsync();
        }

        // ========================================
        // Client → UI
        // ========================================

        private void HookClientEvents() {
            _client.RelayChanged += (uint ch, bool state) =>
                _owner.Dispatcher.Invoke(() => UpdateRelayState(ch, state));
        }

        // ========================================
        // Relay Logic
        // ========================================

        private async Task ToggleRelayAsync(uint channel) {
            if (!_client.IsConnected) {
                Console.WriteLine("RelayGroup: Client not connected.");
                return;
            }

            if (!_client.IsCommander) {
                Console.WriteLine("RelayGroup: Client is not Commander.");
                return;
            }

            if (channel >= _buttons.Length)
                return;

            Button btn = _buttons[channel];
            bool current = btn.Content?.ToString() == "On";
            bool newState = !current;

            Console.WriteLine($"RelayGroup: Toggle ch:{channel} → {(newState ? "On" : "Off")}");
            _client.SetRelay(channel, newState);
        }

        private void UpdateRelayState(uint channel, bool state) {
            if (channel >= _buttons.Length)
                return;

            Button btn = _buttons[channel];
            btn.Content = state ? "On" : "Off";
            btn.Background = state ? Brushes.LightGreen : Brushes.LightGray;
        }

        // ========================================
        // Configuration
        // ========================================

        private async Task ConfigureRelayAsync(uint channel) {
            if (!_client.IsConnected)
                return;

            try {
                // Await the async version of the config getter
                RELAY_OUT.RelayConfig config = await _client.GetRelayConfigAsync(channel);

                bool? result = null;
                _owner.Dispatcher.Invoke(() => {
                    RelayConfigDlg dlg = new RelayConfigDlg(config);
                    result = dlg.ShowDialog();
                });

                if (result == true) {
                    _ = _client.SetRelayConfigAsync(channel, config);
                    _owner.Dispatcher.Invoke(() => UpdateConfig(channel, config));
                }
            } catch (Exception ex) {
                Console.WriteLine($"RelayGroup: Config ch:{channel} error - {ex.Message}");
            }
        }

        // ========================================
        // Group Operations
        // ========================================

        private async Task AllOnAsync() {
            if (!_client.IsConnected)
                return;

            for (uint ch = 0; ch < _buttons.Length; ch++)
                _client.SetRelay(ch, true);
        }

        private async Task AllOffAsync() {
            if (!_client.IsConnected)
                return;

            for (uint ch = 0; ch < _buttons.Length; ch++)
                _client.SetRelay(ch, false);
        }
    }
}
