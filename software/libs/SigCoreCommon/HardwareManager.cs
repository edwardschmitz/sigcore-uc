using Newtonsoft.Json.Linq;
using SigCoreCommon.Payloads;
using SigCoreCommon.Update;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static SigCoreCommon.HardwareManager;

namespace SigCoreCommon {
    public class HardwareManager {
        public RELAY_OUT RelayOut { get; }
        public A_IN AnalogIn { get; }
        public A_OUT AnalogOut { get; }
        public D_IN DigitalIn { get; }
        private Fm24cl64bDriver FRAM { get; }
        public PID_LOOP PIDs { get; }
        private MYSQL_LOGGER Logger { get; }
        public NetworkManager Network { get; }


        private CancellationTokenSource _cts;
        private CancellationTokenSource _ctsDIn;
        private CancellationTokenSource _ctsPID;
        private CancellationTokenSource _ctsLogger;

        private readonly Dictionary<string, double> snapshot;


        private Config _config;
        public IDispatcher Dispatcher { get; set; }

        public HardwareManager() {
            RelayOut = new RELAY_OUT();
            AnalogIn = new A_IN();
            AnalogOut = new A_OUT();
            DigitalIn = new D_IN();
            PIDs = new PID_LOOP(this);
            FRAM = new Fm24cl64bDriver();
            Logger = new MYSQL_LOGGER(this);
            Network = new NetworkManager();


            _cts = new CancellationTokenSource();
            _ctsDIn = new CancellationTokenSource();
            _ctsPID = new CancellationTokenSource();

            _config = new Config();

            snapshot = new Dictionary<string, double>();
        }

        private void SetupSnapshot() {
            for (uint ch = 0; ch < 4; ch++) {
                snapshot[$"AI{ch}"] = 0.0;
                snapshot[$"AO{ch}"] = 0.0;
                snapshot[$"PID{ch}_SP"] = 0.0;
                snapshot[$"PID{ch}_PV"] = 0.0;
                snapshot[$"PID{ch}_OUT"] = 0.0;
                snapshot[$"PID{ch}_AUTO"] = 0.0;
                snapshot[$"PID{ch}_DEV"] = 0.0;
                snapshot[$"PID{ch}_TOL"] = 0.0;

            }
            for (uint ch = 0; ch < 8; ch++) {
                snapshot[$"RELAY{ch}"] = 0.0;
                snapshot[$"DI{ch}"] = 0.0;
            }
        }


