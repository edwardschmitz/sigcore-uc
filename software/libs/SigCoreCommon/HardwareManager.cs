using Newtonsoft.Json.Linq;
using SigCoreCommon.Payloads;
using SigCoreCommon.Update;
using System;
using System.IO;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static SigCoreCommon.HardwareManager;

namespace SigCoreCommon {
    public class HardwareManager {
        private static readonly Log EventLog = new Log();

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

        private Task _analogTask;
        private Task _dInTask;
        private Task _pidTask;

        private readonly object _analogLoopLock = new object();
        private readonly object _dInLoopLock = new object();
        private readonly object _pidLoopLock = new object();

        private readonly Dictionary<string, double> snapshot;


        private Config _config;
        public IDispatcher Dispatcher { get; set; }

        public HardwareManager() {
            RelayOut = new RELAY_OUT();
            AnalogIn = new A_IN();
            AnalogOut = new A_OUT();
            DigitalIn = new D_IN();
            PIDs = new PID_LOOP(this);
            try {
                FRAM = new Fm24cl64bDriver();
            } catch {
                // FRAM is not required on current hardware.
                FRAM = null;
            }
            Logger = new MYSQL_LOGGER(this);
            Network = new NetworkManager();


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

            JObject storedConfig = _config.Load(FRAM, CreateDefaultConfig);

            // Relay configs
            JArray relays = storedConfig["relays"] as JArray;
            if (relays != null) {
                foreach (JObject relayObj in relays) {
                    RelayConfigPayload payload = new RelayConfigPayload(relayObj);
                    RelayOut.FromPayload(payload);
                }
            } else {
                EventLog.Error("Configuration", "MissingRelayConfig", "Relay configuration is missing.");
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
                EventLog.Error("Configuration", "MissingAnalogInputConfig", "Analog input configuration is missing.");
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
                EventLog.Error("Configuration", "MissingAnalogOutputConfig", "Analog output configuration is missing.");
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
                EventLog.Error("Configuration", "MissingDigitalInputConfig", "Digital input configuration is missing.");
                throw new Exception("Error Reading Digital In Config");
            }

            // PID configs
            JArray pids = storedConfig["pids"] as JArray;
            if (pids != null) {
                foreach (JObject obj in pids) {
                    PIDs.UnpackConfig(obj);
                }
            } else {
                EventLog.Error("Configuration", "MissingPidConfig", "PID configuration is missing.");
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
            lock (_pidLoopLock) {
                if (_pidTask != null && !_pidTask.IsCompleted)
                    return;

                _ctsPID?.Dispose();
                _ctsPID = new CancellationTokenSource();
                CancellationToken token = _ctsPID.Token;
                _pidTask = Task.Run(() => SamplePIDLoop(token));
            }
        }

        private void StopPIDLoop() {
            Task task;
            CancellationTokenSource cts;

            lock (_pidLoopLock) {
                task = _pidTask;
                cts = _ctsPID;
                cts?.Cancel();
            }

            WaitForLoopToStop(task, "PID");

            lock (_pidLoopLock) {
                if (ReferenceEquals(task, _pidTask)) {
                    _pidTask = null;
                    _ctsPID = null;
                    cts?.Dispose();
                }
            }
        }

        private void StartDInLoop() {
            lock (_dInLoopLock) {
                if (_dInTask != null && !_dInTask.IsCompleted)
                    return;

                _ctsDIn?.Dispose();
                _ctsDIn = new CancellationTokenSource();
                CancellationToken token = _ctsDIn.Token;
                _dInTask = Task.Run(() => SampleDigitalInLoop(token));
            }
        }

        private async Task SamplePIDLoop(CancellationToken token) {
            while (!token.IsCancellationRequested) {
                try {
                    await Task.Delay(100, token);

                    PIDs.ComputeAll();
                    for (uint chan = 0; chan < 4; chan++) {
                        if (PIDs.IsEnabled(chan)) {
                            JObject payload = PIDs.PackCurVal(chan);
                            if (payload != null)
                                _ = Dispatcher.NotifyPIDChanged(chan, payload);
                        }
                    }
                } catch (OperationCanceledException) when (token.IsCancellationRequested) {
                    break;
                } catch (Exception ex) {
                    EventLog.Error("Sampling", "PidLoopFailure", "PID sampling loop failed.", ex);
                    if (!await DelayAfterLoopError(token))
                        break;
                }
            }
        }

        private async Task SampleDigitalInLoop(CancellationToken token) {
            while (!token.IsCancellationRequested) {
                try {
                    await Task.Delay(1, token);
                    if (DigitalIn.SampleInputs())
                        _ = Dispatcher.NotifyDInChanged(DigitalIn.States);
                } catch (OperationCanceledException) when (token.IsCancellationRequested) {
                    break;
                } catch (Exception ex) {
                    EventLog.Error("Sampling", "DigitalInputLoopFailure", "Digital input sampling loop failed.", ex);
                    if (!await DelayAfterLoopError(token))
                        break;
                }
            }
        }

        public void StopDInLoop() {
            Task task;
            CancellationTokenSource cts;

            lock (_dInLoopLock) {
                task = _dInTask;
                cts = _ctsDIn;
                cts?.Cancel();
            }

            WaitForLoopToStop(task, "digital input");

            lock (_dInLoopLock) {
                if (ReferenceEquals(task, _dInTask)) {
                    _dInTask = null;
                    _ctsDIn = null;
                    cts?.Dispose();
                }
            }
        }

        public void StartAnalogLoop() {
            lock (_analogLoopLock) {
                if (_analogTask != null && !_analogTask.IsCompleted)
                    return;

                _cts?.Dispose();
                _cts = new CancellationTokenSource();
                CancellationToken token = _cts.Token;
                _analogTask = Task.Run(() => SampleAnalogInLoop(token));
            }
        }

        public void StopAnalogLoop() {
            Task task;
            CancellationTokenSource cts;

            lock (_analogLoopLock) {
                task = _analogTask;
                cts = _cts;
                cts?.Cancel();
            }

            WaitForLoopToStop(task, "analog input");

            lock (_analogLoopLock) {
                if (ReferenceEquals(task, _analogTask)) {
                    _analogTask = null;
                    _cts = null;
                    cts?.Dispose();
                }
            }
        }

        private async Task SampleAnalogInLoop(CancellationToken token) {
            try {
                while (!token.IsCancellationRequested) {
                    try {
                        ConfigureAnalogSampling();

                        while (!token.IsCancellationRequested) {
                            await Task.Delay(1, token);
                            if (AnalogIn.GetValue())
                                _ = Dispatcher.NotifyAInChanged(AnalogIn.CurrentValues());
                        }
                    } catch (OperationCanceledException) when (token.IsCancellationRequested) {
                        break;
                    } catch (Exception ex) {
                        EventLog.Error("Sampling", "AnalogInputLoopFailure", "Analog input sampling loop failed.", ex);
                        try { AnalogIn.DisableAllMuxes(); } catch { }

                        if (!await DelayAfterLoopError(token))
                            break;
                    }
                }
            } finally {
                // Normal stop, config restart, and unexpected exit all leave the
                // analog front end with no TMUX enabled.
                try { AnalogIn.DisableAllMuxes(); } catch { }
            }
        }

        private void ConfigureAnalogSampling() {
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
                    throw new ArgumentOutOfRangeException(nameof(_config.AnalogSamplingRate));
            }
        }

