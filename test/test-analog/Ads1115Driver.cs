using System;
using System.Device.I2c;
using System.Threading;

namespace test_analog {
    public enum MeasuringRange : byte {
        FS6144 = 0x00,  // ±6.144V
        FS4096 = 0x01,  // ±4.096V
        FS2048 = 0x02,  // ±2.048V
        FS1024 = 0x03,  // ±1.024V
        FS0512 = 0x04,  // ±0.512V
        FS0256 = 0x05   // ±0.256V
    }

    public enum InputMultiplexer : byte {
        AIN0 = 0x04,
        AIN1 = 0x05,
        AIN2 = 0x06,
        AIN3 = 0x07
    }

    public class Ads1115Driver : IDisposable {
        private readonly I2cDevice _device;
        public MeasuringRange MeasuringRange { get; set; } = MeasuringRange.FS6144;

        public Ads1115Driver(int busId = 1, int address = 0x48) {
            var settings = new I2cConnectionSettings(busId, address);
            _device = I2cDevice.Create(settings);
        }

        public void Dispose() => _device?.Dispose();

        /// <summary>
        /// Reads all four ADS1115 channels (AIN0–AIN3) in order, averaging N samples per channel.
        /// </summary>
        public double[] ReadAllChannels(int samplesPerChannel = 10) {
            double[] result = new double[4];

            for (int ch = 0; ch < 4; ch++) {
                InputMultiplexer mux = (InputMultiplexer)(0x04 + ch);
                SetMultiplexer(mux);
                Thread.Sleep(3); // Allow MUX switch to settle

                double sum = 0;
                for (int i = 0; i < samplesPerChannel; i++) {
                    sum += ReadContinuousVoltage().Volts;
                    Thread.Sleep(2); // Wait for fresh sample @ 475 SPS
                }
                result[ch] = sum / samplesPerChannel;
            }

            return result;
        }

        public void SetMultiplexer(InputMultiplexer channel) {
            ushort config = 0x0000; // Continuous conversion mode
            config |= (ushort)((byte)channel << 12); // MUX bits
            config |= (ushort)((byte)MeasuringRange << 9); // PGA bits
            config |= 0x00E0; // DR = 475 SPS
            config |= 0x0003; // Disable comparator

            Span<byte> configBytes = stackalloc byte[3];
            configBytes[0] = 0x01; // CONFIG register
            configBytes[1] = (byte)(config >> 8);
            configBytes[2] = (byte)(config & 0xFF);
            _device.Write(configBytes);
        }

        private (short Raw, double Volts) ReadContinuousVoltage() {
            Span<byte> convAddr = stackalloc byte[] { 0x00 }; // CONVERSION register
            Span<byte> convData = stackalloc byte[2];
            _device.WriteRead(convAddr, convData);

            short raw = (short)((convData[0] << 8) | convData[1]);
            double scale = GetFullScaleVoltage(MeasuringRange);
            double volts = raw * scale / 32768.0;
            return (raw, volts);
        }

        private static double GetFullScaleVoltage(MeasuringRange range) {
            switch (range) {
                case MeasuringRange.FS6144: return 6.144;
                case MeasuringRange.FS4096: return 4.096;
                case MeasuringRange.FS2048: return 2.048;
                case MeasuringRange.FS1024: return 1.024;
                case MeasuringRange.FS0512: return 0.512;
                case MeasuringRange.FS0256: return 0.256;
                default: return 2.048;
            }
        }
    }
}
