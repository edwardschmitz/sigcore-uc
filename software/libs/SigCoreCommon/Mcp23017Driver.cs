using System;
using System.Collections.Generic;
using System.Device.I2c;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnitsNet;

namespace SigCoreCommon {
    public class Mcp23017Driver : II2cDeviceDriver {
        public I2cDevice Device { get; set; }
        public bool[] Outputs { get; set; }

        public string DeviceName => "MCP23017";
        public int I2cAddress => Device.ConnectionSettings.DeviceAddress;

        // MCP23017 Register Constants
        private const byte IODIRA = 0x00;
        private const byte IODIRB = 0x01;
        private const byte IOCON = 0x0A;
        private const byte OLATA = 0x14;
        private const byte OLATB = 0x15;
        private const byte GPIOA = 0x12;
        private const byte GPIOB = 0x13;

        public Mcp23017Driver(int address) {
            Outputs = new bool[16];
            for (int i = 0; i < 16; i++) {
                Outputs[i] = false;
            }

            I2cConnectionSettings settings = new I2cConnectionSettings(1, address); 
            Device = I2cDevice.Create(settings);
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
            // Set all pins as OUTPUT (0 = output in IODIR)
            WriteRegister(IODIRA, 0x00);
            WriteRegister(IODIRB, 0x00);
            WriteRegister(0x0A, 0x20); // IOCON: BANK=0, SEQOP=1 (disable auto-increment)
            WriteRegister(0x0B, 0x20); // IOCON: BANK=0, SEQOP=1 (disable auto-increment)

            // Set initial output values from Outputs array
            SendRegisters();
        }

        public void GetRegisters() {
            byte a = ReadRegister(GPIOA);
            byte b = ReadRegister(GPIOB);

            for (int i = 0; i < 8; i++) {
                Outputs[i] = (a & (1 << i)) != 0;
            }

            for (int i = 0; i < 8; i++) {
                Outputs[i + 8] = (b & (1 << i)) != 0;
            }
        }

        public void SendRegisters() {
            byte olatA = 0;
            byte olatB = 0;

            for (int i = 0; i < 8; i++) {
                if (Outputs[i]) olatA |= (byte)(1 << i);
            }
            for (int i = 8; i < 16; i++) {
                if (Outputs[i]) olatB |= (byte)(1 << (i - 8));
            }

            string binaryA = Convert.ToString(olatA, 2).PadLeft(8, '0');
            string binaryB = Convert.ToString(olatB, 2).PadLeft(8, '0');
            WriteRegister(GPIOA, olatA);
            WriteRegister(GPIOB, olatB);
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
