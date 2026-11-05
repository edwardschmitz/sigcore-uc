using Newtonsoft.Json.Linq;
using System;
using static SigCoreCommon.A_OUT;

namespace SigCoreCommon.Payloads {

    public class AOutConfigPayload : JObjectBacked {

        public AOutConfigPayload(JObject root) : base(root) { }
        public AOutConfigPayload() : base() { }

        // ─────────────────────────
        // Static enum domains (INT)
        // ─────────────────────────

        public static (string Name, int Value)[] OutputModes {
            get {
                Array values = Enum.GetValues(typeof(OutputMode));
                (string, int)[] result = new (string, int)[values.Length];

                for (int i = 0; i < values.Length; i++) {
                    OutputMode m = (OutputMode)values.GetValue(i);
                    result[i] = (m.ToString(), (int)m);
                }

                return result;
            }
        }

        public static (string Name, int Value)[] DisplayFormats {
            get {
                Array values = Enum.GetValues(typeof(DisplayFormat));
                (string, int)[] result = new (string, int)[values.Length];

                for (int i = 0; i < values.Length; i++) {
                    DisplayFormat d = (DisplayFormat)values.GetValue(i);
                    result[i] = (d.ToString(), (int)d);
                }

                return result;
            }
        }

        // ─────────────────────────
        // Core properties
        // ─────────────────────────

        public uint Channel {
            get => Get<uint>("channel", 0);
            set => Set("channel", value);
        }

        public string Name {
            get => Get<string>("name", string.Empty);
            set => Set("name", value);
        }

        public string Units {
            get => Get<string>("units", string.Empty);
            set => Set("units", value);
        }

        public bool IsAuto {
            get => Get<bool>("auto", false);
            set => Set("auto", value);
        }

        public int Precision {
            get => Get<int>("precision", 3);
            set => Set("precision", value);
        }

        // ─────────────────────────
        // Enum-backed fields (INT)
        // ─────────────────────────

        public int Mode {
            get {
                JToken token = _root["mode"];
                if (token == null || token.Type != JTokenType.Integer)
                    return (int)OutputMode.Voltage;

                return token.ToObject<int>();
            }
            set => Set("mode", value);
        }

        public int Display {
            get {
                JToken token = _root["display"];
                if (token == null || token.Type != JTokenType.Integer)
                    return (int)DisplayFormat.FixedPoint;

                return token.ToObject<int>();
            }
            set => Set("display", value);
        }

        // ─────────────────────────
        // Voltage scaling (mX + B)
        // ─────────────────────────

        public double VoltageScaleM {
            get => Get<double>("voltM", 1.0);
            set => Set("voltM", value);
        }

        public double VoltageScaleB {
            get => Get<double>("voltB", 0.0);
            set => Set("voltB", value);
        }
        public static int VoltageModeValue {
            get {
                (string Name, int Value)[] modes = OutputModes;

                for (int i = 0; i < modes.Length; i++) {
                    if (modes[i].Name == "Voltage")
                        return modes[i].Value;
                }

                return 0;
            }
        }
    }
}
