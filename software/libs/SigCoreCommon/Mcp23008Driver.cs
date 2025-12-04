using System;
using System.Device.I2c;

namespace SigCoreCommon {
    public class Mcp23008Driver : II2cDeviceDriver {
        protected I2cDevice Device;
        protected bool[] IOStates { get; set; }

        public string DeviceName => "MCP23008";
        public int I2cAddress => Device.ConnectionSettings.DeviceAddress;

        // MCP23008 Registers
        private const byte IODIR = 0x00;
        private const byte IOCON = 0x05;
        private const byte GPPU = 0x06;
        private const byte GPIO = 0x09;
        private const byte OLAT = 0x0A;

        private byte _iodirShadow = 0x00;
        private byte _gppuShadow = 0x00;

        public Mcp23008Driver(int address) {
            IOStates = new bool[8];
            for (int i = 0; i < 8; i++) {
                IOStates[i] = false;
            }

            I2cConnectionSettings settings = new I2cConnectionSettings(1, address);
            Device = I2cDevice.Create(settings);
        }

        /// <summary>
        /// true = all inputs, false = all outputs
        /// </summary>
        public bool Dir {
            set {
                _iodirShadow = value ? (byte)0xFF : (byte)0x00;
                WriteRegister(IODIR, _iodirShadow);
                if (value) {
                    EnablePullUps();
                }
            }
        }
        /// <summary>
        /// Enable internal pull-ups on all pins.
        /// </summary>
        public void EnablePullUps() {
            _gppuShadow = 0xFF; // all 8 bits set
            WriteRegister(GPPU, _gppuShadow);
        }
        public bool Probe() {
            try {
                byte iocon = ReadRegister(IOCON); // Read known register
                return true;
            } catch {
                return false;
            }
        }

        public virtual void Initialize() {
            // Apply current IODIR and pull-ups
            WriteRegister(IODIR, _iodirShadow);
            WriteRegister(GPPU, _gppuShadow);
            WriteRegister(IOCON, 0x20); // IOCON: BANK=0, SEQOP=1

            // Apply initial outputs
            SendOutputs();
        }

        public void GetRegisters() {
            byte val = ReadRegister(GPIO);
            for (int i = 0; i < 8; i++) {
                IOStates[i] = (val & (1 << i)) != 0;
            }
        }

        public void SendOutputs() {
            byte value = 0;
            for (int i = 0; i < 8; i++) {
                if (IOStates[i]) {
                    value |= (byte)(1 << i);
                }
            }
            WriteRegister(GPIO, value); // update outputs
        }

        private void WriteRegister(byte register, byte value) {
            Span<byte> buffer = stackalloc byte[] { register, value };
            Device.Write(buffer);
        }

        private byte ReadRegister(byte register) {
            Device.WriteByte(register);
            return Device.ReadByte();
        }
    }
}
