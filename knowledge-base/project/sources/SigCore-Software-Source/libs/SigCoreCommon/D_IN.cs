using Newtonsoft.Json.Linq;
using SigCoreCommon.Payloads;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SigCoreCommon.RELAY_OUT;

namespace SigCoreCommon {
    public class D_IN : Mcp23008Driver {
        public class DInConfig {

            public string Name { get; set; } = string.Empty;
            public int DebounceMs { get; set; } = 50;
            public bool Inverted { get; set; } = false;

            public DInConfigPayload ToPayload(uint channel) {
                DInConfigPayload payload = new DInConfigPayload();

                payload.Channel = channel;
                payload.Name = Name;
                payload.DebounceMs = DebounceMs;
                payload.Inverted = Inverted;

                return payload;
            }

            public void FromPayload(DInConfigPayload payload) {
                if (payload == null)
                    return;

                Name = payload.Name;
                DebounceMs = payload.DebounceMs;
                Inverted = payload.Inverted;
            }
        }

        private readonly DInConfig[] _config = new DInConfig[8];
        private const int address = 0x21;
        public D_IN() : base(address) {
            Dir = true;
            for (int i = 0; i < _config.Length; i++) {
                _config[i] = new DInConfig {
                    Name = $"DIN{i + 1}",
                };
            }
        }

        private DateTime[] _lastTransition = new DateTime[8];
        private bool[] CurrentStates = new bool[8]; // Final debounced, inverted states

        public bool[] States => CurrentStates;

        public bool SampleInputs() {
            bool result = false;
            GetRegisters();  // updates IOStates[]

            DateTime now = DateTime.UtcNow;
            for (int i = 0; i < 8; i++) {
                bool state = IOStates[i];
                if (!_config[i].Inverted)
                    state = !state;

                if (state != CurrentStates[i]) {
                    if ((now - _lastTransition[i]).TotalMilliseconds >= _config[i].DebounceMs) {
                        CurrentStates[i] = state;
                        _lastTransition[i] = now;
                        result = true;
                    }
                } else {
                    _lastTransition[i] = now;
                }
            }
            return result;
        }
        public bool GetInput(uint input) {
            return CurrentStates[input];
        }
        public void FromPayload(DInConfigPayload payload) {
            uint channel = payload.Channel;
            if (channel > 7)
                throw new ArgumentOutOfRangeException(nameof(channel));

            _config[channel].FromPayload(payload);
        }

        public DInConfigPayload ToPayload(uint channel) {
            if (channel > 7)
                throw new ArgumentOutOfRangeException(nameof(channel));

            return _config[channel].ToPayload(channel);
        }
        public JArray GenerateAllPayload() {
            JArray arr = new JArray();
            for (uint i = 0; i < 8; i++) {
                arr.Add(ToPayload(i).ToJson());
            }
            return arr;
        }

        private bool sendAll = false;
        internal void ClearAllLastValues() {
            sendAll = true;
        }
    }
}
