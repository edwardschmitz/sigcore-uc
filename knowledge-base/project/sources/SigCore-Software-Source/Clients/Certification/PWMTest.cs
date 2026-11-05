using Newtonsoft.Json.Linq;
using SigCoreCommon;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Certification {
    internal class PWMTest : ITest {
        private readonly SigCoreSystem _system;
        private int[] pwmStatus = new int[4];

        public string Name { get { return "PWM Loopback Test"; } }
        public string Instructions { get { return "Connect AO1–4 (PWM) to DI1–4 using jumper wires."; } }
        public string Progress { get; private set; }
        private IProgress<string> _progress;
        private int _onCount;
        private int _offCount;

        public PWMTest(SigCoreSystem system) {
            _system = system;
            for (int i = 0; i < 4; i++) {
                pwmStatus[i] = 0;
            }
        }

        private void UpdateProgress() {
            if (_progress == null) return;

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < pwmStatus.Length; i++) {
                sb.Append("channel ");
                sb.Append(i);
                sb.Append(" | ");
                sb.Append(GetStatusText(pwmStatus[i]));
                sb.AppendLine();
            }
            _progress.Report(sb.ToString());
        }

        private string GetStatusText(int state) {
            if (state == 0) return "NOT STARTED";
            if (state > 0 && state < 100) return $"{state} >>> On: {_onCount}, Off: {_offCount}";
            if (state == 100) return "COMPLETE";
            if (state == -1) return "FAILED";
            return "UNKNOWN";
        }

        public async Task<bool> RunAsync(IProgress<string> progress, CancellationToken token) {
            bool result = true;
            _progress = progress;

            GlobalConfig(40);
            for (uint chan = 0; chan < 4; chan++) {
                if (!await ConfigurePWMChannel(chan, token)) {
                    pwmStatus[chan] = -1;
                    result = false;
                } else {
                    pwmStatus[chan] = 0;
                }
                UpdateProgress();
            }

            for (uint chan = 0; chan < 4; chan++) {
                if (!await VerifyPWMDetection(chan, token)) {
                    pwmStatus[chan] = -1;
                    result = false;
                } else {
                    pwmStatus[chan] = 100;
                    UpdateProgress();
                }
                UpdateProgress();
            }

            return result;
        }

        private async void GlobalConfig(int Hz) {
            try {
                HardwareManager.Config config = await _system.GetGlobalConfigAsync();
                config.PWMFrequency = Hz;
                await _system.SetGlobalConfigAsync(config);
            } catch (Exception ex) {
                Console.WriteLine("GlobalConfig update failed: " + ex.Message);
            }
        }

        private async Task<bool> ConfigurePWMChannel(uint chan, CancellationToken token) {
            try {
                A_OUT.AnalogOutChannelConfig config = await _system.GetAOutConfigAsync(chan);
                config.Mode = A_OUT.OutputMode.Voltage;
                config.VoltageScaleM = 1.0;
                config.VoltageScaleB = 0.0;

                await _system.SetAOutConfigAsync(chan, config);

                await _system.SetAOutValue(chan, 0); 
                return true;
            } catch {
                return false;
            }
        }

        private async Task<bool> VerifyPWMDetection(uint chan, CancellationToken token) {
            try {
                const int expectedHz = 40;
                const int toleranceHz = 5;
                const int durationMs = 4000;
                const int expected = (durationMs * expectedHz * 2) /1000;

                int edgeCount = await CountEdgesAsync(chan, durationMs, token);

                if (edgeCount == 0) {
                    pwmStatus[chan] = -1;
                } else {
                    pwmStatus[chan] = edgeCount;
                }
                UpdateProgress();

                await Task.Delay(2000);
                if (Math.Abs(expected - edgeCount) < toleranceHz) {
                    return true;
                }
                return false;
            } catch {
                return false;
            }
        }

        private async Task<int> CountEdgesAsync(uint dinChannel, int durationMs, CancellationToken token) {
            int edgeCount = 0;
            bool lastState = await _system.GetDInAsync(dinChannel);

            int interval = 10;
            int elapsed = 0;
            int i = 0;
            double outState = 0.0;

            _onCount = 0;
            _offCount = 0;

            Stopwatch sw = Stopwatch.StartNew();
            sw.Start();
            while (elapsed < durationMs) {
                token.ThrowIfCancellationRequested();

                await Task.Delay(interval, token);

                if (outState == 0.0) {
                    outState = 5.0;
                } else {
                    outState = 0.0;
                }

                await _system.SetAOutValue(dinChannel, outState);
                bool state = await _system.GetDInAsync(dinChannel);
                _onCount += state ? 1 : 0;
                _offCount += !state ? 1 : 0;
                if (state != lastState) {
                    edgeCount++;
                    lastState = state;
                }

                elapsed = (int)sw.Elapsed.TotalMilliseconds;
                pwmStatus[dinChannel] = (int)(((double)elapsed/durationMs) * 100);
                UpdateProgress();
            }

            return edgeCount;
        }
    }
}
