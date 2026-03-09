using Newtonsoft.Json.Linq;
using System;
using static SigCoreCommon.A_IN;

namespace SigCoreCommon.Payloads {

    public class AInConfigPayload : JObjectBacked {

        public AInConfigPayload(JObject root) : base(root) { }
        public AInConfigPayload() : base() { }

        // ─────────────────────────
        // Static enum domains (INT)
        // ─────────────────────────

        public static (string Name, int Value)[] CalTypes {
            get {
                Array values = Enum.GetValues(typeof(CalType));
                (string, int)[] result = new (string, int)[values.Length];

                for (int i = 0; i < values.Length; i++) {
                    CalType ct = (CalType)values.GetValue(i);
                    result[i] = (ct.ToString(), (int)ct);
                }

                return result;
            }
        }
        public static (string Name, int Value)[] Ranges {
            get {
                Array values = Enum.GetValues(typeof(A_IN.Range));
                (string, int)[] result = new (string, int)[values.Length];

                for (int i = 0; i < values.Length; i++) {
                    A_IN.Range r = (A_IN.Range)values.GetValue(i);
                    result[i] = (r.ToString(), (int)r);
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

        public int AveragingSamples {
            get => Get<int>("avgSamples", 1);
            set => Set("avgSamples", value);
        }

        public int Precision {
            get => Get<int>("precision", 3);
            set => Set("precision", value);
        }

        // ─────────────────────────
        // Enum-backed fields (INT, legacy-safe)
        // ─────────────────────────

        public int CalibrationType {
            get {
                JToken token = _root["calType"];
                if (token == null || token.Type != JTokenType.Integer)
                    return (int)CalType.mXplusB;

                return token.ToObject<int>();
            }
            set => Set("calType", value);
        }

        public int InputRange {
            get {
                JToken token = _root["range"];
                if (token == null || token.Type != JTokenType.Integer)
                    return (int)A_IN.Range.Voltage5V;

                return token.ToObject<int>();
            }
            set => Set("range", value);
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
        // mX + B calibration
        // ─────────────────────────

        public double M {
            get => Get<double>("m", 1.0);
            set => Set("m", value);
        }

        public double B {
            get => Get<double>("b", 0.0);
            set => Set("b", value);
        }

        // ─────────────────────────
        // Polynomial calibration (string-based)
        // ─────────────────────────

        public string PolynomialCoefficientsString {
            get => Get<string>("polyString", string.Empty);
            set => Set("polyString", value);
        }

        // ─────────────────────────
        // MultiPoint calibration (string-based)
        // ─────────────────────────

        public string PiecewisePairsString {
            get => Get<string>("pieceWiseString", string.Empty);
            set => Set("pieceWiseString", value);
        }
    }
}
