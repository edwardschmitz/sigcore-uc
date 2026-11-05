using Newtonsoft.Json.Linq;
using SigCoreCommon.Payloads;
using System;
using System.Collections.Generic;
using System.Device.I2c;

namespace SigCoreCommon {
    public class A_OUT : Mcp4728Driver {
        public enum OutputMode {
            Voltage,
            PWM
        }
        public enum DisplayFormat {
            FixedPoint = 0,
            Scientific = 1,
        }

        public class AnalogOutChannelConfig {

            public string Name { get; set; } = string.Empty;
            public string Units { get; set; } = string.Empty;
            public double VoltageScaleM { get; set; } = 1.0;
            public double VoltageScaleB { get; set; } = 0.0;
            public OutputMode Mode { get; set; } = OutputMode.Voltage;
            public bool IsAuto { get; set; }
            public int Precision { get; set; } = 3;
            public DisplayFormat Display { get; set; } = DisplayFormat.FixedPoint;

            // ─────────────────────────
            // Payload conversion
            // ─────────────────────────

            public AOutConfigPayload ToPayload(uint channel) {

                AOutConfigPayload payload = new AOutConfigPayload();

                payload.Channel = channel;
                payload.Name = Name;
                payload.Units = Units;
                payload.VoltageScaleM = VoltageScaleM;
                payload.VoltageScaleB = VoltageScaleB;
                payload.Mode = (int)Mode;
                payload.IsAuto = IsAuto;
                payload.Precision = Precision;
                payload.Display = (int)Display;

                return payload;
            }

            public void FromPayload(AOutConfigPayload payload) {

                Name = payload.Name;
                Units = payload.Units;
                VoltageScaleM = payload.VoltageScaleM;
                VoltageScaleB = payload.VoltageScaleB;
                Mode = (OutputMode)payload.Mode;
                IsAuto = payload.IsAuto;
                Precision = payload.Precision;
                Display = (DisplayFormat)payload.Display;
            }

            // ─────────────────────────
            // Enum domains for UI
            // ─────────────────────────

            public static OutputMode[] OutputModes {
                get { return (OutputMode[])Enum.GetValues(typeof(OutputMode)); }
            }
        }
        public bool GetIsAuto(uint channel) { 
            return _channelConfigs[channel].IsAuto;
        }
        public void SetIsAuto(uint channel, bool val) {
            _channelConfigs[channel].IsAuto = val;
        }
        private Pca9685Driver configurator = new Pca9685Driver(0x40);
        private AnalogOutChannelConfig[] _channelConfigs = new AnalogOutChannelConfig[4];
        private double[] currentValue = new double[4];  // always stores hardware value (voltage or duty)

        public A_OUT() : base(0x60) {
            for (int i = 0; i < 4; i++) {
                _channelConfigs[i] = new AnalogOutChannelConfig {
                    Name = $"AO{i + 1}",
                    Units = "V",
                    VoltageScaleM = 1.0,
                    VoltageScaleB = 0.0,
                    Mode = OutputMode.Voltage
                };
            }
        }

        public override void Initialize() {
            base.Initialize();
            configurator.Initialize();
            for (uint i = 0; i < 4; i++) {
                SetMode(i);
            }
        }

        public void SetFromPID(uint channel, double val) {
            if (_channelConfigs[channel].IsAuto)
                SetAnalogValue(channel, val);
        }

        /// <summary>
        /// Sets the analog output value in engineering units.
        /// Converts to hardware voltage or duty cycle and stores that hardware value.
        /// </summary>
        public bool SetAnalogValue(uint channel, double engineeringValue) {
            bool result = false;
            if (channel > 3)
                throw new ArgumentOutOfRangeException(nameof(channel));

            AnalogOutChannelConfig config = _channelConfigs[channel];

            if (config.Mode == OutputMode.Voltage) {
                // Engineering → Voltage
                double voltage = (engineeringValue * config.VoltageScaleM) + config.VoltageScaleB;
                voltage = Math.Max(0.0, Math.Min(5.0, voltage));

                int hwValue = (int)Math.Round((voltage / 5.0) * 4095);
                if (currentValue[channel] != voltage) {
                    SetChannel(channel, hwValue);
                    currentValue[channel] = voltage;
                    result = true;
                }
            } else if (config.Mode == OutputMode.PWM) {
                // Engineering → Duty cycle
                double duty = (engineeringValue * config.VoltageScaleM) + config.VoltageScaleB;
                duty = Math.Max(0.0, Math.Min(100.0, duty));
                if (currentValue[channel] != duty) {
                    configurator.SetDutyCycle(channel, duty);
                    currentValue[channel] = duty;
                    result = true;
                }
            } else {
                throw new InvalidOperationException("Unsupported output mode.");
            }
            return result;
        }

        /// <summary>
        /// Gets the analog value in engineering units.
        /// Converts the stored hardware value back using display scaling.
        /// </summary>
        public (double, bool) GetAnalogValue(uint channel) {
            if (channel > 3)
                throw new ArgumentOutOfRangeException(nameof(channel));

            AnalogOutChannelConfig config = _channelConfigs[channel];
            double hwValue = currentValue[channel];
            return ((hwValue - config.VoltageScaleB) / config.VoltageScaleM, GetIsAuto(channel));
        }
        public AOutConfigPayload GetConfig(uint channel) {
            if (channel > 3)
                throw new ArgumentOutOfRangeException(nameof(channel));
            return _channelConfigs[channel].ToPayload(channel);
        }
        public void SetConfig(AOutConfigPayload payload) {
            uint channel = payload.Channel;

            if (channel > 3) return;
            _channelConfigs[channel].FromPayload(payload);
            SetMode(channel);
        }
        public AOutConfigPayload ToPayload(uint channel) {
            if (channel > 3)
                throw new ArgumentOutOfRangeException(nameof(channel));
            return _channelConfigs[channel].ToPayload(channel);
        }

        public void FromPayload(AOutConfigPayload payload) {
            uint channel = payload.Channel;

            if (channel < 0 || channel > 3)
                throw new ArgumentOutOfRangeException(nameof(channel));

            configurator.SetDutyCycle((uint)channel, 0.0);
            SetChannel((uint)channel, 0);

            Thread.Sleep(1); // allow analog nodes to discharge

            _channelConfigs[channel].FromPayload(payload);

            SetMode(channel);
        }

        private void SetMode(uint channel) {
            bool isV = _channelConfigs[channel].Mode == OutputMode.Voltage;
            configurator.SetDigitalOut((uint)channel, !isV);

            Thread.Sleep(1); // allow new path to connect and stabilize

            if (isV) {
                // Reset DAC to 0 V (safe baseline)
                SetChannel((uint)channel, 0);
                currentValue[channel] = 0.0;
            } else {
                // Reset PWM to 0% duty (off)
                configurator.SetDutyCycle((uint)channel, 0.0);
                currentValue[channel] = 0.0;
            }
        }

        internal void SetFrequency(int pwmFrequency) {
            configurator.SetPwmFreq(pwmFrequency);
        }

        public JArray GenerateAllPayload() {
            JArray arr = new JArray();
            for (uint i = 0; i < 4; i++)
                arr.Add(ToPayload(i).ToJson());
            return arr;
        }

        internal void ClearAllLastValues() {
            throw new NotImplementedException();
        }
    }
}
