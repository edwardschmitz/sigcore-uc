using Iot.Device.Common;
using System;

namespace SigCoreCommon {
    public class PWM_OUT : Pca9685Driver {
        private const int address = 0x40;
        private readonly uint[] _channels = { 0, 2, 4, 6 };

        public PWM_OUT() : base(address) { }

        public void SetDutyCycle(int index, double percent) {

            if (index < 0 || index >= _channels.Length) {
                Console.WriteLine("Invalid index");
                return;
            }

            if (percent <= 0.0) {
                // Full OFF
                SetPwm(_channels[index], 0, 0);
            } else if (percent >= 100.0) {
                // Full ON
                SetPwm(_channels[index], -1, -1);
            } else {
                // Normal PWM
                int ticks = (int)(percent / 100.0 * 4095.0);
                if (ticks >= 4095) ticks = 4094; // prevent rollover
                SetPwm(_channels[index], 0, ticks);
            }
        }

        public void SetFrequency(double freqHz) {
            SetPwmFreq(freqHz);
        }

        public int ChannelCount => _channels.Length;

        public uint GetPcaChannel(int index) {
            return _channels[index];
        }
    }
}
