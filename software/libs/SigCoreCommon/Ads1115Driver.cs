using Newtonsoft.Json.Linq;
using System;
using System.Device.I2c;
using System.Threading;

namespace SigCoreCommon {

    public class Ads1115Driver : IDisposable, II2cDeviceDriver {

        public enum InputMultiplexer : byte {
            AIN0 = 0x04,
            AIN1 = 0x05,
            AIN2 = 0x07, // Due to reverse wiring on the board
            AIN3 = 0x06  // AIN2 is on 0x07. AIN3 is on 0x06. This will be fixed in the next version of the board
        }

        public enum Ads1115Gain : byte {
            Gain2_3x = 0,  // ±6.144 V
            Gain1x = 1,    // ±4.096 V
            Gain2x = 2,    // ±2.048 V
            Gain4x = 3,    // ±1.024 V
            Gain8x = 4,    // ±0.512 V
            Gain16x = 5    // ±0.256 V
        }

        public enum Ads1115DataRate : byte {
            SPS8 = 0,
            SPS16 = 1,
            SPS32 = 2,
            SPS64 = 3,
            SPS128 = 4,
            SPS250 = 5,
            SPS475 = 6,
            SPS860 = 7
        }

        private const byte CONFIG = 0x01;
        private readonly I2cDevice _device;

        private Ads1115DataRate _dataRate = Ads1115DataRate.SPS128; // default
        public Ads1115Gain[] measurementRange = new Ads1115Gain[4];

        public virtual string DeviceName => "ADS1115";
        public virtual int I2cAddress => _device.ConnectionSettings.DeviceAddress;

        InputMultiplexer[] channelMap = new[] {
            InputMultiplexer.AIN0,
            InputMultiplexer.AIN1,
            InputMultiplexer.AIN2,
            InputMultiplexer.AIN3
        };

        public Ads1115Driver(int busId, int address) {
            var settings = new I2cConnectionSettings(busId, address);
            _device = I2cDevice.Create(settings);
        }

        public void Dispose() => _device?.Dispose();

        /// <summary>
        /// Configure the ADC data rate (SPS).
        /// </summary>
        public void SetDataRate(Ads1115DataRate rate) {
            _dataRate = rate;
        }

        /// <summary>
        /// Reads all four ADS1115 channels (AIN0–AIN3) in order, averaging N samples per channel.
        /// Discards first sample after each channel switch.
        /// </summary>
        public bool ReadAllChannels(double[] samples, int[] samplesPerChannel) {
            bool result = false;
            int delayMs = GetConversionDelayMs();

            for (int ch = 0; ch < 4; ch++) {
                InputMultiplexer mux = channelMap[ch];
                SetMultiplexer(mux, measurementRange[ch]);
                Thread.Sleep(delayMs);

                ReadContinuousVoltage(); // Discard
                Thread.Sleep(delayMs);

                double sum = 0;
                for (int i = 0; i < samplesPerChannel[ch]; i++) {
                    short raw = ReadContinuousVoltage();
                    double scale = GetFullScaleVoltage(measurementRange[ch]);
                    double volts = raw * scale / 32768.0;
                    sum += volts;
                    Thread.Sleep(delayMs);
                }

                double sample = sum / samplesPerChannel[ch];
                if (sample != samples[ch]) {
                    samples[ch] = sample;
                    result = true;
                }
            }
            return result;
        }

        public void SetMultiplexer(InputMultiplexer channel, Ads1115Gain range) {
            ushort config = 0x0000; // continuous conversion mode
            config |= (ushort)((byte)channel << 12); // MUX bits
            config |= (ushort)((byte)range << 9);    // PGA bits
            config |= (ushort)((byte)_dataRate << 5); // DR bits
            config |= 0x0003; // disable comparator

            Span<byte> configBytes = stackalloc byte[3];
            configBytes[0] = CONFIG;
            configBytes[1] = (byte)(config >> 8);
            configBytes[2] = (byte)(config & 0xFF);
            _device.Write(configBytes);
        }

        private short ReadContinuousVoltage() {
            Span<byte> convAddr = stackalloc byte[] { 0x00 }; // CONVERSION register
            Span<byte> convData = stackalloc byte[2];
            _device.WriteRead(convAddr, convData);

            short raw = (short)((convData[0] << 8) | convData[1]);
            return raw;
        }

        private static double GetFullScaleVoltage(Ads1115Gain range) {
            switch (range) {
                case Ads1115Gain.Gain2_3x: return 6.144;
                case Ads1115Gain.Gain1x: return 4.096;
                case Ads1115Gain.Gain2x: return 2.048;
                case Ads1115Gain.Gain4x: return 1.024;
                case Ads1115Gain.Gain8x: return 0.512;
                case Ads1115Gain.Gain16x: return 0.256;
                default: return 6.144;
            }
        }

        private int GetConversionDelayMs() {
            switch (_dataRate) {
                case Ads1115DataRate.SPS8: return 130;
                case Ads1115DataRate.SPS16: return 65;
                case Ads1115DataRate.SPS32: return 34;
                case Ads1115DataRate.SPS64: return 17;
                case Ads1115DataRate.SPS128: return 8;
                case Ads1115DataRate.SPS250: return 4;
                case Ads1115DataRate.SPS475: return 3;
                case Ads1115DataRate.SPS860: return 2;
                default: return 10;
            }
        }

        public bool Probe() {
            try {
                ReadRegister16(CONFIG);
                return true;
            } catch {
                return false;
            }
        }

        public virtual void Initialize() {
            // nothing yet
        }

        private ushort ReadRegister16(byte register) {
            _device.WriteByte(register);
            Span<byte> readBuffer = stackalloc byte[2];
            _device.Read(readBuffer);
            return (ushort)((readBuffer[0] << 8) | readBuffer[1]);
        }
    }
}
