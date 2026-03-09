using Newtonsoft.Json.Linq;
using System;
using static SigCoreCommon.HardwareManager;

namespace SigCoreCommon.Payloads {

    public class SystemConfigPayload : JObjectBacked {

        public SystemConfigPayload(JObject root) : base(root) { }
        public SystemConfigPayload() : base() { }

        // ─────────────────────────
        // Static enum domains (INT)
        // ─────────────────────────

        public static (string Name, int Value)[] SamplingRates {
            get {
                Array values = Enum.GetValues(typeof(SamplesPerSecond));
                (string, int)[] result = new (string, int)[values.Length];

                for (int i = 0; i < values.Length; i++) {
                    SamplesPerSecond v = (SamplesPerSecond)values.GetValue(i);
                    result[i] = (v.ToString(), (int)v);
                }

                return result;
            }
        }

        // ─────────────────────────
        // Core system identity
        // ─────────────────────────

        public string SystemName {
            get => Get<string>("systemName", "SigCore UC");
            set => Set("systemName", value);
        }

        public string SystemSerialNumber {
            get => Get<string>("systemSerialNumber", "SCU000000");
            set => Set("systemSerialNumber", value);
        }

        public string HardwareVersion {
            get => Get<string>("hardwareVersion", "1.0");
            set => Set("hardwareVersion", value);
        }

        public string SoftwareVersion {
            get => Get<string>("softwareVersion", "0.0.0");
            set => Set("softwareVersion", value);
        }

        // ─────────────────────────
        // Startup behavior
        // ─────────────────────────

        public bool UseLastKnownValuesAtStartup {
            get => Get<bool>("useLastKnownValues", true);
            set => Set("useLastKnownValues", value);
        }

        // ─────────────────────────
        // Timing / sampling (INT)
        // ─────────────────────────

        public int AnalogSamplingRate {
            get {
                JToken token = _root["analogSamplingRate"];
                if (token == null || token.Type != JTokenType.Integer)
                    return (int)SamplesPerSecond.Medium;

                return token.ToObject<int>();
            }
            set => Set("analogSamplingRate", value);
        }

        public int PWMFrequency {
            get => Get<int>("pwm_frequency", 250);
            set => Set("pwm_frequency", value);
        }

        // ─────────────────────────
        // Control features
        // ─────────────────────────

        public bool IsPIDOpen {
            get => Get<bool>("isPIDOpen", true);
            set => Set("isPIDOpen", value);
        }

        // ─────────────────────────
        // Security
        // ─────────────────────────

        public string SystemPassword {
            get => Get<string>("systemPassword", "admin");
            set => Set("systemPassword", value);
        }

        // ─────────────────────────
        // FRAM serialization scope
        // ─────────────────────────

        public JObject ToFramPayload() {
            JObject payload = new JObject();

            payload["systemName"] = SystemName;
            payload["systemSerialNumber"] = SystemSerialNumber;
            payload["useLastKnownValues"] = UseLastKnownValuesAtStartup;
            payload["analogSamplingRate"] = AnalogSamplingRate;
            payload["pwm_frequency"] = PWMFrequency;

            return payload;
        }
    }
}