        public Dictionary<string, double> GetSnapshot() {
            lock (snapshot) {
                // --- Analog Inputs ---
                for (uint i = 0; i < 4; i++) {
                    snapshot[$"AI{i}"] = AnalogIn.CurrentReading(i);
                }

                // --- Analog Outputs ---
                for (uint i = 0; i < 4; i++) {
                    (double value, bool isAuto) = AnalogOut.GetAnalogValue(i);
                    snapshot[$"AO{i}"] = value;
                    snapshot[$"AO{i}_AUTO"] = isAuto ? 1.0 : 0.0;
                }

                // --- Digital Inputs ---
                for (uint i = 0; i < 8; i++) {
                    snapshot[$"DI{i}"] = DigitalIn.GetInput(i) ? 1.0 : 0.0;
                }

                // --- Relay Outputs ---
                for (uint i = 0; i < 8; i++) {
                    snapshot[$"RELAY{i}"] = RelayOut.GetState(i) ? 1.0 : 0.0;
                }

                // --- PID Loops ---
                for (uint i = 0; i < 4; i++) {
                    PID_LOOP.Config cfg = PIDs.Configs[i];
                    snapshot[$"PID{i}_SP"] = cfg.SP;
                    snapshot[$"PID{i}_PV"] = cfg.PV;
                    snapshot[$"PID{i}_OUT"] = cfg.Output;
                    snapshot[$"PID{i}_AUTO"] = cfg.Auto ? 1.0 : 0.0;
                    snapshot[$"PID{i}_DEV"] = cfg.Dev;
                    snapshot[$"PID{i}_TOL"] = cfg.Tol;
                }

                return snapshot;
            }
        }
        public void Initialize() {

            // Initialize FRAM
            FRAM.Initialize();

            // Read config blob from FRAM
            JObject storedConfig = ReadConfigFromFram();
            if (storedConfig == null) {
                Console.WriteLine("Error: Failed to read config from FRAM.");
                return;
            }

            // Global config
            JObject global = storedConfig["global"] as JObject;
            SystemConfigPayload gPayload = new SystemConfigPayload(global);
            _config.FromPayload(gPayload);

            // Relay configs
            JArray relays = storedConfig["relays"] as JArray;
            if (relays != null) {
                foreach (JObject relayObj in relays) {
                    RelayConfigPayload payload = new RelayConfigPayload(relayObj);
                    RelayOut.FromPayload(payload);
                }
            } else {
                Console.WriteLine("Error: Missing relay config.");
                throw new Exception("Error Reading Relay Config");
            }

            // Analog inputs
            JArray analogIns = storedConfig["analogInputs"] as JArray;
            if (analogIns != null) {
                foreach (JObject ainObj in analogIns) {
                    AInConfigPayload payload = new AInConfigPayload(ainObj);
                    AnalogIn.FromPayload(payload);
                }
            } else {
                Console.WriteLine("Error: Missing analog inputs config.");
                throw new Exception("Error Reading Analog In Config");
            }

            // Analog outputs
            JArray analogOuts = storedConfig["analogOutputs"] as JArray;
            if (analogOuts != null) {
                foreach (JObject aoutObj in analogOuts) {
                    AOutConfigPayload payload = new AOutConfigPayload(aoutObj);
                    AnalogOut.FromPayload(payload);
                }
            } else {
                Console.WriteLine("Error: Missing analog outputs config.");
                throw new Exception("Error Reading Analog Out Config");
            }

            // Digital inputs
            JArray digitalIns = storedConfig["digitalInputs"] as JArray;
            if (digitalIns != null) {
                foreach (JObject dinObj in digitalIns) {
                    DInConfigPayload payload = new DInConfigPayload(dinObj);
                    DigitalIn.FromPayload(payload);
                }
            } else {
                Console.WriteLine("Error: Missing digital inputs config.");
                throw new Exception("Error Reading Digital In Config");
            }

            // PID configs
            JArray pids = storedConfig["pids"] as JArray;
            if (pids != null) {
                foreach (JObject obj in pids) {
                    PIDs.UnpackConfig(obj);
                }
            } else {
                Console.WriteLine("Error: Missing PID config.");
                throw new Exception("Error Reading PID Config");
            }

            // Logger config
            JObject logger = storedConfig["logger"] as JObject;
            Logger.SetConfig(logger);

            JObject network = storedConfig["network"] as JObject;
            Network.FromFram(network);


            // Initialize hardware with restored settings
            RelayOut.Initialize();
            AnalogIn.Initialize();
            AnalogOut.Initialize();
            DigitalIn.Initialize();
            PIDs.Initialize();

            for (uint i = 0; i < 4; i++) {
                (double v, bool a) val = GetAOutValue(i);

                JObject pidVal = GetPIDCurVal(i, true);
                _ = Dispatcher.NotifyPIDChanged(i, pidVal);
                _ = Dispatcher.NotifyAOutChanged(i, val.v, val.a);
            }

            SetupSnapshot();

            // Start background sampling
            StartAnalogLoop();
            StartDInLoop();
            StartPIDLoop();
            StartLoggerLoop();
        }

        public void StartLoggerLoop() {
            if (!Logger.Config.Enabled) {
                Logger.Config.Status = "Logger disabled by config";
                return;
            }

            _ctsLogger = new CancellationTokenSource();
            Task.Run(async () => {
                try {
                    // --- Write header once at start ---
                    int configId = await Logger.WriteConfigHeaderAsync();
                    if (configId > 0)
                        Logger.Config.Status = $"Logger started (config_id={configId})";
                    else
                        Logger.Config.Status = "Logger started (header skipped)";
                } catch (Exception ex) {
                    Logger.Config.Status = "Logger header error: " + ex.Message;
                }

                await LoggerLoop(_ctsLogger.Token);
            });
        }

        private async Task RestartLoggerAfterConfigChangeAsync() {
            bool wasRunning = _ctsLogger != null && !_ctsLogger.IsCancellationRequested && Logger.Config.Enabled;

            if (wasRunning) {
                StopLoggerLoop();
                Logger.Config.Status = "Logger paused for config update...";
                await Task.Delay(100); // short grace period
            }

            // Re-write config to database
            int configId = await Logger.WriteConfigHeaderAsync();

            if (wasRunning && configId > 0) {
                StartLoggerLoop();
                Logger.Config.Status = $"Logger resumed (new config_id={configId})";
            }
        }

