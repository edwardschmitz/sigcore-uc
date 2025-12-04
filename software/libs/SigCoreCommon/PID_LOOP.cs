using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;

namespace SigCoreCommon {
    public class PID_LOOP {
        private readonly PidLoopModel[] loops = new PidLoopModel[4];
        private readonly ConfigRuntime[] configs = new ConfigRuntime[4];
        private readonly HardwareManager hwMan;

        public Config[] Configs => configs;

        public PID_LOOP(HardwareManager hw) {
            hwMan = hw;
            for (uint i = 0; i < loops.Length; i++) {
                configs[i] = new ConfigRuntime(hwMan);
                loops[i] = new PidLoopModel(configs[i]);
                configs[i].OutputDestination = i;
                configs[i].PvSource = i;
            }
        }

        public void Initialize() {
            for (int i = 0; i < loops.Length; i++)
                loops[i].Reset();
        }

        public bool AutoMode(uint chan) => configs[chan].Auto;
        public bool IsEnabled(uint chan) => configs[chan].Enabled;

        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private double _lastTime = 0;

        public void ComputeAll() {
            double now = _stopwatch.Elapsed.TotalSeconds;
            double dt = now - _lastTime;
            _lastTime = now;
            if (dt <= 0) return;

            for (int i = 0; i < loops.Length; i++) {
                if (configs[i].Enabled)
                    loops[i].Compute(dt);
            }
            Thread.Sleep(10);
        }

        public void UnpackConfig(JObject payload) {
            uint channel = payload.Value<uint>("channel");
            configs[channel].UnpackConfig(payload);
        }

        public JObject PackConfig(uint channel) => configs[channel].PackConfig(channel);

        public JArray GenerateAllPayload() {
            JArray arr = new JArray();
            for (uint i = 0; i < loops.Length; i++)
                arr.Add(PackConfig(i));
            return arr;
        }

        public JObject PackCurVal(uint channel) {
            JObject values = configs[channel].PackCurVals();
            if (values == null || values.Count == 0)
                return null;

            JObject payload = new JObject {
                ["channel"] = channel,
                ["values"] = values
            };

            // include live monitor fields
            PidMonitor m = configs[channel].Monitor;
            JObject monitorObj = new JObject {
                ["error"] = m.Error,
                ["pTerm"] = m.P,
                ["iTerm"] = m.I,
                ["dTerm"] = m.D,
                ["integralRaw"] = m.IntegralRaw,
                ["atUpperLimit"] = m.AtUpperLimit,
                ["atLowerLimit"] = m.AtLowerLimit,
                ["saturated"] = m.Saturated
            };
            payload["monitor"] = monitorObj;
            return payload;
        }

        public void UnpackCurVal(JObject payload) {
            uint channel = payload.Value<uint>("channel");
            configs[channel].UnpackCurValsToObj(payload);
        }

        internal void Reset(uint channel) => loops[channel].Reset();

        public uint IsOutputUsed(uint channel) {
            uint result = 4;
            for (uint i = 0; i < 4; i++) {
                if (configs[i].OutputDestination == channel) {
                    result = i;
                    break;
                }
            }
            return result;
        }

        internal void ClearAllLastValues() {
            throw new NotImplementedException();
        }

        // =====================================================
        // Config class for each PID loop
        // =====================================================
        public class Config : PIDVals {
            private double _output;
            private readonly JObject _lastSentValues = new JObject();

            public string Title { get; set; } = string.Empty;

            public override double Output { 
                get => _output;
                set { 
                    _output = value;
                }
            }

            public uint PvSource { get; set; } = 0;
            public uint OutputDestination { get; set; } = 0;

            public Config() {
                Enabled = false;
                _output = 0.0;
                PV = 0.0;
                SP = 0.0;
                Tol = 0.0;
                Dev = 0.0;
                Auto = true;
            }

            // ---------- Configuration packing ----------
            public virtual JObject PackConfig(uint channel) {
                JObject payload = new JObject {
                    ["channel"] = channel,
                    ["title"] = Title,
                    ["isEnabled"] = Enabled,
                    ["kp"] = Kp,
                    ["ki"] = Ki,
                    ["kd"] = Kd,
                    ["outputMin"] = OutputMin,
                    ["outputMax"] = OutputMax,
                    ["pvMin"] = PVMin,
                    ["pvMax"] = PVMax,
                    ["autoMode"] = Auto,
                    ["pvSource"] = PvSource,
                    ["outputDestination"] = OutputDestination,
                    ["integralLimit"] = IntegralLimit,
                    ["resetOnModeChange"] = ResetOnModeChange
                };
                return payload;
            }

