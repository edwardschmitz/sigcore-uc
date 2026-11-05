using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SigCoreCommon.Payloads {
    public abstract class JObjectBacked {

        protected readonly JObject _root;

        protected JObjectBacked(JObject root) {
            _root = root ?? new JObject();
        }

        protected JObjectBacked() {
            _root = new JObject();
        }

        protected TValue Get<TValue>(string key, TValue defaultValue = default) {
            JToken token = _root[key];
            return token == null ? defaultValue : token.ToObject<TValue>();
        }

        protected void Set<TValue>(string key, TValue value) {
            _root[key] = JToken.FromObject(value);
        }

        public JObject ToJson() => _root;
    }
}
