using System;
using System.Device.I2c;

namespace SigCoreCommon {
    public class Mcp4728Driver : II2cDeviceDriver {
        public I2cDevice Device { get; set; }

        public virtual string DeviceName => "MCP4728";
        public virtual int I2cAddress => Device.ConnectionSettings.DeviceAddress;

        private const int CHANNELS = 4;
        private const int MAX_VALUE = 4095;

        public Mcp4728Driver(int address = 0x60) {
            I2cConnectionSettings settings = new I2cConnectionSettings(1, address);
            Device = I2cDevice.Create(settings);
        }

        public virtual void Initialize() {
            // No global setup needed – defaults from EEPROM are applied
        }

        /// <summary>
        /// Set one channel value (0–4095). Channels: 0=A, 1=B, 2=C, 3=D.
        /// </summary>
        public void SetChannel(uint channel, int value) {
            if (channel >= CHANNELS)
                throw new ArgumentOutOfRangeException(nameof(channel));
            if (value < 0 || value > MAX_VALUE)
                throw new ArgumentOutOfRangeException(nameof(value));

            byte command = (byte)(0x40 | ((3-channel) << 1)); // Write DAC input register
            byte high = (byte)((value >> 8) & 0x0F);
            byte low = (byte)(value & 0xFF);

            Span<byte> buffer = stackalloc byte[3];
            buffer[0] = command;
            buffer[1] = high;
            buffer[2] = low;
            Device.Write(buffer);
        }

        /// <summary>
        /// Update all 4 channels in sequence (fast mode).
        /// </summary>
        public void SetAllChannels(int[] values) {
            if (values.Length != CHANNELS)
                throw new ArgumentException("Must provide 4 values.");

            Span<byte> buffer = stackalloc byte[9];
            buffer[0] = 0x50; // Multi-write command

            for (int i = 0; i < CHANNELS; i++) {
                int v = Math.Clamp(values[i], 0, MAX_VALUE);
                buffer[1 + i * 2] = (byte)((v >> 8) & 0x0F);
                buffer[2 + i * 2] = (byte)(v & 0xFF);
            }

            Device.Write(buffer);
        }

        /// <summary>
        /// Read back raw 24-byte device status/configuration.
        /// </summary>
        public byte[] ReadStatus() {
            byte[] buf = new byte[24];
            Device.Read(buf);
            return buf;
        }

        /// <summary>
        /// Decode the 24-byte status into human-readable channel info.
        /// </summary>
        public Mcp4728ChannelStatus[] GetChannelStatus() {
            byte[] raw = ReadStatus();
            var status = new Mcp4728ChannelStatus[CHANNELS];

            for (int ch = 0; ch < CHANNELS; ch++) {
                int offset = ch * 6;

                byte b0 = raw[offset + 0];
                byte b1 = raw[offset + 1];
                byte b2 = raw[offset + 2];
                byte b3 = raw[offset + 3];
                byte b4 = raw[offset + 4];
                byte b5 = raw[offset + 5];

                // Decode reference and gain
                bool vrefVdd = ((b0 >> 5) & 0x03) == 0; // 00=VDD, 10=Internal
                bool gain2x = ((b0 >> 3) & 0x01) == 1;

                // DAC value is spread across b1+b2 (12 bits)
                int dacValue = ((b1 & 0x0F) << 8) | b2;

                status[ch] = new Mcp4728ChannelStatus {
                    Channel = ch,
                    VrefIsVdd = vrefVdd,
                    Gain2X = gain2x,
                    DacValue = dacValue,
                    EepromBusy = ((b0 >> 7) & 0x01) == 1,
                    PowerOnReset = ((b0 >> 6) & 0x01) == 1
                };
            }

            return status;
        }

        public virtual bool Probe() {
            try {
                ReadStatus(); // if we can read, it's alive
                return true;
            } catch {
                return false;
            }
        }
    }

    public class Mcp4728ChannelStatus {
        public int Channel { get; set; }
        public bool VrefIsVdd { get; set; }
        public bool Gain2X { get; set; }
        public int DacValue { get; set; }
        public bool EepromBusy { get; set; }
        public bool PowerOnReset { get; set; }

        public override string ToString() {
            string refStr = VrefIsVdd ? "VDD" : "Internal 2.048V";
            string gainStr = Gain2X ? "x2" : "x1";
            return $"Ch{Channel}: DAC={DacValue}, Ref={refStr}, Gain={gainStr}, POR={PowerOnReset}, EEPROM Busy={EepromBusy}";
        }
    }
}
