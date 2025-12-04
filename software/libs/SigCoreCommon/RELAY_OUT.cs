using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SigCoreCommon.A_IN;

namespace SigCoreCommon {
    public class RELAY_OUT : Mcp23008Driver {
        public class RelayConfig {
            public string Name { get; set; } = string.Empty;
            public bool DefaultState { get; set; } = false;
            public bool FailSafeState { get; set; } = false;

            public JObject ToPayload(uint channel) {
                return new JObject {
                    ["channel"] = channel,
                    ["name"] = Name,
                    ["defaultState"] = DefaultState,
                    ["failSafeState"] = FailSafeState
                };
            }

            public void FromPayload(JObject payload) {
                Name = payload.Value<string>("name") ?? string.Empty;
                DefaultState = payload.Value<bool?>("defaultState") ?? false;
                FailSafeState = payload.Value<bool?>("failSafeState") ?? false;
            }
        }

        private readonly RelayConfig[] _config = new RelayConfig[8];
        private const int address = 0x20;
        public RELAY_OUT() : base(address) {
            Dir = false;
            for (int i = 0; i < _config.Length; i++) {
                _config[i] = new RelayConfig {
                    Name = $"Relay{i + 1}",
                    DefaultState = false,
                    FailSafeState = false
                };
            }
        }

        internal bool ChangeState(uint relay, bool state) {
            bool result = false;
            if (state != IOStates[relay]) {
                IOStates[relay] = state;
                result = true;
                SendOutputs();
            }
            return result;
        }
        public void FromPayload(JObject payload) {
            uint channel = payload.Value<uint>("channel");
            if (channel > 7)
                throw new ArgumentOutOfRangeException(nameof(channel));

            _config[channel].FromPayload(payload);
        }

        public JObject ToPayload(uint channel) {
            if (channel > 7)
                throw new ArgumentOutOfRangeException(nameof(channel));

            return _config[channel].ToPayload(channel);
        }
        public JArray GenerateAllPayload() {
            JArray arr = new JArray();
            for (uint i = 0; i < 8; i++) {
                arr.Add(ToPayload(i));
            }
            return arr;
        }

        internal bool GetState(uint channel) {
            return IOStates[channel];
        }

        internal void ClearAllLastValues() {
            throw new NotImplementedException();
        }
    }
}
