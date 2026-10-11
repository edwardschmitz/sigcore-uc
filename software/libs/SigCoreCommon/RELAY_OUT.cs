using Newtonsoft.Json.Linq;
using SigCoreCommon.Payloads;
using System;

namespace SigCoreCommon {
    public class RELAY_OUT : Mcp23008Driver {
        public class RelayConfig {
            public string Name { get; set; } = string.Empty;
            public bool DefaultState { get; set; } = false;
            public bool FailSafeState { get; set; } = false;
            public bool InvertedLogic { get; set; } = false;

            public RelayConfigPayload ToPayload(uint channel) {
                RelayConfigPayload payload = new RelayConfigPayload();
                payload.Channel = channel;
                payload.Name = Name;
                payload.DefaultState = DefaultState;
                payload.FailSafeState = FailSafeState;
                payload.InvertedLogic = InvertedLogic;
                return payload;
            }

            public void FromPayload(RelayConfigPayload payload) {
                Name = payload.Name;
                DefaultState = payload.DefaultState;
                FailSafeState = payload.FailSafeState;
                InvertedLogic = payload.InvertedLogic;
            }
        }

        private const int address = 0x20;

        private readonly RelayConfig[] _config = new RelayConfig[8];
        private readonly bool[] _states = new bool[8]; // logical relay states

        public RELAY_OUT() : base(address) {
            Dir = false;

            for (int i = 0; i < _config.Length; i++) {
                _config[i] = new RelayConfig {
                    Name = $"Relay{i + 1}",
                    DefaultState = false,
                    FailSafeState = false,
                    InvertedLogic = false
                };

                _states[i] = false;
            }
        }

        public override void Initialize() {
            base.Initialize();
            ApplyDefaultStates();
        }

        internal void ApplyDefaultStates() {
            for (uint i = 0; i < 8; i++) {
                bool logicalState = _config[i].DefaultState;

                _states[i] = logicalState;
                IOStates[i] = ToHardwareState(i, logicalState);
            }

            SendOutputs();
        }

        private bool ToHardwareState(uint relay, bool logicalState) {
            return _config[relay].InvertedLogic ? !logicalState : logicalState;
        }

        internal bool ChangeState(uint relay, bool state) {
            if (relay > 7)
                throw new ArgumentOutOfRangeException(nameof(relay));

            if (_states[relay] == state)
                return false;

            _states[relay] = state;
            IOStates[relay] = ToHardwareState(relay, state);

            SendOutputs();
            return true;
        }

        internal bool GetState(uint channel) {
            if (channel > 7)
                throw new ArgumentOutOfRangeException(nameof(channel));

            return _states[channel];
        }

        public void FromPayload(RelayConfigPayload payload) {
            if (payload == null)
                return;

            uint channel = payload.Channel;
            if (channel > 7)
                throw new ArgumentOutOfRangeException(nameof(channel));

            _config[channel].FromPayload(payload);
        }

        public RelayConfigPayload ToPayload(uint channel) {
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

        internal void ClearAllLastValues() {
            // No-op for relays.
        }
    }
}