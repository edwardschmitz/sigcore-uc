using Newtonsoft.Json.Linq;
using SigCoreCommon;

public sealed class SystemPropertiesPayload {
    private readonly JObject _root;

    public SystemPropertiesPayload(JObject root) {
        _root = root ?? new JObject();
    }

    public SystemPropertiesPayload(HardwareManager.Config cfg) {
        _root = new JObject {
            ["systemName"] = cfg.SystemName,
            ["useLastKnownValues"] = cfg.UseLastKnownValuesAtStartup,
            ["analogSamplingRate"] = cfg.AnalogSamplingRate.ToString(),
            ["pwmFrequency"] = cfg.PWMFrequency,
            ["isPIDOpen"] = cfg.IsPIDOpen
        };
    }

    public string SystemName {
        get => (string)_root["systemName"];
        set => _root["systemName"] = value;
    }

    public bool UseLastKnownValues {
        get => (bool?)_root["useLastKnownValues"] ?? true;
        set => _root["useLastKnownValues"] = value;
    }

    public string AnalogSamplingRate {
        get => (string)_root["analogSamplingRate"];
        set => _root["analogSamplingRate"] = value;
    }

    public int PWMFrequency {
        get => (int?)_root["pwmFrequency"] ?? 0;
        set => _root["pwmFrequency"] = value;
    }

    public bool IsPIDOpen {
        get => (bool?)_root["isPIDOpen"] ?? true;
        set => _root["isPIDOpen"] = value;
    }

    public JObject ToJson() => _root;
}