        private async Task LoggerLoop(CancellationToken token) {
            TimeSpan interval = TimeSpan.FromSeconds(Logger.Config.IntervalSec);

            while (!token.IsCancellationRequested) {
                try {
                    Dictionary<string, double> snapshot = GetSnapshot();
                    await Logger.LogSnapshotAsync(snapshot);
                } catch (Exception ex) {
                    Logger.Config.Status = "Logger error: " + ex.Message;
                }


                try {
                    Dispatcher.NotifyLoggerChanged(Logger.Config.Status, true);
                    await Task.Delay(interval, token);
                } catch (TaskCanceledException) {
                    Dispatcher.NotifyLoggerChanged(Logger.Config.Status, false);
                    break;
                }
            }

            Logger.Config.Status = "Logger stopped";
        }

        public void StopLoggerLoop() {
            _ctsLogger?.Cancel();
        }
        private void StartPIDLoop() {
            _ctsPID = new CancellationTokenSource();
            Task.Run(() => SamplePIDLoop(_ctsPID.Token));
        }
        private void StopPIDLoop() {
            throw new NotImplementedException();
        }
        private void StartDInLoop() {
            _ctsDIn = new CancellationTokenSource();
            Task.Run(() => SampleDigitalInLoop(_ctsDIn.Token));
        }

        private async Task SamplePIDLoop(CancellationToken token) {
            while (!token.IsCancellationRequested) {
                await Task.Delay(100, token);
                PIDs.ComputeAll();
                for(uint chan =0; chan<4;  chan++) {
                    if (PIDs.IsEnabled(chan)) {
                        JObject payload = PIDs.PackCurVal(chan);
                        _ = Dispatcher.NotifyPIDChanged(chan, payload);
                    }
                }
            }
        }
        private async Task SampleDigitalInLoop(CancellationToken token) {
            while (!token.IsCancellationRequested) {
                await Task.Delay(1, token);
                if (DigitalIn.SampleInputs()) {
                    _ = Dispatcher.NotifyDInChanged(DigitalIn.States);
                }
            }
        }

        public void StopDInLoop() {
            _ctsDIn?.Cancel();
        }

        public JObject ReadConfigFromFram() {
            try {
                // Read 2-byte length prefix from start of FRAM
                byte[] lenBytes = FRAM.ReadBytes(0x0000, 2);
                int length = (lenBytes[0] << 8) | lenBytes[1];

                if (length <= 0 || length > 8190) {
                    Console.WriteLine("Invalid FRAM length header");
                    return null;
                }

                // Read JSON payload bytes
                byte[] jsonBytes = FRAM.ReadBytes(0x0002, length);

                // Decode to string
                string json = System.Text.Encoding.UTF8.GetString(jsonBytes);

                // Parse JSON to JObject
                JObject payload = JObject.Parse(json);

                _ = RestartLoggerAfterConfigChangeAsync();

                return payload;
            } catch (Exception ex) {
                Console.WriteLine("Error reading config from FRAM: " + ex.Message);
                return null;
            }
        }

        public void StartAnalogLoop() {
            _cts = new CancellationTokenSource();
            Task.Run(() => SampleAnalogInLoop(_cts.Token));
        }

        public void StopAnalogLoop() {
            _cts?.Cancel();
        }

        private async Task SampleAnalogInLoop(CancellationToken token) {
            for (uint chan = 0; chan < 4; chan++) {
                AnalogIn.ApplyMux(chan);
                AnalogIn.ApplyInputRange(chan);
            }
            switch (_config.AnalogSamplingRate) { 
                case SamplesPerSecond.Low:
                    AnalogIn.SetDataRate(Ads1115Driver.Ads1115DataRate.SPS8);
                    break;
                case SamplesPerSecond.Medium:
                    AnalogIn.SetDataRate(Ads1115Driver.Ads1115DataRate.SPS32);
                    break;
                case SamplesPerSecond.High:
                    AnalogIn.SetDataRate(Ads1115Driver.Ads1115DataRate.SPS475);
                    break;
                default:
                    throw new ArgumentException();
            }
            while (!token.IsCancellationRequested) {
                await Task.Delay(1, token);
                if (AnalogIn.GetValue()) {
                    _ = Dispatcher.NotifyAInChanged(AnalogIn.CurrentValues());
                }
            }
        }

