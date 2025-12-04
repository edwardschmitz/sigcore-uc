using System;
using System.Device.I2c;
using System.Threading;

namespace SigCoreCommon {
    public class Pca9685Driver : II2cDeviceDriver {
        public I2cDevice Device { get; set; }
        public string DeviceName => "PCA9685";
        public int I2cAddress => Device.ConnectionSettings.DeviceAddress;

        private const byte MODE1 = 0x00;
        private const byte MODE2 = 0x01;
        private const byte PRESCALE = 0xFE;
        private const byte LED0_ON_L = 0x06;

        public Pca9685Driver(int address = 0x40) {
            I2cConnectionSettings settings = new I2cConnectionSettings(1, address);
            Device = I2cDevice.Create(settings);
        }

        public bool Probe() {
            try {
                byte mode1 = ReadRegister(MODE1);
                return true;
            } catch {
                return false;
            }
        }

        public void Initialize() {
            WriteRegister(MODE2, 0x04); // OUTDRV=1 (totem-pole)

            byte oldMode = ReadRegister(MODE1);
            byte sleepMode = (byte)((oldMode & 0x7F) | 0x10);
            WriteRegister(MODE1, sleepMode);
            Thread.Sleep(1);

            SetPwmFreq(250);

            for (uint i = 0; i < 8; i += 2)
                SetDutyCycle(i, 50);
            for (uint i = 1; i < 8; i += 2)
                SetDigitalOut(i, true);
        }

        public void SetDutyCycle(uint channel, double percent) {
            uint chan = channel * 2;
            if (channel > 15)
                throw new ArgumentOutOfRangeException(nameof(channel), "Channel must be between 0 and 15.");

            if (percent <= 0.0) {
                SetFullOff(chan);
            } else if (percent >= 100.0) {
                SetFullOn(chan);
            } else {
                int ticks = (int)(4096 * (percent / 100.0));
                if (ticks > 4095) ticks = 4095;
                SetPwm(chan, 0, ticks);
            }
        }

        public void SetFullOn(uint channel) {
            int reg = LED0_ON_L + 4 * (int)channel;
            Span<byte> buffer = stackalloc byte[5];
            buffer[0] = (byte)reg;
            buffer[1] = 0x00; // ON_L
            buffer[2] = 0x10; // ON_H full-on bit
            buffer[3] = 0x00; // OFF_L
            buffer[4] = 0x00; // OFF_H
            Device.Write(buffer);
        }

        public void SetFullOff(uint channel) {
            int reg = LED0_ON_L + 4 * (int)channel;
            Span<byte> buffer = stackalloc byte[5];
            buffer[0] = (byte)reg;
            buffer[1] = 0x00; // ON_L
            buffer[2] = 0x00; // ON_H
            buffer[3] = 0x00; // OFF_L
            buffer[4] = 0x10; // OFF_H full-off bit
            Device.Write(buffer);
        }

        public void SetPwmFreq(double freqHz) {
            if (freqHz < 40) freqHz = 40;
            if (freqHz > 1000) freqHz = 1000;

            double prescaleVal = 25000000.0 / 4096.0 / freqHz - 1.0;
            byte prescale = (byte)Math.Floor(prescaleVal + 0.5);

            byte oldMode = ReadRegister(MODE1);
            byte sleepMode = (byte)((oldMode & 0x7F) | 0x10);
            WriteRegister(MODE1, sleepMode);
            Thread.Sleep(1);

            WriteRegister(PRESCALE, prescale);

            WriteRegister(MODE1, 0xA1); // Restart + AI + ALLCALL
            Thread.Sleep(1);
        }

        public void SetPwm(uint channel, int onTick, int offTick) {
            int reg = LED0_ON_L + 4 * (int)channel;
            Span<byte> buffer = stackalloc byte[5];
            buffer[0] = (byte)reg;

            buffer[1] = (byte)(onTick & 0xFF);
            buffer[2] = (byte)((onTick >> 8) & 0x0F);
            buffer[3] = (byte)(offTick & 0xFF);
            buffer[4] = (byte)((offTick >> 8) & 0x0F);

            Device.Write(buffer);
        }

        public void SetDigitalOut(uint channel, bool state) {
            uint chan = channel * 2 + 1;
            if (state)
                SetFullOn(chan);
            else
                SetFullOff(chan);
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
