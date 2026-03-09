using Newtonsoft.Json.Linq;

namespace SigCoreCommon.Payloads {

    public class LoggerConfigPayload : JObjectBacked {

        public LoggerConfigPayload(JObject root) : base(root) { }
        public LoggerConfigPayload() : base() { }


        public bool Enabled {
            get => Get<bool>("enabled", false);
            set => Set("enabled", value);
        }

        public double IntervalSec {
            get => Get<double>("intervalSec", 1.0);
            set => Set("intervalSec", value);
        }


        public string Server {
            get => Get<string>("server", "192.168.0.125");
            set => Set("server", value);
        }

        public int Port {
            get => Get<int>("port", 3306);
            set => Set("port", value);
        }

        public string Database {
            get => Get<string>("database", "sigcore_data_logger");
            set => Set("database", value);
        }

        public string User {
            get => Get<string>("user", "edward");
            set => Set("user", value);
        }

        public string Password {
            get => Get<string>("password", string.Empty);
            set => Set("password", value);
        }


        public string TableName {
            get => Get<string>("tableName", "data_log");
            set => Set("tableName", value);
        }

        public string SchemaVersion {
            get => Get<string>("schemaVersion", "1.0");
            set => Set("schemaVersion", value);
        }

        public int ConnectionTimeoutSec {
            get => Get<int>("connectionTimeoutSec", 5);
            set => Set("connectionTimeoutSec", value);
        }

        public int CommandTimeoutSec {
            get => Get<int>("commandTimeoutSec", 10);
            set => Set("commandTimeoutSec", value);
        }

        public string Status {
            get => Get<string>("status", "Idle");
            set => Set("status", value);
        }
    }
}
