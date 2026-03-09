using Newtonsoft.Json.Linq;

namespace SigCoreCommon.Payloads {

    public class PidConfigPayload : JObjectBacked {

        public PidConfigPayload(JObject root) : base(root) { }
        public PidConfigPayload() : base() { }

        // ─────────────────────────
        // Identity
        // ─────────────────────────

        public uint Channel {
            get => Get<uint>("channel", 0);
            set => Set("channel", value);
        }

        public string Title {
            get => Get<string>("title", string.Empty);
            set => Set("title", value);
        }

        // ─────────────────────────
        // Enable / mode
        // ─────────────────────────

        public bool IsEnabled {
            get => Get<bool>("isEnabled", false);
            set => Set("isEnabled", value);
        }

        public bool AutoMode {
            get => Get<bool>("autoMode", true);
            set => Set("autoMode", value);
        }

        // ─────────────────────────
        // PID gains
        // ─────────────────────────

        public double Kp {
            get => Get<double>("kp", 10.0);
            set => Set("kp", value);
        }

        public double Ki {
            get => Get<double>("ki", 0.01);
            set => Set("ki", value);
        }

        public double Kd {
            get => Get<double>("kd", 0.0);
            set => Set("kd", value);
        }

        // ─────────────────────────
        // Limits
        // ─────────────────────────

        public double OutputMin {
            get => Get<double>("outputMin", 0.0);
            set => Set("outputMin", value);
        }

        public double OutputMax {
            get => Get<double>("outputMax", 5.0);
            set => Set("outputMax", value);
        }

        public double PVMin {
            get => Get<double>("pvMin", 0.0);
            set => Set("pvMin", value);
        }

        public double PVMax {
            get => Get<double>("pvMax", 5.0);
            set => Set("pvMax", value);
        }

        // ─────────────────────────
        // Routing
        // ─────────────────────────

        public uint PvSource {
            get => Get<uint>("pvSource", 0);
            set => Set("pvSource", value);
        }

        public uint OutputDestination {
            get => Get<uint>("outputDestination", 0);
            set => Set("outputDestination", value);
        }

        // ─────────────────────────
        // Integral / behavior
        // ─────────────────────────

        public double IntegralLimit {
            get => Get<double>("integralLimit", 1000.0);
            set => Set("integralLimit", value);
        }

        public bool ResetOnModeChange {
            get => Get<bool>("resetOnModeChange", false);
            set => Set("resetOnModeChange", value);
        }
    }
}
