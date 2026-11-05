using Newtonsoft.Json.Linq;

namespace SigCoreCommon.Payloads {

    public class DInConfigPayload : JObjectBacked {

        public DInConfigPayload(JObject root) : base(root) { }
        public DInConfigPayload() : base() { }

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

        public int DebounceMs {
            get => Get<int>("debounce", 50);
            set => Set("debounce", value);
        }

        public bool Inverted {
            get => Get<bool>("inverted", false);
            set => Set("inverted", value);
        }
    }
}