        // --- Relay Section ---
        public void SetRelay(uint relay, bool state) {
            if (RelayOut.ChangeState(relay, state)) {
                _ = Dispatcher.NotifyRelayChange(relay, state);
            }
        }

        public bool GetRelayOutput(uint channel) {
            if (channel > 7)
                throw new ArgumentOutOfRangeException(nameof(channel));
            return RelayOut.GetState(channel);
        }
        // --- Analog Input Section ---
        public double GetAnalogIn(uint channel) {
            return AnalogIn.CurrentReading(channel);
        }
        public double GetAnalogIn(string channel) {
            if (!channel.StartsWith("AIN", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"Invalid channel name: {channel}");

            uint index = uint.Parse(channel.Substring(3));
            return AnalogIn.CurrentReading(index);
        }
        public AInConfigPayload GetAnalogInConfig(uint channel) {
            return AnalogIn.ToPayload(channel);
        }
        public void SetAInConfig(AInConfigPayload payload) {
            AnalogIn.FromPayload(payload);
            SaveConfigToFram();
            RestartSamplingLoop();
        }



        // --- Digital Input Section ---
        public bool GetDIn(uint channel) {
            return DigitalIn.GetInput(channel);
        }

        // --- Analog Output Section ---
        public (double, bool) GetAOutValue(uint channel) {
            if (channel > 3)
                throw new ArgumentOutOfRangeException(nameof(channel));
            return AnalogOut.GetAnalogValue(channel);
        }
        public void SetAOutFromPID(uint channel, double val) {
            if (channel > 3)
                throw new ArgumentOutOfRangeException(nameof(channel));
            AnalogOut.SetFromPID(channel, val);
        }
        public void SetAOutValue(uint channel, double val) {
            if (channel > 3)
                throw new ArgumentOutOfRangeException(nameof(channel));

            bool isAuto = AnalogOut.GetIsAuto(channel);
            AnalogOut.SetAnalogValue(channel, val);
            Dispatcher.NotifyAOutChanged(channel, val, isAuto);

            uint pidCh = PIDs.IsOutputUsed(channel);
            if (pidCh != 4) {
                JObject payload = PIDs.PackCurVal(channel);
                _ = Dispatcher.NotifyPIDChanged(channel, payload);
            }
        }
        public void SetAOutAuto(uint channel, bool val) {
            if (channel > 3)
                throw new ArgumentOutOfRangeException(nameof(channel));

            AnalogOut.SetIsAuto(channel, val);

            (double val, bool auto) v = GetAOutValue(channel);
            if (Dispatcher != null) {
                Dispatcher.NotifyAOutChanged(channel, v.val, v.auto);
            }
        }
        public bool GetAOutAuto(uint channel) {
            if (channel > 3)
                throw new ArgumentOutOfRangeException(nameof(channel));
            bool result = AnalogOut.GetIsAuto(channel);
            return result;
        }
        public AOutConfigPayload GetAOutConfig(uint channel) {
            if (channel > 3)
                throw new ArgumentOutOfRangeException(nameof(channel));
            return AnalogOut.GetConfig(channel);
        }
        public void SetAOutConfig(AOutConfigPayload payload) {
                AnalogOut.SetConfig(payload);
                SaveConfigToFram();
        }

        public SystemConfigPayload GetGlobalConfig() {
            LoadSystemIdentity();
            SystemConfigPayload payload = _config.ToPayload();
            return payload; 
        }
        public void ApplyGlobalConfig(SystemConfigPayload payload) {
            _config.FromPayload(payload);
            RestartSamplingLoop();
            AnalogOut.SetFrequency(_config.PWMFrequency);
            SaveConfigToFram();
        }
        private void RestartSamplingLoop() {
            StopAnalogLoop();
            StartAnalogLoop();
        }

        public RelayConfigPayload GetRelayConfig(uint channel) {
            return RelayOut.ToPayload(channel);
        }

        public void SetRelayConfig(RelayConfigPayload payload) {
            RelayOut.FromPayload(payload);
            SaveConfigToFram();
        }

        public DInConfigPayload GetDInConfig(uint channel) {
            return DigitalIn.ToPayload(channel);
        }

        public void SetDInConfig(DInConfigPayload payload) {
            DigitalIn.FromPayload(payload);
            SaveConfigToFram();
        }

        public void ClearAllLastValues() {
            AnalogIn.ClearAllLastValues();
            DigitalIn.ClearAllLastValues();
            RelayOut.ClearAllLastValues();
            AnalogOut.ClearAllLastValues();
            PIDs.ClearAllLastValues();
        }
        public void SaveConfigToFram() {
            try {
                JObject config = new JObject {
                    ["global"] = _config.ToFram(),
                    ["network"] = Network.ToFram(),
                    ["relays"] = RelayOut.GenerateAllPayload(),
                    ["analogInputs"] = AnalogIn.GenerateAllPayload(),
                    ["analogOutputs"] = AnalogOut.GenerateAllPayload(),
                    ["digitalInputs"] = DigitalIn.GenerateAllPayload(),
                    ["pids"] = PIDs.GenerateAllPayload(),
                    ["logger"] = Logger.GetConfig(),
                };

                string json = config.ToString(Newtonsoft.Json.Formatting.None);
                byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
                int length = jsonBytes.Length;


                if (length > 8190)
                    throw new Exception("Config payload too large for FRAM");

                // Write length header
                byte[] lengthBytes = new byte[2];
                lengthBytes[0] = (byte)((length >> 8) & 0xFF);
                lengthBytes[1] = (byte)(length & 0xFF);
                FRAM.WriteBytes(0x0000, lengthBytes);

                // Write JSON payload
                FRAM.WriteBytes(0x0002, jsonBytes);
            } catch (Exception ex) {
                Console.WriteLine("Error saving config to FRAM: " + ex.Message);
            }
        }

        public JObject GetPIDConfig(uint channel) {
            JObject obj = PIDs.PackConfig(channel);
            return obj;
        }

        public void SetPIDConfig(JObject payload) {
            PIDs.UnpackConfig(payload);
            SaveConfigToFram();
        }

        public JObject GetPIDCurVal(uint channel, bool all = false) {
            if (all) {
                // Force full transmission for THIS SPECIFIC CHANNEL
                PID_LOOP.Config cfg = PIDs.Configs[channel];

                JObject obj = new JObject {
                    ["sp"] = cfg.SP,
                    ["pv"] = cfg.PV,
                    ["out"] = cfg.Output,
                    ["auto"] = cfg.Auto,
                    ["dev"] = cfg.Dev,
                    ["tol"] = cfg.Tol
                };

                return obj;
            }

            // Normal behavior: only changed fields
            return PIDs.PackCurVal(channel);
        }

        public void SetPIDCurVal(JObject payload) {
            PIDs.UnpackCurVal(payload);
        }

        public void ResetPID(uint channel) {
            PIDs.Reset(channel);
        }

        public JObject GetLoggerConfig() {
            JObject payload = Logger.GetConfig();
            return payload;
        }

        public void SetLoggerConfig(JObject payload) {
            // Update the current configuration
            Logger.SetConfig(payload);

            bool shouldRun = Logger.Config.Enabled;
            bool isRunning = _ctsLogger != null && !_ctsLogger.IsCancellationRequested;

            // Start or stop logging based on the config change
            if (shouldRun && !isRunning) {
                Logger.Config.Status = "Starting logger...";
                StartLoggerLoop();
            } else if (!shouldRun && isRunning) {
                Logger.Config.Status = "Stopping logger...";
                StopLoggerLoop();
            } else {
                // No change to running state, but still update status
                Logger.Config.Status = shouldRun ? "Logger running" : "Logger disabled";
            }
            SaveConfigToFram();
        }

        public string GetLoggerStatus() {
            return Logger.GetStatus();
        }

        public bool IsLoggingEnabled() {
            return Logger.IsLogging();
        }
        public Config GlobalConfig() {
            LoadSystemIdentity();
            return _config;
        }

        private void LoadSystemIdentity() {
            try {
                LauncherConfig lc = LauncherConfig.Load();

                _config.SoftwareVersion = lc.Version;
                _config.HardwareVersion = lc.Revision;
            } catch (Exception ex) {
                Console.WriteLine("Error loading system identity: " + ex.Message);
                _config.SoftwareVersion = "unknown";
                _config.HardwareVersion = "unknown";
            }
        }

        public void GetWiFiStatus(JObject status) {

            if (status["sections"] == null)
                status["sections"] = new JArray();

            JArray sections = (JArray)status["sections"];

            NetworkSettingsPayload netSettings = GetNetworkConfig();
            JArray list = new JArray();

            list.Add(new JObject {
                ["label"] = "SSID",
                ["data"] = netSettings.WifiSsid ?? ""
            });

            list.Add(new JObject {
                ["label"] = "PSK",
                ["data"] = netSettings.WifiPsk ?? "",
            });

            sections.Add(new JObject {
                ["section"] = "WiFi",
                ["items"] = list
            });
        }

        public void GetSystemStatus(JObject status) {

            // ensure sections exists
            if (status["sections"] == null)
                status["sections"] = new JArray();

            JArray sections = (JArray)status["sections"];

            Config cfg = GlobalConfig();
            JArray list = new JArray();

            list.Add(new JObject { ["label"] = "Name", ["data"] = cfg.SystemName });
            list.Add(new JObject { ["label"] = "Version", ["data"] = cfg.SoftwareVersion });
            list.Add(new JObject { ["label"] = "Revision", ["data"] = cfg.HardwareVersion });
            list.Add(new JObject { ["label"] = "Serial Number", ["data"] = cfg.SystemSerialNumber });

            sections.Add(new JObject {
                ["section"] = "SigCore",
                ["items"] = list
            });
        }

        public void FactoryReset(string serialNo, string systemName, string ver, string rev, 
                                    string manifestURL, string manifestSigUrl, string url, string signature,
                                    string hostname ) {
            LauncherConfig lc = LauncherConfig.Load();

            lc.Revision = rev;
            lc.Version = ver;

            lc.Save();

            DeviceConfig devConfig = new DeviceConfig();
            devConfig.ManifestUrl = manifestURL;
            devConfig.ManifestSigUrl = manifestSigUrl;
            devConfig.Url = url;
            devConfig.Signature = signature;
            devConfig.Save();

            _config.SystemName = systemName;

            NetworkSettingsPayload payload = Network.GetConfig();
            payload.Hostname = hostname;
            Network.SetConfig(payload);

            _config.SystemSerialNumber = serialNo;

            SaveConfigToFram();

            FactoryResetAIn();
            FactoryResetAOut();
            FactoryResetDIn();
            FactoryResetRelays();
            FactoryResetPIDs();
            FactoryResetLogger();
        }

        // =============================================================
        // FACTORY RESET: ANALOG INPUTS
        // =============================================================
        private void FactoryResetAIn() {
            Console.WriteLine("FactoryResetAIn");

            for (uint ch = 0; ch < 4; ch++) {
                // Reset to default names/units/ranges/etc.
                var cfg = new A_IN.AInConfig {
                    Name = $"",
                    Units = "V",
                    AveragingSamples = 1,
                    CalibrationType = A_IN.CalType.None,
                    M = 1.0,
                    B = 0.0,
                    InputRange = A_IN.Range.Voltage5V,
                    Display = A_IN.DisplayFormat.FixedPoint,
                    Precision = 3
                };

                // Push to driver
                AnalogIn.FromPayload(cfg.ToPayload(ch));
            }

            AnalogIn.ClearAllLastValues();
        }

        // =============================================================
        // FACTORY RESET: ANALOG OUTPUTS
        // =============================================================
        private void FactoryResetAOut() {
            Console.WriteLine("FactoryResetAOut");

            for (uint ch = 0; ch < 4; ch++) {
                var cfg = new A_OUT.AnalogOutChannelConfig {
                    Name = $"",
                    Units = "V",
                    VoltageScaleM = 1.0,
                    VoltageScaleB = 0.0,
                    Mode = A_OUT.OutputMode.Voltage,
                    IsAuto = false,
                    Precision = 3,
                    Display = A_OUT.DisplayFormat.FixedPoint
                };

                // Reset hardware to 0V / 0%
                AnalogOut.FromPayload(cfg.ToPayload(ch));
                AnalogOut.SetAnalogValue(ch, 0.0);
            }

            // Set PWM frequency back to global config default
            AnalogOut.SetFrequency(_config.PWMFrequency);

            try { AnalogOut.ClearAllLastValues(); } catch { }
        }

        // =============================================================
        // FACTORY RESET: DIGITAL INPUTS
        // =============================================================
        private void FactoryResetDIn() {
            Console.WriteLine("FactoryResetDIn");

            for (uint ch = 0; ch < 8; ch++) {
                DInConfigPayload cfg = new DInConfigPayload();

                DigitalIn.FromPayload(cfg);
            }

            DigitalIn.ClearAllLastValues();
        }

        // =============================================================
        // FACTORY RESET: RELAYS
        // =============================================================
        private void FactoryResetRelays() {
            Console.WriteLine("FactoryResetRelays");

            for (uint ch = 0; ch < 8; ch++) {
                RELAY_OUT.RelayConfig cfg = new RELAY_OUT.RelayConfig {
                    Name = $"",
                    DefaultState = false,
                    FailSafeState = false
                };

                RelayOut.FromPayload(cfg.ToPayload(ch));

                // Reset physical state to OFF
                RelayOut.ChangeState(ch, false);
            }

            try { RelayOut.ClearAllLastValues(); } catch { }
        }

        // =============================================================
        // FACTORY RESET: PID CONFIG
        // =============================================================
        private void FactoryResetPIDs() {
            Console.WriteLine("FactoryResetPIDs");

            for (uint ch = 0; ch < 4; ch++) {
                var cfg = new PID_LOOP.Config {
                    Title = $"Disabled{ch}",
                    Enabled = false,
                    Auto = true,
                    SP = 0.0,
                    PV = 0.0,
                    Output = 0.0,
                    Dev = 0.0,
                    Tol = 0.0,

                    OutputMin = 0.0,
                    OutputMax = 5.0,
                    PVMin = 0.0,
                    PVMax = 5.0,

                    Kp = 10.0,
                    Ki = 0.01,
                    Kd = 0.0,

                    PvSource = ch,
                    OutputDestination = ch,

                    IntegralLimit = 1000.0,
                    ResetOnModeChange = false
                };

                PIDs.UnpackConfig(cfg.PackConfig(ch));
                PIDs.Reset(ch);
            }

            try { PIDs.ClearAllLastValues(); } catch { }
        }

        // =============================================================
        // FACTORY RESET: LOGGER
        // =============================================================
        private void FactoryResetLogger() {
            Console.WriteLine("FactoryResetLogger");

            var cfg = new MYSQL_LOGGER.DataLoggerConfig {
                Enabled = false,
                IntervalSec = 1.0,
                Server = "192.168.0.xxx",
                Port = 3306,
                Database = "sigcore_data_logger",
                User = "logger_user",
                Password = "logger_password",
                TableName = "data_log",
                SchemaVersion = "1.0",
                ConnectionTimeoutSec = 5,
                CommandTimeoutSec = 10,
                Status = "Factory reset"
            };

            Logger.SetConfig(cfg.ToPayload());
        }

        // ─────────────────────────────────────────────
        // NETWORK CONFIG
        // ─────────────────────────────────────────────

        public NetworkSettingsPayload GetNetworkConfig() {
            return Network.GetConfig();
        }

        public void SetNetworkConfig(NetworkSettingsPayload payload) {
            if (payload == null)
                return;

            Network.SetConfig(payload);
            SaveConfigToFram();
        }


        public enum SamplesPerSecond {
            Low,
            Medium,
            High
        }


        //public class Config {
        //    public string SystemName { get; set; } = "SigCore UC";
        //    public string SystemSerialNumber { get; set; } = "SCU123456";
        //    public string HardwareVersion { get; set; } = "1.0";
        //    public string SoftwareVersion { get; set; } = "0.9.0";

        //    public bool UseLastKnownValuesAtStartup { get; set; } = true;

        //    public SamplesPerSecond AnalogSamplingRate { get; set; } = SamplesPerSecond.Medium;
        //    public int PWMFrequency { get; set; } = 250;

        //    public bool IsPIDOpen { get; set; } = true;

        //    // Security
        //    public string SystemPassword { get; set; } = "admin";

        //    // Convert from JSON payload
        //    public void FromPayload(JObject payload) {
        //        if (payload == null) return;

        //        SystemName = (string)payload["systemName"] ?? SystemName;
        //        SystemSerialNumber = (string)payload["systemSerialNumber"] ?? SystemSerialNumber;
        //        UseLastKnownValuesAtStartup = (bool?)payload["useLastKnownValues"] ?? UseLastKnownValuesAtStartup;

        //        string sps = (string)payload["analogSamplingRate"];
        //        if (!string.IsNullOrEmpty(sps) &&
        //            Enum.TryParse(sps, true, out SamplesPerSecond rate)) {
        //            AnalogSamplingRate = rate;
        //        }

        //        JToken pwmToken = payload["pwm_frequency"];
        //        if (pwmToken != null && pwmToken.Type != JTokenType.Null)
        //            PWMFrequency = pwmToken.Value<int>();

        //        IsPIDOpen = (bool?)payload["isPIDOpen"] ?? IsPIDOpen;

        //        HardwareVersion = (string)payload["hardwareVersion"] ?? HardwareVersion;
        //        SoftwareVersion = (string)payload["softwareVersion"] ?? SoftwareVersion;

        //        if (payload.ContainsKey("systemPassword"))
        //            SystemPassword = (string)payload["systemPassword"] ?? SystemPassword;
        //    }

        //    // Convert to JSON payload
        //    public JObject ToPayload() {
        //        JObject payload = new JObject();
        //        serialize(payload);
        //        return payload;
        //    }

        //    private void serializeToFram(JObject payload) {
        //        payload["systemName"] = SystemName;
        //        payload["systemSerialNumber"] = SystemSerialNumber;
        //        payload["useLastKnownValues"] = UseLastKnownValuesAtStartup;
        //        payload["analogSamplingRate"] = AnalogSamplingRate.ToString();
        //        payload["pwm_frequency"] = PWMFrequency.ToString();
        //    }

        //    private void serialize(JObject payload) {
        //        serializeToFram(payload);

        //        payload["isPIDOpen"] = IsPIDOpen;
        //        payload["systemPassword"] = SystemPassword;
        //        payload["hardwareVersion"] = HardwareVersion;
        //        payload["softwareVersion"] = SoftwareVersion;
        //    }

        //    internal JObject ToFram() {
        //        JObject payload = new JObject();
        //        serializeToFram(payload);
        //        return payload;
        //    }
        //}

        public class Config {

            public string SystemName { get; set; } = "SigCore UC";
            public string SystemSerialNumber { get; set; } = "SCU123456";
            public string HardwareVersion { get; set; } = "1.0";
            public string SoftwareVersion { get; set; } = "0.9.0";

            public bool UseLastKnownValuesAtStartup { get; set; } = true;

            public SamplesPerSecond AnalogSamplingRate { get; set; } = SamplesPerSecond.Medium;
            public int PWMFrequency { get; set; } = 250;

            public bool IsPIDOpen { get; set; } = true;

            // Security
            public string SystemPassword { get; set; } = "admin";

            // ─────────────────────────
            // Convert FROM payload
            // ─────────────────────────

            public void FromPayload(SystemConfigPayload payload) {
                if (payload == null)
                    return;

                SystemName = payload.SystemName;
                SystemSerialNumber = payload.SystemSerialNumber;
                UseLastKnownValuesAtStartup = payload.UseLastKnownValuesAtStartup;

                AnalogSamplingRate = (SamplesPerSecond)payload.AnalogSamplingRate;
                PWMFrequency = payload.PWMFrequency;

                IsPIDOpen = payload.IsPIDOpen;

                HardwareVersion = payload.HardwareVersion;
                SoftwareVersion = payload.SoftwareVersion;

                SystemPassword = payload.SystemPassword;
            }

            // ─────────────────────────
            // Convert TO full payload
            // ─────────────────────────

            public SystemConfigPayload ToPayload() {
                SystemConfigPayload cfg = BuildPayload();
                return cfg;
            }

            // ─────────────────────────
            // Convert TO FRAM payload
            // ─────────────────────────

            internal JObject ToFram() {
                SystemConfigPayload cfg = BuildPayload();
                return cfg.ToFramPayload();
            }

            // ─────────────────────────
            // Internal mapping
            // ─────────────────────────

            private SystemConfigPayload BuildPayload() {
                SystemConfigPayload cfg = new SystemConfigPayload();

                cfg.SystemName = SystemName;
                cfg.SystemSerialNumber = SystemSerialNumber;
                cfg.UseLastKnownValuesAtStartup = UseLastKnownValuesAtStartup;

                cfg.AnalogSamplingRate = (int)AnalogSamplingRate;
                cfg.PWMFrequency = PWMFrequency;

                cfg.IsPIDOpen = IsPIDOpen;
                cfg.SystemPassword = SystemPassword;
                cfg.HardwareVersion = HardwareVersion;
                cfg.SoftwareVersion = SoftwareVersion;

                return cfg;
            }
        }
    }
}
