using Newtonsoft.Json.Linq;
using SigCoreCommon;
using System;
using System.Text;
using System.Threading.Tasks;

namespace Certification {
    internal class LoopBackTest : ITest {
        private readonly SigCoreCommon.SigCoreSystem _system;
        private int[] analogStatus = new int[4];

        public string Name { get { return "Loopback Test"; } }
        public string Instructions { get { return "Connect AOut1–4 to AIn1–4 using short patch cables."; } }
        public string Progress { get; private set; }

        public LoopBackTest(SigCoreCommon.SigCoreSystem system) {
            _system = system;
            analogStatus[0] = 0;
            analogStatus[1] = 0;
            analogStatus[2] = 0;
            analogStatus[3] = 0;
        }

        /// <summary>
        /// Format:
        ///     channel 0 | <progress>
        ///     channel 1 | <progress>
        ///     channel 2 | <progress>
        ///     channel 3 | <progress>
        ///     
        /// <progress> 
        ///     0 = NOT STARTED
        ///     > 0 & < 100 = <percent complete>
        ///     100 = COMPLETE
        ///     -1 = FAILED
        /// </summary>
        private void UpdateProgress(IProgress<string> progress) {
            string pStr = string.Empty;
            for (int i = 0; i < analogStatus.Length; i++) {
                pStr += $"channel {i} | {GetStatusText(analogStatus[i])}\n";
            }
            progress.Report(pStr);
        }
        private string GetStatusText(int state) {
            if (state == 0) return "NOT STARTED";
            if (state > 0 && state < 100) return state + "%";
            if (state == 100) return "COMPLETE";
            if (state == -1) return "FAILED";
            return "UNKNOWN";
        }

        public async Task<bool> RunAsync(IProgress<string> progress, CancellationToken token) {
            bool result = true;
            progress.Report("Starting Test...");
            for (uint chan = 0; chan < 4; chan++) {
                if (!await ConfigureAnalogChannels(progress, token, chan)) {
                    result = false;
                    break;
                }
            }
            for(uint chan = 0; chan < 4; chan++) {
                if (!await TestChannel(progress, token, chan)) {
                    result = false;
                }
            }
            await Task.Delay(3000, token);

            return result;
        }

        private async Task<bool> TestChannel(IProgress<string> progress, CancellationToken token, uint chan) {
            bool result = true;
            const double tolerance = 0.1;
            const int maxSteps = 6;

            analogStatus[chan] = 0; // no progress

            try {
                for (int i = 0; i < maxSteps; i++) {
                    double targetVoltage = (double)i; // 0–5 V
                    double percent = (i / (double)(maxSteps)) * 100.0;
                    analogStatus[chan] = (int)percent;

                    await _system.SetAOutValue(chan, targetVoltage);

                    // --- Wait 1000 ms (or cancel early) ---
                    await Task.Delay(1000, token);

                    // --- Read input ---
                    double measured = await _system.ReadAnalogInputAsync(chan);
                    double delta = Math.Abs(measured - targetVoltage);

                    if (delta > tolerance) {
                        analogStatus[chan] = -1;  // failure
                        result = false;
                        break;
                    }
                    UpdateProgress(progress);
                }

                if (result) {
                    analogStatus[chan] = 100; // complete
                    UpdateProgress(progress);
                }
            } catch (OperationCanceledException) {
                result = false;
            } catch (Exception) {
                analogStatus[chan] = -1; // error → failure
                result = false;
            }

            return result;
        }

        private async Task<bool> ConfigureAnalogChannels(IProgress<string> progress, CancellationToken token, uint chan) {
            bool result = false;
            try {
                A_IN.AInConfig ainConfig = await _system.GetAInConfigAsync(chan);
                A_OUT.AnalogOutChannelConfig aoutConfig =
                    await _system.GetAOutConfigAsync(chan);  

                ainConfig.InputRange = SigCoreCommon.A_IN.Range.Voltage5V;
                aoutConfig.Mode = A_OUT.OutputMode.Voltage;
                aoutConfig.VoltageScaleM = 1.0;
                aoutConfig.VoltageScaleB = 0.0;

                await _system.SetAInConfigAsync(chan, ainConfig);
                await _system.SetAOutConfigAsync(chan, aoutConfig);
                result = true;
            } catch (OperationCanceledException) {
                result = false;
            } catch (Exception) {
                result = false;
            }
            return result;
        }
    }
}
