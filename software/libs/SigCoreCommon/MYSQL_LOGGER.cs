using MySqlConnector;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SigCoreCommon {
    public class MYSQL_LOGGER {
        private readonly HardwareManager owner;
        public DataLoggerConfig Config { get; }

        public MYSQL_LOGGER(HardwareManager owner) {
            this.owner = owner;
            this.Config = new DataLoggerConfig();
            this.Config.Status = "Initialized (not connected)";
        }

        // =====================================================
        // Write complete system configuration to the database
        // =====================================================
        public async Task<int> WriteConfigHeaderAsync() {
            if (!Config.Enabled) {
                Console.WriteLine("MYSQL_LOGGER: Logging disabled; skipping config write.");
                return -1;
            }

            JObject configJson = new JObject {
                ["global"] = owner.GetGlobalConfig(),
                ["relays"] = owner.RelayOut.GenerateAllPayload(),
                ["analogInputs"] = owner.AnalogIn.GenerateAllPayload(),
                ["analogOutputs"] = owner.AnalogOut.GenerateAllPayload(),
                ["digitalInputs"] = owner.DigitalIn.GenerateAllPayload(),
                ["pids"] = owner.PIDs.GenerateAllPayload()
            };

            int configId = await SaveConfigToDatabaseAsync(configJson);
            Console.WriteLine($"MYSQL_LOGGER: New config_current ID = {configId}");
            return configId;
        }

        // =====================================================
        // Save Config JSON to config_current table
        // =====================================================
        public async Task<int> SaveConfigToDatabaseAsync(JObject config) {
            string connStr = "Server=192.168.0.125;Port=3306;Database=sigcore_data_logger;User=edward;Password=eddedd;";
            int newConfigId = -1;

            using (MySqlConnection conn = new MySqlConnection(connStr)) {
                await conn.OpenAsync();

                string sql =
                    "INSERT INTO config_current (config_json, last_updated, schema_version) " +
                    "VALUES (@json, NOW(6), '1.0')";
                using (MySqlCommand cmd = new MySqlCommand(sql, conn)) {
                    cmd.Parameters.AddWithValue("@ts", DateTime.UtcNow);
                    cmd.Parameters.AddWithValue("@json", config.ToString(Newtonsoft.Json.Formatting.None));
                    await cmd.ExecuteNonQueryAsync();
                }

                using (MySqlCommand idCmd = new MySqlCommand("SELECT LAST_INSERT_ID();", conn)) {
                    object idResult = await idCmd.ExecuteScalarAsync();
                    if (idResult != null)
                        newConfigId = Convert.ToInt32(idResult);
                }
            }

            return newConfigId;
        }

        // =====================================================
        // Log Snapshot
        // =====================================================
        public async Task LogSnapshotAsync(Dictionary<string, double> snapshot) {
            if (!Config.Enabled) {
                Console.WriteLine("MYSQL_LOGGER: Logging disabled by config.");
                return;
            }

            string connStr =
                "Server=192.168.0.125;Port=3306;Database=sigcore_data_logger;User=edward;Password=eddedd;ConnectionTimeout=5;DefaultCommandTimeout=10;";

            using (MySqlConnection conn = new MySqlConnection(connStr)) {
                await conn.OpenAsync();

                // --- Ensure valid config_id ---
                int configId = 1;
                using (MySqlCommand checkCmd = new MySqlCommand("SELECT id FROM config_current ORDER BY id DESC LIMIT 1;", conn)) {
                    object result = await checkCmd.ExecuteScalarAsync();
                    if (result != null) {
                        configId = Convert.ToInt32(result);
                    } else {
                        using (MySqlCommand insertCmd = new MySqlCommand(
                            "INSERT INTO config_current (config_json, last_updated, schema_version) " +
                            "VALUES (JSON_OBJECT('auto', TRUE), NOW(6), '1.0');", conn)) {
                            await insertCmd.ExecuteNonQueryAsync();
                        }

                        using (MySqlCommand idCmd = new MySqlCommand("SELECT LAST_INSERT_ID();", conn)) {
                            object idResult = await idCmd.ExecuteScalarAsync();
                            configId = Convert.ToInt32(idResult);
                        }

                    }
                }

                // --- Build batch insert for all snapshot entries ---
                DateTime timestamp = DateTime.UtcNow;
                List<string> rows = new List<string>();
                List<MySqlParameter> parameters = new List<MySqlParameter>();

                foreach (KeyValuePair<string, double> kvp in snapshot) {
                    rows.Add("(?, ?, ?, ?)");
                    parameters.Add(new MySqlParameter { Value = timestamp });
                    parameters.Add(new MySqlParameter { Value = configId });
                    parameters.Add(new MySqlParameter { Value = kvp.Key });
                    parameters.Add(new MySqlParameter { Value = double.IsNaN(kvp.Value) ? 0.0 : kvp.Value });
                }

                if (rows.Count == 0) {
                    Console.WriteLine("LogSnapshotAsync: No data to log");
                    return;
                }

                string sql =
                    "INSERT INTO data_log (`timestamp`, config_id, channel_name, value) VALUES " +
                    string.Join(", ", rows);

                try {
                    using (MySqlCommand cmd = new MySqlCommand(sql, conn)) {
                        cmd.Parameters.AddRange(parameters.ToArray());
                        int inserted = await cmd.ExecuteNonQueryAsync();
                    }
                } catch (Exception ex) {
                    Console.WriteLine($"LogSnapshotAsync: Insert failed - {ex.Message}");
                }
            }
        }

        // =====================================================
        // Support Routines
        // =====================================================
        internal JObject GetConfig() {
            return Config.ToPayload();
        }

        internal void SetConfig(JObject payload) {
            Config.FromPayload(payload);
        }

        internal string GetStatus() {
            return Config.Status;
        }
        public bool IsLogging() { return Config.Enabled; }

        public class DataLoggerConfig {
            public bool Enabled { get; set; } = false;
            public double IntervalSec { get; set; } = 1.0;
            public string Server { get; set; } = "192.168.0.125";
            public int Port { get; set; } = 3306;
            public string Database { get; set; } = "sigcore_data_logger";
            public string User { get; set; } = "edward";
            public string Password { get; set; } = "";
            public string TableName { get; set; } = "data_log";
            public string SchemaVersion { get; set; } = "1.0";
            public int ConnectionTimeoutSec { get; set; } = 5;
            public int CommandTimeoutSec { get; set; } = 10;
            public string Status { get; set; } = "Idle";

            public JObject ToPayload() {
                JObject payload = new JObject {
                    ["enabled"] = Enabled,
                    ["intervalSec"] = IntervalSec,
                    ["server"] = Server,
                    ["port"] = Port,
                    ["database"] = Database,
                    ["user"] = User,
                    ["password"] = Password,
                    ["tableName"] = TableName,
                    ["schemaVersion"] = SchemaVersion,
                    ["connectionTimeoutSec"] = ConnectionTimeoutSec,
                    ["commandTimeoutSec"] = CommandTimeoutSec,
                    ["status"] = Status
                };
                return payload;
            }

            public void FromPayload(JObject payload) {
                if (payload == null) return;

                Enabled = (bool?)payload["enabled"] ?? Enabled;
                IntervalSec = (double?)payload["intervalSec"] ?? IntervalSec;
                Server = (string?)payload["server"] ?? Server;
                Port = (int?)payload["port"] ?? Port;
                Database = (string?)payload["database"] ?? Database;
                User = (string?)payload["user"] ?? User;
                Password = (string?)payload["password"] ?? Password;
                TableName = (string?)payload["tableName"] ?? TableName;
                SchemaVersion = (string?)payload["schemaVersion"] ?? SchemaVersion;
                ConnectionTimeoutSec = (int?)payload["connectionTimeoutSec"] ?? ConnectionTimeoutSec;
                CommandTimeoutSec = (int?)payload["commandTimeoutSec"] ?? CommandTimeoutSec;
                Status = (string?)payload["status"] ?? Status;
            }
        }
    }
}
