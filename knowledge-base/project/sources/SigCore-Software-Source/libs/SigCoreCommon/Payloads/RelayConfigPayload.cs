using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SigCoreCommon.Payloads {
    public class RelayConfigPayload : JObjectBacked {

        public RelayConfigPayload(JObject root) : base(root) { }

        public RelayConfigPayload() : base() { }

        public uint Channel {
            get => Get<uint>("channel", 0);
            set => Set("channel", value);
        }

        public string Name {
            get => Get<string>("name", string.Empty);
            set => Set("name", value);
        }

        public bool DefaultState {
            get => Get<bool>("defaultState", false);
            set => Set("defaultState", value);
        }

        public bool FailSafeState {
            get => Get<bool>("failSafeState", false);
            set => Set("failSafeState", value);
        }
        public bool InvertedLogic {
            get => Get<bool>("invertedLogic", false);
            set => Set("invertedLogic", value);
        }
    }
}
