using System;
using System.Device.I2c;

namespace SigCoreCommon {
    public class Fm24cl64bDriver : II2cDeviceDriver {
        public I2cDevice Device { get; set; }

        public virtual string DeviceName => "FM24CL64B";
        public virtual int I2cAddress => Device.ConnectionSettings.DeviceAddress;

        private const int MEM_SIZE = 8192; // 64Kb / 8 bits = 8KB

        public Fm24cl64bDriver(int address = 0x50) {
            I2cConnectionSettings settings = new I2cConnectionSettings(1, address);
            Device = I2cDevice.Create(settings);
        }

        public virtual void Initialize() {
            // No init needed – FRAM is ready immediately
        }

        public void WriteByte(int memAddress, byte value) {
            if (memAddress < 0 || memAddress >= MEM_SIZE)
                throw new ArgumentOutOfRangeException(nameof(memAddress));

            Span<byte> buffer = stackalloc byte[3];
            buffer[0] = (byte)((memAddress >> 8) & 0xFF);
            buffer[1] = (byte)(memAddress & 0xFF);
            buffer[2] = value;
            Device.Write(buffer);
        }

        public byte ReadByte(int memAddress) {
            if (memAddress < 0 || memAddress >= MEM_SIZE)
                throw new ArgumentOutOfRangeException(nameof(memAddress));

            Span<byte> addr = stackalloc byte[2];
            addr[0] = (byte)((memAddress >> 8) & 0xFF);
            addr[1] = (byte)(memAddress & 0xFF);
            Device.Write(addr);

            Span<byte> readBuf = stackalloc byte[1];
            Device.Read(readBuf);
            return readBuf[0];
        }

        public void WriteBytes(int memAddress, byte[] data) {
            if (memAddress < 0 || memAddress + data.Length > MEM_SIZE)
                throw new ArgumentOutOfRangeException(nameof(memAddress));

            byte[] buffer = new byte[data.Length + 2];
            buffer[0] = (byte)((memAddress >> 8) & 0xFF);
            buffer[1] = (byte)(memAddress & 0xFF);
            Array.Copy(data, 0, buffer, 2, data.Length);
            Device.Write(buffer);
        }

        public byte[] ReadBytes(int memAddress, int length) {
            if (memAddress < 0 || memAddress + length > MEM_SIZE)
                throw new ArgumentOutOfRangeException(nameof(memAddress));

            Span<byte> addr = stackalloc byte[2];
            addr[0] = (byte)((memAddress >> 8) & 0xFF);
            addr[1] = (byte)(memAddress & 0xFF);
            Device.Write(addr);

            byte[] readBuf = new byte[length];
            Device.Read(readBuf);
            return readBuf;
        }

        public virtual bool Probe() {
            try {
                ReadByte(0); // test first location
                return true;
            } catch {
                return false;
            }
        }
    }
}