            public virtual void UnpackConfig(JObject obj) {
                if (obj == null)
                    throw new ArgumentNullException(nameof(obj));

                Title = (string?)obj["title"] ?? string.Empty;
                Enabled = (bool?)obj["isEnabled"] ?? false;
                Auto = (bool?)obj["autoMode"] ?? true;

                OutputMin = (double?)obj["outputMin"] ?? 0.0;
                OutputMax = (double?)obj["outputMax"] ?? 5.0;
                PVMin = (double?)obj["pvMin"] ?? 0.0;
                PVMax = (double?)obj["pvMax"] ?? 5.0;

                Kp = (double?)obj["kp"] ?? 10.0;
                Ki = (double?)obj["ki"] ?? 0.01;
                Kd = (double?)obj["kd"] ?? 0.0;

                PvSource = (uint?)obj["pvSource"] ?? 0;
                OutputDestination = (uint?)obj["outputDestination"] ?? 0;

                IntegralLimit = (double?)obj["integralLimit"] ?? 1000.0;
                ResetOnModeChange = (bool?)obj["resetOnModeChange"] ?? false;
            }

            // ---------- Current-value packing ----------
            public JObject PackCurVals() {
                JObject values = new JObject();

                if (HasChanged("sp", SP)) {
                    values["sp"] = SP;
                }

                if (HasChanged("pv", PV))
                    values["pv"] = PV;

                if (HasChanged("out", Output))
                    values["out"] = Output;

                if (HasChanged("dev", Dev))
                    values["dev"] = Dev;

                if (HasChanged("tol", Tol))
                    values["tol"] = Tol;

                if (HasChanged("auto", Auto))
                    values["auto"] = Auto;

                if (HasChanged("rampTime", RampTimeSec))
                    values["rampTime"] = RampTimeSec;

                if (HasChanged("rampTarget", RampTarget))
                    values["rampTarget"] = RampTarget;

                if (HasChanged("ramp", Ramp))
                    values["ramp"] = Ramp;

                if (values.Count == 0)
                    return null;

                return values;
            }

            private bool HasChanged(string key, JToken value) {
                if (!_lastSentValues.TryGetValue(key, out JToken existing)) {
                    _lastSentValues[key] = value.DeepClone();
                    return true;
                }

                if (!JToken.DeepEquals(existing, value)) {
                    _lastSentValues[key] = value.DeepClone();
                    return true;
                }

                return false;
            }

            public virtual void UnpackCurValsToObj(JObject obj) {
                if (obj == null)
                    throw new ArgumentNullException(nameof(obj));

                JObject values = (JObject?)obj["values"];
                if (values == null)
                    return;


                if (values.ContainsKey("sp")) {
                    SP = (double?)values["sp"] ?? SP;
                }

                if (values.ContainsKey("pv")) {
                    PV = (double?)values["pv"] ?? PV;
                }

                if (values.ContainsKey("out")) {
                    Output = (double?)values["out"] ?? Output;
                }

                if (values.ContainsKey("dev")) {
                    Dev = (double?)values["dev"] ?? Dev;
                }

                if (values.ContainsKey("tol")) {
                    Tol = (double?)values["tol"] ?? Tol;
                }

                if (values.ContainsKey("auto")) {
                    Auto = (bool?)values["auto"] ?? Auto;
                }
                if (values.ContainsKey("rampTime")) {
                    RampTimeSec = (double?)values["rampTime"] ?? RampTimeSec;
                }
                if (values.ContainsKey("rampTarget")) {
                    RampTarget = (double?)values["rampTarget"] ?? RampTarget;
                }
                if (values.ContainsKey("ramp")) {
                    Ramp = (bool?)values["ramp"] ?? Ramp;
                }
            }
        }

        // =====================================================
        // Runtime subclass — connects PID config to hardware
        // =====================================================
        private class ConfigRuntime : Config {
            private readonly HardwareManager _hwMan;

            public ConfigRuntime(HardwareManager hwMan) {
                _hwMan = hwMan;
            }

            public override double PV {
                get {
                    if (_hwMan == null) {
                        return double.NaN;
                    }

                    return _hwMan.GetAnalogIn(PvSource);
                }
                set {
                    // PV is read-only — no-op
                }
            }

            public override double Output {
                get {
                    if (_hwMan == null) {
                        return double.NaN;
                    }

                    var result = _hwMan.GetAOutValue(OutputDestination);
                    return result.Item1;
                }
                set {
                    if (_hwMan == null) {
                        return;
                    }

                    _hwMan.SetAOutValue(OutputDestination, value);
                }
            }

            public override bool Auto {
                get {
                    if (_hwMan == null)
                        return false; // or your desired default
                    return _hwMan.GetAOutAuto(OutputDestination);
                }
                set {
                    if (_hwMan == null) {
                        return;
                    }
                    _hwMan.SetAOutAuto(OutputDestination, value);
                }
            }
        }
    }
}
