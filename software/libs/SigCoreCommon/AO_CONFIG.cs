using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SigCoreCommon {
    public class AO_CONFIG : Pca9685Driver {
        private const int address = 0x40;
        private readonly uint[] _channels = { 1, 3, 5, 7 };


        public AO_CONFIG() : base(address) { }


        public void Write(int index, bool state) {
            if (index < 0 || index >= _channels.Length) return;
            SetDigitalOut(_channels[index], state);
        }


        public int ChannelCount => _channels.Length;
        public uint GetPcaChannel(int index) => _channels[index];
    }
}