        private static async Task<bool> DelayAfterLoopError(CancellationToken token) {
            try {
                await Task.Delay(100, token);
                return true;
            } catch (OperationCanceledException) when (token.IsCancellationRequested) {
                return false;
            }
        }

        private static void WaitForLoopToStop(Task task, string loopName) {
            if (task == null)
                return;

            try {
                task.GetAwaiter().GetResult();
            } catch (OperationCanceledException) {
                // Expected when a loop is deliberately stopped.
            } catch (Exception ex) {
                EventLog.Warn("Sampling", "LoopStopFailure", $"{loopName} loop did not stop cleanly: {ex.Message}");
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
            StopAnalogLoop();
            try {
                AnalogIn.FromPayload(payload);
                SaveConfig();
            } finally {
                StartAnalogLoop();
            }
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
            SaveConfig();
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
            SaveConfig();
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
            SaveConfig();
        }

        public DInConfigPayload GetDInConfig(uint channel) {
            return DigitalIn.ToPayload(channel);
        }

        public void SetDInConfig(DInConfigPayload payload) {
            StopDInLoop();
            try {
                DigitalIn.FromPayload(payload);
                SaveConfig();
            } finally {
                StartDInLoop();
            }
        }

        public void ClearAllLastValues() {
            AnalogIn.ClearAllLastValues();
            DigitalIn.ClearAllLastValues();
            RelayOut.ClearAllLastValues();
            AnalogOut.ClearAllLastValues();
            PIDs.ClearAllLastValues();
        }
        public void SaveConfig() {
            _config.Save(BuildConfigPayload());
        }

        public JObject BuildConfigPayload() {
            return new JObject {
                ["global"] = _config.ToFram(),
                ["network"] = Network.ToFram(),
                ["relays"] = RelayOut.GenerateAllPayload(),
                ["analogInputs"] = AnalogIn.GenerateAllPayload(),
                ["analogOutputs"] = AnalogOut.GenerateAllPayload(),
                ["digitalInputs"] = DigitalIn.GenerateAllPayload(),
                ["pids"] = PIDs.GenerateAllPayload(),
                ["logger"] = Logger.GetConfig()
            };
        }

        private JObject CreateDefaultConfig() {
            _config.ResetToDefaults();
            FactoryResetAIn();
            FactoryResetAOut();
            FactoryResetDIn();
            FactoryResetRelays();
            FactoryResetPIDs();
            FactoryResetLogger();
            return BuildConfigPayload();
        }

        public JObject GetPIDConfig(uint channel) {
            JObject obj = PIDs.PackConfig(channel);
            return obj;
        }

        public void SetPIDConfig(JObject payload) {
            StopPIDLoop();
            try {
                PIDs.UnpackConfig(payload);
                SaveConfig();
            } finally {
                StartPIDLoop();
            }
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
            SaveConfig();
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
                LauncherConfig lc = LauncherConfig.Load("/opt/sigcore/current/config.json");

                _config.SoftwareVersion = lc.Version;
                _config.HardwareVersion = lc.Revision;
            } catch (Exception ex) {
                EventLog.Error("Configuration", "SystemIdentityLoadFailure", "System identity could not be loaded.", ex);
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

        public JObject GetUpdateConfig() {
            DeviceConfig cfg = DeviceConfig.Load();

            return new JObject {
                ["manifest_url"] = cfg.ManifestUrl ?? "",
                ["manifest_sig_url"] = cfg.ManifestSigUrl ?? "",
                ["url"] = cfg.Url ?? "",
                ["signature"] = cfg.Signature ?? ""
            };
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
                                    string hostname) {
            // Release identity is owned by the active version directory.
            // Factory Settings must not create or modify /opt/sigcore/config.json.
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

            StopAnalogLoop();
            StopDInLoop();
            StopPIDLoop();
            try {
                FactoryResetAIn();
                FactoryResetAOut();
                FactoryResetDIn();
                FactoryResetRelays();
                FactoryResetPIDs();
                FactoryResetLogger();

                SaveConfig();
            } finally {
                StartAnalogLoop();
                StartDInLoop();
                StartPIDLoop();
            }
        }

        // =============================================================
        // FACTORY RESET: ANALOG INPUTS
        // =============================================================
        private void FactoryResetAIn() {

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

                // Reset hardware to 0V / 0% and explicitly force manual mode.
                // Do not rely solely on payload/config translation for the runtime auto flag.
                AnalogOut.FromPayload(cfg.ToPayload(ch));
                AnalogOut.SetIsAuto(ch, false);
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

            for (uint ch = 0; ch < 8; ch++) {
                DInConfigPayload cfg = new DInConfigPayload {
                    Channel = ch,
                    Name = "",
                    DebounceMs = 50,
                    Inverted = false
                };

                DigitalIn.FromPayload(cfg);
            }

            DigitalIn.ClearAllLastValues();
        }

        // =============================================================
        // FACTORY RESET: RELAYS
        // =============================================================
        private void FactoryResetRelays() {

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

            for (uint ch = 0; ch < 4; ch++) {
                var cfg = new PID_LOOP.Config {
                    Title = $"Disabled{ch}",
                    Enabled = false,
                    Auto = false,
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

        public JObject VerifyFactoryReset(
            string serialNo,
            string systemName,
            string ver,
            string rev,
            string manifestURL,
            string manifestSigUrl,
            string url,
            string signature,
            string hostname) {

            JArray failures = new JArray();

            void Require(bool condition, string text) {
                if (!condition)
                    failures.Add(text);
            }

            SystemConfigPayload global = GetGlobalConfig();
            Require(global.SystemSerialNumber == serialNo, "Serial number did not persist.");
            Require(global.SystemName == systemName, "System name did not persist.");
            Require(global.SoftwareVersion == ver, "Software version did not persist.");
            Require(global.HardwareVersion == rev, "Hardware revision did not persist.");

            NetworkSettingsPayload network = GetNetworkConfig();
            Require(network.Hostname == hostname, "Configured hostname did not persist.");

            JObject update = GetUpdateConfig();
            Require((string)update["manifest_url"] == manifestURL, "Manifest URL did not persist.");
            Require((string)update["manifest_sig_url"] == manifestSigUrl, "Manifest signature URL did not persist.");
            Require((string)update["url"] == url, "Update URL did not persist.");
            Require((string)update["signature"] == signature, "Update signature URL did not persist.");

            for (uint ch = 0; ch < 4; ch++) {
                AInConfigPayload ai = GetAnalogInConfig(ch);
                Require(ai.Channel == ch, $"AI{ch + 1} channel identity is wrong.");
                Require(string.IsNullOrEmpty(ai.Name), $"AI{ch + 1} name was not cleared.");
                Require(ai.Units == "V", $"AI{ch + 1} units were not reset.");
                Require(ai.AveragingSamples == 1, $"AI{ch + 1} averaging was not reset.");
                Require(ai.Precision == 3, $"AI{ch + 1} precision was not reset.");
                Require(ai.CalibrationType == (int)A_IN.CalType.None, $"AI{ch + 1} calibration was not reset.");
                Require(ai.InputRange == (int)A_IN.Range.Voltage5V, $"AI{ch + 1} range was not reset.");
                Require(Math.Abs(ai.M - 1.0) < 0.0000001, $"AI{ch + 1} M was not reset.");
                Require(Math.Abs(ai.B) < 0.0000001, $"AI{ch + 1} B was not reset.");

                AOutConfigPayload ao = GetAOutConfig(ch);
                Require(ao.Channel == ch, $"AO{ch + 1} channel identity is wrong.");
                Require(string.IsNullOrEmpty(ao.Name), $"AO{ch + 1} name was not cleared.");
                Require(ao.Units == "V", $"AO{ch + 1} units were not reset.");
                Require(!ao.IsAuto, $"AO{ch + 1} was not reset to manual.");
                Require(Math.Abs(ao.VoltageScaleM - 1.0) < 0.0000001, $"AO{ch + 1} scale M was not reset.");
                Require(Math.Abs(ao.VoltageScaleB) < 0.0000001, $"AO{ch + 1} scale B was not reset.");

                (double value, bool autoMode) = GetAOutValue(ch);
                Require(Math.Abs(value) < 0.0000001, $"AO{ch + 1} output was not reset to zero.");
                Require(!autoMode, $"AO{ch + 1} output mode was not reset to manual.");
            }

            for (uint ch = 0; ch < 8; ch++) {
                DInConfigPayload di = GetDInConfig(ch);
                Require(di.Channel == ch, $"DI{ch + 1} channel identity is wrong.");
                Require(string.IsNullOrEmpty(di.Name), $"DI{ch + 1} name was not cleared.");
                Require(di.DebounceMs == 50, $"DI{ch + 1} debounce was not reset.");
                Require(!di.Inverted, $"DI{ch + 1} inversion was not reset.");

                RelayConfigPayload relay = GetRelayConfig(ch);
                Require(relay.Channel == ch, $"R{ch + 1} channel identity is wrong.");
                Require(string.IsNullOrEmpty(relay.Name), $"R{ch + 1} name was not cleared.");
                Require(!relay.DefaultState, $"R{ch + 1} default state was not reset.");
                Require(!relay.FailSafeState, $"R{ch + 1} fail-safe state was not reset.");
                Require(!relay.InvertedLogic, $"R{ch + 1} inverted logic was not reset.");
                Require(!GetRelayOutput(ch), $"R{ch + 1} physical state was not reset to OFF.");
            }

            for (uint ch = 0; ch < 4; ch++) {
                JObject pid = GetPIDConfig(ch);
                Require(pid.Value<bool?>("isEnabled") == false, $"PID{ch + 1} was not disabled.");
            }

            JObject logger = GetLoggerConfig();
            Require(logger.Value<bool?>("enabled") == false, "Data logger was not disabled.");

            return new JObject {
                ["success"] = failures.Count == 0,
                ["failures"] = failures
            };
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
            SaveConfig();
        }


        public enum SamplesPerSecond {
            Low,
            Medium,
            High
        }


        public class Config {

            private const int FramSize = 8192;
            private const int FramHeaderSize = 2;
            private const string ConfigFilePath = "/opt/sigcore/properties.json";
            private readonly Log _log;

            public string FilePath { get; }

            public string SystemName { get; set; } = "";
            public string SystemSerialNumber { get; set; } = "SCU123456";
            public string HardwareVersion { get; set; } = "1.0";
            public string SoftwareVersion { get; set; } = "0.9.0";

            public bool UseLastKnownValuesAtStartup { get; set; } = true;

            public SamplesPerSecond AnalogSamplingRate { get; set; } = SamplesPerSecond.Medium;
            public int PWMFrequency { get; set; } = 250;

            public bool IsPIDOpen { get; set; } = true;

            // Security
            public string SystemPassword { get; set; } = "admin";

            public Config(string filePath = null) {
                _log = new Log();

                FilePath = string.IsNullOrWhiteSpace(filePath)
                    ? ConfigFilePath
                    : filePath;

            }

            public JObject Load(
                Fm24cl64bDriver fram,
                Func<JObject> createDefaultConfig) {

                if (createDefaultConfig == null)
                    throw new ArgumentNullException(nameof(createDefaultConfig));

                if (File.Exists(FilePath)) {
                    JObject fileConfig = ReadFromFile();

                    if (IsValid(fileConfig)) {
                        LoadGlobalSettings(fileConfig);
                        return fileConfig;
                    }

                    _log.Warn(
                        "Configuration",
                        "InvalidPropertiesFile",
                        "The hardware properties file is invalid; defaults will be used.");

                    return CreateAndSaveDefault(createDefaultConfig);
                }

                JObject framConfig = ReadFromFram(fram);

                if (IsValid(framConfig)) {
                    LoadGlobalSettings(framConfig);
                    Save(framConfig);
                    return framConfig;
                }

                _log.Warn(
                    "Configuration",
                    "NoValidConfiguration",
                    "No valid hardware configuration was found; defaults will be used.");

                return CreateAndSaveDefault(createDefaultConfig);
            }

            public void Save(JObject config) {
                if (!IsValid(config)) {
                    _log.Error(
                        "Configuration",
                        "InvalidConfigurationSaveRejected",
                        "Refusing to save an invalid hardware configuration.");
                    throw new ArgumentException(
                        "Cannot save an invalid configuration.",
                        nameof(config));
                }

                try {
                    string directory = Path.GetDirectoryName(FilePath);
                    if (!string.IsNullOrWhiteSpace(directory))
                        Directory.CreateDirectory(directory);

                    string temporaryPath = FilePath + ".tmp";
                    string json = config.ToString(Newtonsoft.Json.Formatting.Indented);

                    File.WriteAllText(
                        temporaryPath,
                        json,
                        new UTF8Encoding(false));

                    File.Move(temporaryPath, FilePath, true);
                } catch (Exception ex) {
                    _log.Error(
                        "Configuration",
                        "PropertiesSaveFailure",
                        "Hardware properties could not be saved.",
                        ex);
                    throw;
                }
            }

            public void ResetToDefaults() {
                SystemName = "SigCore UC";
                SystemSerialNumber = "SCU123456";
                HardwareVersion = "1.0";
                SoftwareVersion = "0.9.0";
                UseLastKnownValuesAtStartup = true;
                AnalogSamplingRate = SamplesPerSecond.Medium;
                PWMFrequency = 250;
                IsPIDOpen = true;
                SystemPassword = "admin";
            }

            private JObject CreateAndSaveDefault(
                Func<JObject> createDefaultConfig) {

                JObject defaultConfig = createDefaultConfig();

                if (!IsValid(defaultConfig)) {
                    _log.Error(
                        "Configuration",
                        "GeneratedDefaultInvalid",
                        "The generated default hardware configuration is invalid.");
                    throw new InvalidOperationException(
                        "The default configuration is invalid.");
                }

                Save(defaultConfig);
                LoadGlobalSettings(defaultConfig);
                return defaultConfig;
            }

            private void LoadGlobalSettings(JObject config) {
                JObject global = config["global"] as JObject;
                FromPayload(new SystemConfigPayload(global));
            }

            private JObject ReadFromFile() {
                try {
                    string json = File.ReadAllText(FilePath, Encoding.UTF8);
                    return JObject.Parse(json);
                } catch (Exception ex) {
                    _log.Error(
                        "Configuration",
                        "PropertiesReadFailure",
                        "Hardware properties could not be read.",
                        ex);
                    return null;
                }
            }

            private JObject ReadFromFram(Fm24cl64bDriver fram) {
                if (fram == null) {
                    // FRAM is optional and absent on current hardware.
                    return null;
                }

                try {
                    fram.Initialize();

                    byte[] lengthBytes = fram.ReadBytes(0, FramHeaderSize);
                    int length = (lengthBytes[0] << 8) | lengthBytes[1];

                    if (length <= 0 || length > FramSize - FramHeaderSize) {
                        _log.Warn(
                            "Configuration",
                            "InvalidFramConfiguration",
                            "Legacy FRAM contains an invalid configuration length.");
                        return null;
                    }

                    byte[] jsonBytes = fram.ReadBytes(FramHeaderSize, length);
                    string json = Encoding.UTF8.GetString(jsonBytes);
                    return JObject.Parse(json);
                } catch (Exception ex) {
                    _log.Error(
                        "Configuration",
                        "FramReadFailure",
                        "Legacy FRAM configuration could not be read.",
                        ex);
                    return null;
                }
            }

            private static bool IsValid(JObject config) {
                if (config == null)
                    return false;

                return config["global"] is JObject &&
                       config["network"] is JObject &&
                       config["logger"] is JObject &&
                       HasCount(config["relays"], 8) &&
                       HasCount(config["analogInputs"], 4) &&
                       HasCount(config["analogOutputs"], 4) &&
                       HasCount(config["digitalInputs"], 8) &&
                       HasCount(config["pids"], 4);
            }

            private static bool HasCount(JToken token, int expectedCount) {
                return token is JArray array && array.Count == expectedCount;
            }

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
