using SigCoreCommon;
using SigCoreCommon.Payloads;
using System;

namespace ControlPanel.Dialogs {

    public class AInConfigDlgVM : ViewModelBase {
        private readonly AInConfigPayload _config;

        public AInConfigDlgVM(AInConfigPayload config) {
            _config = config;
        }

        // ─────────────────────────
        // UI domain lists (built from payload)
        // ─────────────────────────

        public static NameValueItem[] CalTypes {
            get {
                (string Name, int Value)[] src = AInConfigPayload.CalTypes;
                NameValueItem[] result = new NameValueItem[src.Length];

                for (int i = 0; i < src.Length; i++) {
                    result[i] = new NameValueItem(src[i].Name, src[i].Value);
                }

                return result;
            }
        }

        public static NameValueItem[] Ranges {
            get {
                (string Name, int Value)[] src = AInConfigPayload.Ranges;
                var result = new System.Collections.Generic.List<NameValueItem>(src.Length);
                NameValueItem reference = null;

                foreach (var range in src) {
                    if (range.Name == "Voltage10V") {
                        // Rev B uses the legacy 10 V protocol value for the 2.500 V reference.
                        reference = new NameValueItem("2.500V Reference", range.Value);
                    } else {
                        result.Add(new NameValueItem(range.Name, range.Value));
                    }
                }

                if (reference != null) {
                    int groundSenseIndex = result.FindIndex(item => item.Name == "GNDSense");
                    if (groundSenseIndex >= 0) {
                        result.Insert(groundSenseIndex, reference);
                    } else {
                        result.Add(reference);
                    }
                }

                return result.ToArray();
            }
        }

        public static NameValueItem[] DisplayFormats {
            get {
                (string Name, int Value)[] src = AInConfigPayload.DisplayFormats;
                NameValueItem[] result = new NameValueItem[src.Length];

                for (int i = 0; i < src.Length; i++) {
                    result[i] = new NameValueItem(src[i].Name, src[i].Value);
                }

                return result;
            }
        }

        // ─────────────────────────
        // Bound properties
        // ─────────────────────────

        public string Name {
            get => _config.Name;
            set { if (_config.Name != value) { _config.Name = value; OnPropertyChanged(); } }
        }

        public string Units {
            get => _config.Units;
            set { if (_config.Units != value) { _config.Units = value; OnPropertyChanged(); } }
        }

        public int AveragingSamples {
            get => _config.AveragingSamples;
            set { if (_config.AveragingSamples != value) { _config.AveragingSamples = value; OnPropertyChanged(); } }
        }

        public int InputRange {
            get => _config.InputRange;
            set { if (_config.InputRange != value) { _config.InputRange = value; OnPropertyChanged(); } }
        }

        public int CalibrationType {
            get => _config.CalibrationType;
            set { if (_config.CalibrationType != value) { _config.CalibrationType = value; OnPropertyChanged(); } }
        }

        public double M {
            get => _config.M;
            set { if (_config.M != value) { _config.M = value; OnPropertyChanged(); } }
        }

        public double B {
            get => _config.B;
            set { if (_config.B != value) { _config.B = value; OnPropertyChanged(); } }
        }

        public string PolynomialCoefficientsString {
            get => _config.PolynomialCoefficientsString;
            set {
                if (_config.PolynomialCoefficientsString != value) {
                    _config.PolynomialCoefficientsString = value;
                    OnPropertyChanged();
                }
            }
        }

        public string PiecewisePairsString {
            get => _config.PiecewisePairsString;
            set {
                if (_config.PiecewisePairsString != value) {
                    _config.PiecewisePairsString = value;
                    OnPropertyChanged();
                }
            }
        }

        public int Precision {
            get => _config.Precision;
            set { if (_config.Precision != value) { _config.Precision = value; OnPropertyChanged(); } }
        }

        public int Display {
            get => _config.Display;
            set { if (_config.Display != value) { _config.Display = value; OnPropertyChanged(); } }
        }

        public AInConfigPayload Config => _config;
    }

    // ─────────────────────────
    // UI helper (binding-friendly)
    // ─────────────────────────

    public sealed class NameValueItem {
        public string Name { get; }
        public int Value { get; }

        public NameValueItem(string name, int value) {
            Name = name;
            Value = value;
        }
    }
}
