using System;
using System.Device.I2c;
using System.Threading;

namespace test_analog {
    //public enum MeasuringRange : byte {
    //    FS6144 = 0x00,  // ±6.144V
    //    FS4096 = 0x01,  // ±4.096V
    //    FS2048 = 0x02,  // ±2.048V (default)
    //    FS1024 = 0x03,  // ±1.024V
    //    FS0512 = 0x04,  // ±0.512V
    //    FS0256 = 0x05   // ±0.256V
    //}

    //public enum InputMultiplexer : byte {
    //    AIN0 = 0x04,
    //    AIN1 = 0x05,
    //    AIN2 = 0x06,
    //    AIN3 = 0x07
    //}

    public class Ads1115 : IDisposable {
        private readonly I2cDevice _device;
        public MeasuringRange MeasuringRange { get; set; } = MeasuringRange.FS6144;
        public InputMultiplexer InputMultiplexer { get; set; } = InputMultiplexer.AIN0;

        public Ads1115(int busId = 1, int address = 0x48) {
            I2cConnectionSettings settings = new I2cConnectionSettings(busId, address);
            _device = I2cDevice.Create(settings);
        }

        public void Dispose() => _device?.Dispose();

        public (short Raw, double Volts) ReadVoltage() {
            ushort config = 0x8000; // OS = 1 (start single conversion)
            config |= (ushort)((byte)InputMultiplexer << 12);
            config |= (ushort)((byte)MeasuringRange << 9);
            config |= 0x0100; // MODE = 1 (single-shot)
            config |= 0x0003; // DR = 128 SPS

            Span<byte> configBytes = stackalloc byte[3];
            configBytes[0] = 0x01;
            configBytes[1] = (byte)(config >> 8);
            configBytes[2] = (byte)(config & 0xFF);
            _device.Write(configBytes);

            Span<byte> writeBuf = stackalloc byte[] { 0x01 };
            Span<byte> readBuf = stackalloc byte[2];
            while (true) {
                _device.WriteRead(writeBuf, readBuf);
                ushort configRead = (ushort)((readBuf[0] << 8) | readBuf[1]);
                if ((configRead & 0x8000) != 0)
                    break;
                Thread.Sleep(1);
            }

            return ReadConversionInternal();
        }

        public (short Raw, double Volts) ReadVoltageResult() {
            return ReadConversionInternal();
        }

        private (short Raw, double Volts) ReadConversionInternal() {
            Span<byte> convAddr = stackalloc byte[] { 0x00 };
            Span<byte> convData = stackalloc byte[2];
            _device.WriteRead(convAddr, convData);

            short raw = (short)((convData[0] << 8) | convData[1]);

            double scale = MeasuringRange switch {
                MeasuringRange.FS6144 => 6.144,
                MeasuringRange.FS4096 => 4.096,
                MeasuringRange.FS2048 => 2.048,
                MeasuringRange.FS1024 => 1.024,
                MeasuringRange.FS0512 => 0.512,
                MeasuringRange.FS0256 => 0.256,
                _ => 2.048
            };

            double volts = raw * scale / 32768.0;
            return (raw, volts);
        }

        // 🆕 Start continuous conversion on one channel
        public void StartContinuousConversion(InputMultiplexer channel) {
            ushort config = 0x0000; // OS = 0 (continuous conversion)
            config |= (ushort)((byte)channel << 12);
            config |= (ushort)((byte)MeasuringRange << 9);
            config |= 0x0000; // MODE = 0 (continuous)
            config |= 0x00E0; // DR = 475 SPS (adjust if needed)

            Span<byte> configBytes = stackalloc byte[3];
            configBytes[0] = 0x01;
            configBytes[1] = (byte)(config >> 8);
            configBytes[2] = (byte)(config & 0xFF);
            _device.Write(configBytes);
        }

        // 🆕 Change MUX input in continuous mode
        public void SetMultiplexer(InputMultiplexer channel) {
            ushort config = 0x0000; // Continuous conversion
            config |= (ushort)((byte)channel << 12);
            config |= (ushort)((byte)MeasuringRange << 9);
            config |= 0x0000; // MODE = 0 (continuous)
            config |= 0x00E0; // DR = 475 SPS (adjust as needed)

            Span<byte> configBytes = stackalloc byte[3];
            configBytes[0] = 0x01;
            configBytes[1] = (byte)(config >> 8);
            configBytes[2] = (byte)(config & 0xFF);
            _device.Write(configBytes);
        }

        // 🆕 Read latest sample from conversion register in continuous mode
        public (short Raw, double Volts) ReadContinuousVoltage() {
            Thread.Sleep(3); // Wait for new sample (475 SPS → ~2.1ms/sample)
            return ReadConversionInternal();
        }
    }
}
