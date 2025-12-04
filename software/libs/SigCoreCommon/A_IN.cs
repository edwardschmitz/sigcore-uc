using System;
using System.Collections.Generic;
using System.Device.I2c;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SigCoreCommon {
    public class A_IN : Ads1115Driver {
        public enum CalType {
            None = 0,
            mXplusB,
            MultiPoint,
            Polynomial,
        }

        public enum Range {
            Voltage256mV,
            Voltage1024mV,
            Voltage2048mV,
            Voltage5V,
            Voltage10V,
            Current,
            GNDSense,
        }

        public enum DisplayFormat {
            FixedPoint,
            Scientific
        }

        public class AInConfig {
            public string Name { get; set; } = string.Empty;
            public string Units { get; set; } = string.Empty;
            public int AveragingSamples { get; set; } = 1;
            public CalType CalibrationType { get; set; } = CalType.mXplusB;
            public double M { get; set; } = 1.0;
            public double B { get; set; } = 0.0;
            public double[] InputPoints { get; set; } = Array.Empty<double>();
            public double[] AdjustedPoints { get; set; } = Array.Empty<double>();
            public double[] PolynomialCoefficients { get; set; } = Array.Empty<double>();
            public Range InputRange { get; set; } = Range.Voltage5V;
            public DisplayFormat Display { get; set; } = DisplayFormat.FixedPoint;
            public int Precision { get; set; } = 3;

            // --- Polynomial string interface ---
            public string PolynomialCoefficientsString {
                get => string.Join(", ", PolynomialCoefficients ?? Array.Empty<double>());
                set {
                    PolynomialCoefficients = value
                        .Split(',')
                        .Select(s => double.TryParse(s.Trim(), out double v) ? v : 0)
                        .ToArray();
                }
            }

            // --- MultiPoint string interface ---
            public string PiecewisePairsString {
                get {
                    if (InputPoints == null || AdjustedPoints == null || InputPoints.Length != AdjustedPoints.Length)
                        return string.Empty;

                    return string.Join(", ",
                        InputPoints.Zip(AdjustedPoints, (x, y) => $"{x}:{y}"));
                }
                set {
                    if (string.IsNullOrWhiteSpace(value)) {
                        InputPoints = Array.Empty<double>();
                        AdjustedPoints = Array.Empty<double>();
                        return;
                    }

                    var pairs = value.Split(',');
                    List<double> ins = new List<double>();
                    List<double> outs = new List<double>();

                    foreach (var pair in pairs) {
                        var parts = pair.Split(':');
                        if (parts.Length == 2 &&
                            double.TryParse(parts[0].Trim(), out double x) &&
                            double.TryParse(parts[1].Trim(), out double y)) {
                            ins.Add(x);
                            outs.Add(y);
                        }
                    }

                    InputPoints = ins.ToArray();
                    AdjustedPoints = outs.ToArray();
                }
            }

            // --- Serialization to FRAM / network ---
            public JObject ToPayload(uint channel) {
                var payload = new JObject {
                    ["channel"] = channel,
                    ["name"] = Name,
                    ["units"] = Units,
                    ["avgSamples"] = AveragingSamples,
                    ["calType"] = CalibrationType.ToString(),
                    ["range"] = InputRange.ToString(),
                    ["precision"] = Precision,
                    ["display"] = Display.ToString(),
                };

                switch (CalibrationType) {
                    case CalType.mXplusB:
                        payload["m"] = M;
                        payload["b"] = B;
                        break;

                    case CalType.MultiPoint:
                        var points = new JArray();
                        for (int i = 0; i < InputPoints.Length; i++) {
                            points.Add(new JObject {
                                ["input"] = InputPoints[i],
                                ["adjusted"] = AdjustedPoints[i]
                            });
                        }
                        payload["points"] = points;
                        break;

                    case CalType.Polynomial:
                        payload["coefficients"] = new JArray(PolynomialCoefficients);
                        break;
                }

                return payload;
            }

            public void FromPayload(JObject payload) {
                Name = payload.Value<string>("name") ?? string.Empty;
                Units = payload.Value<string>("units") ?? string.Empty;
                AveragingSamples = payload.Value<int?>("avgSamples") ?? 1;
                CalibrationType = Enum.TryParse(payload.Value<string>("calType"), out CalType ct) ? ct : CalType.mXplusB;
                InputRange = Enum.TryParse(payload.Value<string>("range"), out Range r) ? r : Range.Voltage5V;
                Display = Enum.TryParse(payload.Value<string>("display"), out DisplayFormat d) ? d : DisplayFormat.FixedPoint;
                Precision = payload.Value<int?>("precision") ?? 3;

                switch (CalibrationType) {
                    case CalType.mXplusB:
                        M = payload.Value<double?>("m") ?? 1.0;
                        B = payload.Value<double?>("b") ?? 0.0;
                        break;

                    case CalType.MultiPoint:
                        JArray points = payload["points"] as JArray;
                        if (points != null) {
                            List<double> inList = new List<double>();
                            List<double> adjList = new List<double>();
                            foreach (var pt in points) {
                                inList.Add(pt.Value<double>("input"));
                                adjList.Add(pt.Value<double>("adjusted"));
                            }
                            InputPoints = inList.ToArray();
                            AdjustedPoints = adjList.ToArray();
                        }
                        break;

                    case CalType.Polynomial:
                        PolynomialCoefficients = payload["coefficients"]?.ToObject<double[]>() ?? Array.Empty<double>();
                        break;
                }
            }
        }

        // --- Core A_IN driver members ---
        private readonly AInConfig[] _configs = new AInConfig[4];
        private readonly double[] _samples = new double[4];
        private int[] _sampleCount = { 1, 1, 1, 1 };
        private readonly A_DOUT _muxController = new A_DOUT();
        private const int address = 0x48;

        public double[] Samples => _samples;

        public A_IN() : base(1, address) {
            for (int i = 0; i < 4; i++) {
                _configs[i] = new AInConfig {
                    Name = $"AI{i + 1}",
                    Units = "V"
                };
            }
        }

        public override void Initialize() {
            base.Initialize();
            _muxController.Initialize();

            for (int i = 0; i < _muxController.Outputs.Length; i++) {
                _muxController.Outputs[i] = false;
            }
            _muxController.SendRegisters();
        }

        public void FromPayload(JObject payload) {
            uint channel = payload.Value<uint>("channel");
            if (channel > 3)
                throw new ArgumentOutOfRangeException(nameof(channel));

            _configs[channel].FromPayload(payload);
            _sampleCount[channel] = _configs[channel].AveragingSamples;
            ApplyMux(channel);
        }

        public JObject ToPayload(uint channel) {
            if (channel > 3)
                throw new ArgumentOutOfRangeException(nameof(channel));
            return _configs[channel].ToPayload(channel);
        }

        private double ApplyCalibration(uint channel, double raw) {
            switch (_configs[channel].CalibrationType) {
                case CalType.mXplusB:
                    return raw * _configs[channel].M + _configs[channel].B;

                case CalType.MultiPoint:
                    return Interpolate(raw, _configs[channel].InputPoints, _configs[channel].AdjustedPoints);

                case CalType.Polynomial:
                    return EvaluatePolynomial(raw, _configs[channel].PolynomialCoefficients);

                default:
                    return raw;
            }
        }

        public void ApplyInputRange(uint channel) {
            Range range = _configs[channel].InputRange;
            Ads1115Gain gain;

            switch (range) {
                case Range.Voltage256mV:
                    gain = Ads1115Gain.Gain16x; break;
                case Range.Voltage1024mV:
                    gain = Ads1115Gain.Gain4x; break;
                case Range.Voltage2048mV:
                    gain = Ads1115Gain.Gain2x; break;
                case Range.Voltage5V:
                case Range.Voltage10V:
                case Range.Current:
                default:
                    gain = Ads1115Gain.Gain2_3x; break;
            }
            measurementRange[channel] = gain;
        }

        public void ApplyMux(uint channel) {
            Range range = _configs[channel].InputRange;
            uint address0 = channel * 2;
            uint address1 = channel * 2 + 1;
            uint addressEn = channel + 12;

            _muxController.Outputs[addressEn] = false;
            _muxController.SendRegisters();

            switch (range) {
                case Range.Voltage5V:
                case Range.Voltage256mV:
                case Range.Voltage2048mV:
                case Range.Voltage1024mV:
                    _muxController.Outputs[address0] = false;
                    _muxController.Outputs[address1] = false;
                    break;
                case Range.Voltage10V:
                    _muxController.Outputs[address0] = true;
                    _muxController.Outputs[address1] = false;
                    break;
                case Range.Current:
                    _muxController.Outputs[address0] = false;
                    _muxController.Outputs[address1] = true;
                    break;
                case Range.GNDSense:
                    _muxController.Outputs[address0] = true;
                    _muxController.Outputs[address1] = true;
                    break;
            }

            _muxController.Outputs[addressEn] = true;
            _muxController.SendRegisters();
        }

        private static double EvaluatePolynomial(double x, double[] coeffs) {
            double result = 0;
            for (int i = 0; i < coeffs.Length; i++)
                result += coeffs[i] * Math.Pow(x, i);
            return result;
        }

        private static double Interpolate(double x, double[] input, double[] adjusted) {
            if (input.Length < 2 || adjusted.Length < 2)
                return x;
            for (int i = 0; i < input.Length - 1; i++) {
                if (x >= input[i] && x <= input[i + 1]) {
                    double t = (x - input[i]) / (input[i + 1] - input[i]);
                    return adjusted[i] + t * (adjusted[i + 1] - adjusted[i]);
                }
            }
            return adjusted[^1];
        }

        public bool GetValue() {
            bool rtn= false;
            bool readResult = ReadAllChannels(_samples, _sampleCount);
            if (readResult || sendAll) {
                rtn = true;
            } else {
                rtn = false;
            }
            sendAll = false;
            return rtn;
        }

        public double[] CurrentValues() {
            double[] vals = new double[_samples.Length];
            for (uint i = 0; i < _samples.Length; i++)
                vals[i] = CurrentReading(i);
            return vals;
        }

        internal double CurrentReading(uint channel) {
            return ApplyCalibration(channel, _samples[channel]);
        }

        public JArray GenerateAllPayload() {
            JArray arr = new JArray();
            for (uint i = 0; i < 4; i++)
                arr.Add(ToPayload(i));
            return arr;
        }

        private bool sendAll = false;
        internal void ClearAllLastValues() {
            sendAll = true;
        }
    }
}
