using Iot.Device.Display;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Ports;
using System.Reactive.Joins;
using System.Reflection.Metadata;
using System.Runtime.Intrinsics.X86;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


namespace SigCoreCommon {




    public class SigCoreSystem : IDispatcher, IDisposable {
        private readonly SigCoreClient _client;
        private readonly ConcurrentDictionary<ulong, TaskCompletionSource<JObject>> _pending;
        private readonly TimeSpan _defaultTimeout = TimeSpan.FromMilliseconds(3000);
        private TaskCompletionSource<bool> _connectHandshakeTcs;

        // ===============================
        // Event Declarations
        // ===============================

        public event Action<bool[]> DigitalInChanged;
        public event Action<double[]> AnalogInChanged;
        public event Action<uint, bool> RelayChanged;
        public event Action<uint, double, bool> AnalogOutChanged;
        public event Action<uint, double> PIDSPChanged;
        public event Action<uint, double> PIDPVChanged;
        public event Action<uint, double> PIDOutputChanged;
        public event Action<uint, double> PIDTolChanged;
        public event Action<uint, double> PIDDevChanged;
        public event Action<uint, bool> PIDAutoChanged;
        public event Action<uint, double> PIDRampTimeChanged;
        public event Action<uint, double> PIDRampTargetChanged;
        public event Action<uint, bool> PIDRampChanged;
        public event Action<string, bool> LoggerStatusChanged;


        public bool IsConnected => _client.IsConnected;
        public bool IsCommander { get; set; }

        public SigCoreSystem() {
            _client = new SigCoreClient(this);
            _pending = new ConcurrentDictionary<ulong, TaskCompletionSource<JObject>>();
            _connectHandshakeTcs = new TaskCompletionSource<bool>();
        }

        public async Task<bool> ConnectAsync(string ip, int port = 7020) {
            bool result;

            _connectHandshakeTcs = new TaskCompletionSource<bool>();
            result = await _client.ConnectAsync(ip, port);
            if (result) {
                await _connectHandshakeTcs.Task;
            }
            return result;
        }

        public void Close() {
            _client.Close();
        }

        public void Dispose() {
            _client.Close();
        }

        // ===============================
        // Pattern A — Request / Response
        // ===============================
        private async Task<T> RequestAsync<T>(SigCoreMessage msg, Func<JObject, T> parser, int timeoutMs = 3000) {
            TaskCompletionSource<JObject> tcs = new TaskCompletionSource<JObject>();
            _pending[msg.MsgId] = tcs;
            try {
                await _client.SendAsync(msg);
                Task delay = Task.Delay(timeoutMs);
                Task completed = await Task.WhenAny(tcs.Task, delay);
                if (completed == delay) throw new TimeoutException();
                JObject payload = await tcs.Task;
                return parser(payload);
            } finally {
                _pending.TryRemove(msg.MsgId, out _);
            }
        }

        private async Task<JObject> RequestAsync(SigCoreMessage msg, int timeoutMs = 3000) {
            TaskCompletionSource<JObject> tcs = new TaskCompletionSource<JObject>();
            _pending[msg.MsgId] = tcs;
            try {
                await _client.SendAsync(msg);
                Task delay = Task.Delay(timeoutMs);
                Task completed = await Task.WhenAny(tcs.Task, delay);
                if (completed == delay) throw new TimeoutException();
                return await tcs.Task;
            } finally {
                _pending.TryRemove(msg.MsgId, out _);
            }
        }

        // ===============================
        // Pattern B — Fire and Forget
        // ===============================
        private async Task SendCommandAsync(SigCoreMessage msg) {
            await _client.SendAsync(msg);
        }

        // ===============================
        // Pattern D — Initialization
        // ===============================
        private async Task InitializeGroupAsync(Func<uint, Task> initFunc, int count) {
            for (uint i = 0; i < count; i++) await initFunc(i);
        }

        // ===============================
        // Pattern E — Config Cycle
        // ===============================
        private async Task<TConfig> ConfigureAsync<TConfig>(
            uint channel,
            Func<uint, Task<TConfig>> getFunc,
            Func<uint, TConfig, Task> setFunc,
            Func<TConfig, TConfig> modifyFunc) {
            TConfig cfg = await getFunc(channel);
            TConfig newCfg = modifyFunc(cfg);
            await setFunc(channel, newCfg);
            return newCfg;
        }

        // ===============================
        // Pattern C — Alerts (generic)
        // ===============================
        private void OnAlert<T>(Action<T> handler, T payload) {
            if (handler != null) handler(payload);
        }
        private void OnAlert<T1, T2>(Action<T1, T2> handler, T1 a1, T2 a2) {
            handler?.Invoke(a1, a2);
        }
        private void OnAlert<T1, T2, T3>(Action<T1, T2, T3> handler, T1 a1, T2 a2, T3 a3) {
            handler?.Invoke(a1, a2, a3);
        }

        // ===============================
        // Core Bridge Helpers
        // ===============================

        // Converts an async operation returning T into a synchronous call.
        private T Sync<T>(Func<Task<T>> func) {
            return Task.Run(func).GetAwaiter().GetResult();
        }

        // Converts an async operation with no return value into a synchronous call.
        private void Sync(Func<Task> func) {
            Task.Run(func).GetAwaiter().GetResult();
        }



        // Combined connect + initialize pattern
        public async Task ConnectAndInitializeAsync(string ip, int port = 7020) {
            Console.WriteLine("[CONNECT] Connecting to SigCore server...");

            await ConnectAsync(ip, port);
            Console.WriteLine("[CONNECT] Connected. Starting initialization...");

        }

        // ===============================
        // Fire-and-Forget Commands
        // ===============================

        // ===============================================
        // FIRE-AND-FORGET + AWAITABLE ASYNC SETTERS
        // ===============================================

        public async Task SetRelayAsync(uint channel, bool state) {
            SigCoreMessage msg = SigCoreMessage.CreateSetRelay(channel, state);
            await _client.SendAsync(msg);
        }

        public void SetRelay(uint channel, bool state) {
            _ = SetRelayAsync(channel, state);
        }

        public async Task SetAOutValueAsync(uint channel, double value) {
            SigCoreMessage msg = SigCoreMessage.CreateSetAOut(channel, value);
            await _client.SendAsync(msg);
        }

        public void SetAOutValue(uint channel, double value) {
            _ = SetAOutValueAsync(channel, value);
        }

        public async Task SetRelayConfigAsync(uint channel, RELAY_OUT.RelayConfig config) {
            JObject payload = config.ToPayload(channel);
            SigCoreMessage msg = SigCoreMessage.CreateSetRelayConfig(channel, payload);
            await _client.SendAsync(msg);
        }

        public void SetRelayConfig(uint channel, RELAY_OUT.RelayConfig config) {
            _ = SetRelayConfigAsync(channel, config);
        }

        public async Task SetDInConfigAsync(uint channel, D_IN.DInConfig config) {
            JObject payload = config.ToPayload(channel);
            SigCoreMessage msg = SigCoreMessage.CreateSetDInConfig(channel, payload);
            await _client.SendAsync(msg);
        }

        public void SetDInConfig(uint channel, D_IN.DInConfig config) {
            _ = SetDInConfigAsync(channel, config);
        }

        public async Task SetAInConfigAsync(uint channel, A_IN.AInConfig config) {
            JObject payload = config.ToPayload(channel);
            SigCoreMessage msg = SigCoreMessage.CreateSetAInConfig(channel, payload);
            await _client.SendAsync(msg);
        }

        public void SetAInConfig(uint channel, A_IN.AInConfig config) {
            _ = SetAInConfigAsync(channel, config);
        }
        public async Task SetAOutConfigAsync(uint channel, A_OUT.AnalogOutChannelConfig config) {
            JObject payload = config.ToPayload(channel);
            SigCoreMessage msg = SigCoreMessage.CreateSetAOutConfig(channel, payload);
            await _client.SendAsync(msg);
        }

        public void SetAOutConfig(uint channel, A_OUT.AnalogOutChannelConfig config) {
            _ = SetAOutConfigAsync(channel, config);
        }

        public async Task SetPIDSPAsync(uint channel, double sp) {
            SigCoreMessage msg = SigCoreMessage.CreateSetPIDSP(channel, sp);
            await _client.SendAsync(msg);
        }
        public async Task SetPIDOutputAsync(uint channel, double output) {
            SigCoreMessage msg = SigCoreMessage.CreateSetPIDOutput(channel, output);
            await _client.SendAsync(msg);
        }
        public async Task SetPIDTolAsync(uint channel, double tol) {
            SigCoreMessage msg = SigCoreMessage.CreateSetPIDTol(channel, tol);
            await _client.SendAsync(msg);
        }
        public async Task SetPIDAutoAsync(uint channel, bool auto) {
            SigCoreMessage msg = SigCoreMessage.CreateSetPIDAuto(channel, auto);
            await _client.SendAsync(msg);
        }
        public async Task SetPIDRampTimeAsync(uint channel, double rampTime) {
            SigCoreMessage msg = SigCoreMessage.CreateSetPIDRampTime(channel, rampTime);
            await _client.SendAsync(msg);
        }
        public async Task SetPIDRampTargetAsync(uint channel, double rampTarget) {
            SigCoreMessage msg = SigCoreMessage.CreateSetPIDRampTarget(channel, rampTarget);
            await _client.SendAsync(msg);
        }
        public async Task SetPIDRampAsync(uint channel, bool ramp) {
            SigCoreMessage msg = SigCoreMessage.CreateSetPIDRamp(channel, ramp);
            await _client.SendAsync(msg);
        }

        public async Task ResetPIDAsync(uint channel) {
            SigCoreMessage msg = SigCoreMessage.CreateResetPID(channel);
            await _client.SendAsync(msg);
        }

        public void ResetPID(uint channel) {
            _ = ResetPIDAsync(channel);
        }

        public async Task SetPIDConfigAsync(uint channel, PID_LOOP.Config config) {
            JObject payload = config.PackConfig(channel);
            SigCoreMessage msg = SigCoreMessage.CreateSetPIDConfig(channel, payload);
            await _client.SendAsync(msg);
        }

        public void SetPIDConfig(uint channel, PID_LOOP.Config config) {
            _ = SetPIDConfigAsync(channel, config);
        }

        public async Task SetGlobalConfigAsync(HardwareManager.Config config) {
            JObject payload = config.ToPayload();
            SigCoreMessage msg = SigCoreMessage.CreateSetGlobalConfig(payload);
            await _client.SendAsync(msg);
        }

        public void SetGlobalConfig(HardwareManager.Config config) {
            _ = SetGlobalConfigAsync(config);
        }

        public void Subscribe() {
            _ = Task.Run(async () => {
                SigCoreMessage msg = SigCoreMessage.CreateSubscribe();
                await _client.SendAsync(msg);
            });
        }

        // ===============================
        // Query / Get Operations
        // ===============================

        public Task<A_IN.AInConfig> GetAInConfigAsync(uint channel) {
            SigCoreMessage msg = SigCoreMessage.CreateGetAInConfig(channel);
            return RequestAsync(msg, payload =>
            {
                A_IN.AInConfig cfg = new A_IN.AInConfig();
                cfg.FromPayload(payload);
                return cfg;
            });
        }

        public void GetAInConfig(uint channel, Action<A_IN.AInConfig> callback) {
            _ = Task.Run(async () => {
                SigCoreMessage msg = SigCoreMessage.CreateGetAInConfig(channel);
                A_IN.AInConfig cfg = await RequestAsync(msg, payload => {
                    A_IN.AInConfig config = new A_IN.AInConfig();
                    config.FromPayload(payload);
                    return config;
                });
                callback?.Invoke(cfg);
            });
        }

        public Task<double> GetAnalogInputAsync(uint channel) {
            SigCoreMessage msg = SigCoreMessage.CreateGetAnalogIn(channel);
            return RequestAsync(msg, payload => payload.Value<double>("value"));
        }
        public void GetAnalogInput(uint channel, Action<double> callback) {
            _ = Task.Run(async () => {
                SigCoreMessage msg = SigCoreMessage.CreateGetAnalogIn(channel);
                double value = await RequestAsync(msg, payload => payload.Value<double>("value"));
                callback?.Invoke(value);
            });
        }

        // ==========================================
        // DIGITAL INPUT
        // ==========================================

        public Task<D_IN.DInConfig> GetDInConfigAsync(uint channel) {
            SigCoreMessage msg = SigCoreMessage.CreateGetDInConfig(channel);
            return RequestAsync(msg, payload =>
            {
                D_IN.DInConfig cfg = new D_IN.DInConfig();
                cfg.FromPayload(payload);
                return cfg;
            });
        }

        public void GetDInConfig(uint channel, Action<D_IN.DInConfig> callback) {
            _ = Task.Run(async () =>
            {
                D_IN.DInConfig cfg = await GetDInConfigAsync(channel);
                callback?.Invoke(cfg);
            });
        }

        public Task<bool> GetDInAsync(uint channel) {
            SigCoreMessage msg = SigCoreMessage.CreateGetDIn(channel);
            return RequestAsync(msg, payload => payload.Value<bool>("state"));
        }

        public void GetDIn(uint channel, Action<bool> callback) {
            _ = Task.Run(async () =>
            {
                bool value = await GetDInAsync(channel);
                callback?.Invoke(value);
            });
        }

        // ==========================================
        // RELAY OUTPUT
        // ==========================================

        public Task<RELAY_OUT.RelayConfig> GetRelayConfigAsync(uint channel) {
            SigCoreMessage msg = SigCoreMessage.CreateGetRelayConfig(channel);
            return RequestAsync(msg, payload =>
            {
                RELAY_OUT.RelayConfig cfg = new RELAY_OUT.RelayConfig();
                cfg.FromPayload(payload);
                return cfg;
            });
        }

        public void GetRelayConfig(uint channel, Action<RELAY_OUT.RelayConfig> callback) {
            _ = Task.Run(async () =>
            {
                RELAY_OUT.RelayConfig cfg = await GetRelayConfigAsync(channel);
                callback?.Invoke(cfg);
            });
        }

        public Task<bool> GetRelayAsync(uint channel) {
            SigCoreMessage msg = SigCoreMessage.CreateGetRelay(channel);
            return RequestAsync(msg, payload => payload.Value<bool>("state"));
        }

        public void GetRelay(uint channel, Action<bool> callback) {
            _ = Task.Run(async () =>
            {
                bool value = await GetRelayAsync(channel);
                callback?.Invoke(value);
            });
        }

        // ==========================================
        // ANALOG OUTPUT
        // ==========================================

        public Task<A_OUT.AnalogOutChannelConfig> GetAOutConfigAsync(uint channel) {
            SigCoreMessage msg = SigCoreMessage.CreateGetAOutConfig(channel);
            return RequestAsync(msg, payload =>
            {
                A_OUT.AnalogOutChannelConfig cfg = new A_OUT.AnalogOutChannelConfig();
                cfg.FromPayload(payload);
                return cfg;
            });
        }

        public void GetAOutConfig(uint channel, Action<A_OUT.AnalogOutChannelConfig> callback) {
            _ = Task.Run(async () =>
            {
                A_OUT.AnalogOutChannelConfig cfg = await GetAOutConfigAsync(channel);
                callback?.Invoke(cfg);
            });
        }

        public Task<double> GetAOutValueAsync(uint channel) {
            SigCoreMessage msg = SigCoreMessage.CreateGetAOut(channel);
            return RequestAsync(msg, payload => payload.Value<double>("value"));
        }

        public void GetAOutValue(uint channel, Action<double> callback) {
            _ = Task.Run(async () =>
            {
                double value = await GetAOutValueAsync(channel);
                callback?.Invoke(value);
            });
        }

        // ==========================================
        // PID LOOP
        // ==========================================

        public Task<PID_LOOP.Config> GetPIDConfigAsync(uint channel) {
            SigCoreMessage msg = SigCoreMessage.CreateGetPIDConfig(channel);
            return RequestAsync(msg, payload =>
            {
                PID_LOOP.Config cfg = new PID_LOOP.Config();
                cfg.UnpackConfig(payload);
                return cfg;
            });
        }

        public void GetPIDConfig(uint channel, Action<PID_LOOP.Config> callback) {
            _ = Task.Run(async () =>
            {
                PID_LOOP.Config cfg = await GetPIDConfigAsync(channel);
                callback?.Invoke(cfg);
            });
        }

        public Task<(double setPoint, double processVariable, double output, double tolerance, bool autoMode, double deviation)>
            GetPIDCurValAsync(uint channel) {
            SigCoreMessage msg = SigCoreMessage.CreateGetPIDCurVal(channel);
            return RequestAsync(msg, payload => (
                payload.Value<double>("setPoint"),
                payload.Value<double>("processVariable"),
                payload.Value<double>("output"),
                payload.Value<double>("tolerance"),
                payload.Value<bool>("autoMode"),
                payload.Value<double>("deviation")
            ));
        }

        public void GetPIDCurVal(uint channel, Action<double, double, double, double, bool, double> callback) {
            _ = Task.Run(async () =>
            {
                var tuple = await GetPIDCurValAsync(channel);
                callback?.Invoke(tuple.setPoint, tuple.processVariable, tuple.output, tuple.tolerance, tuple.autoMode, tuple.deviation);
            });
        }

        public Task<MYSQL_LOGGER.DataLoggerConfig> GetLoggingConfigAsync() {
            SigCoreMessage msg = SigCoreMessage.CreateGetLoggingConfig();
            return RequestAsync(msg, payload => {
                MYSQL_LOGGER.DataLoggerConfig cfg = new MYSQL_LOGGER.DataLoggerConfig();
                cfg.FromPayload(payload);
                return cfg;
            });
        }

        public void SetLoggingConfig(MYSQL_LOGGER.DataLoggerConfig config) {
            JObject payload = config.ToPayload();
            SigCoreMessage msg = SigCoreMessage.CreateSetLoggingConfig(payload);
            _ = _client.SendAsync(msg);
        }
        public Task<HardwareManager.Config> GetGlobalConfigAsync() {
            SigCoreMessage msg = SigCoreMessage.CreateGetGlobalConfig();
            return RequestAsync(msg, payload =>
            {
                HardwareManager.Config cfg = new HardwareManager.Config();
                cfg.FromPayload(payload);
                return cfg;
            });
        }

        public void GetGlobalConfig(Action<HardwareManager.Config> callback) {
            _ = Task.Run(async () =>
            {
                HardwareManager.Config cfg = await GetGlobalConfigAsync();
                callback?.Invoke(cfg);
            });
        }

        public void GetFRAM(Action<JObject> callback) {
            _ = Task.Run(async () => {
                SigCoreMessage msg = SigCoreMessage.CreateGetFRAM();
                JObject obj = await RequestAsync(msg);
                callback?.Invoke(obj);
            });
        }

        public void PingPong(Action<bool> callback, int timeoutMs = 1000) {
            _ = Task.Run(async () => {
                SigCoreMessage msg = SigCoreMessage.CreatePing();
                try {
                    await RequestAsync(msg, timeoutMs);
                    callback?.Invoke(true);
                } catch (TimeoutException) {
                    callback?.Invoke(false);
                }
            });
        }
        // ===============================
        // Config Cycle Implementations
        // ===============================

        // Global Config
        public HardwareManager.Config ConfigureGlobal(Func<HardwareManager.Config, HardwareManager.Config> modifyFunc) {
            return Sync(() => ConfigureAsync(
                0,
                async _ => {
                    SigCoreMessage msg = SigCoreMessage.CreateGetGlobalConfig();
                    return await RequestAsync(msg, payload => {
                        HardwareManager.Config cfg = new HardwareManager.Config();
                        cfg.FromPayload(payload);
                        return cfg;
                    });
                },
                async (_, cfg) => {
                    JObject payload = cfg.ToPayload();
                    SigCoreMessage msg = SigCoreMessage.CreateSetGlobalConfig(payload);
                    await SendCommandAsync(msg);
                },
                modifyFunc
            ));
        }

        // Relay Config
        public RELAY_OUT.RelayConfig ConfigureRelay(uint channel, Func<RELAY_OUT.RelayConfig, RELAY_OUT.RelayConfig> modifyFunc) {
            return Sync(() => ConfigureAsync(
                channel,
                async ch => {
                    SigCoreMessage msg = SigCoreMessage.CreateGetRelayConfig(ch);
                    return await RequestAsync(msg, payload => {
                        RELAY_OUT.RelayConfig cfg = new RELAY_OUT.RelayConfig();
                        cfg.FromPayload(payload);
                        return cfg;
                    });
                },
                async (ch, cfg) => {
                    JObject payload = cfg.ToPayload(ch);
                    SigCoreMessage msg = SigCoreMessage.CreateSetRelayConfig(ch, payload);
                    await SendCommandAsync(msg);
                },
                modifyFunc
            ));
        }

        // Digital Input Config
        public D_IN.DInConfig ConfigureDIn(uint channel, Func<D_IN.DInConfig, D_IN.DInConfig> modifyFunc) {
            return Sync(() => ConfigureAsync(
                channel,
                async ch => {
                    SigCoreMessage msg = SigCoreMessage.CreateGetDInConfig(ch);
                    return await RequestAsync(msg, payload => {
                        D_IN.DInConfig cfg = new D_IN.DInConfig();
                        cfg.FromPayload(payload);
                        return cfg;
                    });
                },
                async (ch, cfg) => {
                    JObject payload = cfg.ToPayload(ch);
                    SigCoreMessage msg = SigCoreMessage.CreateSetDInConfig(ch, payload);
                    await SendCommandAsync(msg);
                },
                modifyFunc
            ));
        }

        // Analog Input Config
        public A_IN.AInConfig ConfigureAIn(uint channel, Func<A_IN.AInConfig, A_IN.AInConfig> modifyFunc) {
            return Sync(() => ConfigureAsync(
                channel,
                async ch => {
                    SigCoreMessage msg = SigCoreMessage.CreateGetAInConfig(ch);
                    return await RequestAsync(msg, payload => {
                        A_IN.AInConfig cfg = new A_IN.AInConfig();
                        cfg.FromPayload(payload);
                        return cfg;
                    });
                },
                async (ch, cfg) => {
                    JObject payload = cfg.ToPayload(ch);
                    SigCoreMessage msg = SigCoreMessage.CreateSetAInConfig(ch, payload);
                    await SendCommandAsync(msg);
                },
                modifyFunc
            ));
        }

        // Analog Output Config
        public A_OUT.AnalogOutChannelConfig ConfigureAOut(uint channel, Func<A_OUT.AnalogOutChannelConfig, A_OUT.AnalogOutChannelConfig> modifyFunc) {
            return Sync(() => ConfigureAsync(
                channel,
                async ch => {
                    SigCoreMessage msg = SigCoreMessage.CreateGetAOutConfig(ch);
                    return await RequestAsync(msg, payload => {
                        A_OUT.AnalogOutChannelConfig cfg = new A_OUT.AnalogOutChannelConfig();
                        cfg.FromPayload(payload);
                        return cfg;
                    });
                },
                async (ch, cfg) => {
                    JObject payload = cfg.ToPayload(ch);
                    SigCoreMessage msg = SigCoreMessage.CreateSetAOutConfig(ch, payload);
                    await SendCommandAsync(msg);
                },
                modifyFunc
            ));
        }

        // PID Config
        public PID_LOOP.Config ConfigurePID(uint channel, Func<PID_LOOP.Config, PID_LOOP.Config> modifyFunc) {
            return Sync(() => ConfigureAsync(
                channel,
                async ch => {
                    SigCoreMessage msg = SigCoreMessage.CreateGetPIDConfig(ch);
                    return await RequestAsync(msg, payload => {
                        PID_LOOP.Config cfg = new PID_LOOP.Config();
                        cfg.UnpackConfig(payload);
                        return cfg;
                    });
                },
                async (ch, cfg) => {
                    JObject payload = cfg.PackConfig(ch);
                    SigCoreMessage msg = SigCoreMessage.CreateSetPIDConfig(ch, payload);
                    await SendCommandAsync(msg);
                },
                modifyFunc
            ));
        }
        public async Task Shutdown() {
            SigCoreMessage msg = SigCoreMessage.CreateShutdown();
            await SendCommandAsync(msg);
        }
        public async Task Restart() {
            SigCoreMessage msg = SigCoreMessage.CreateRestart();
            await SendCommandAsync(msg);
        }

        public async Task<JObject> GetStatusAsync() {
            SigCoreMessage msg = SigCoreMessage.CreateGetStatus();
            return await RequestAsync(msg);
        }
        public async Task FactoryReset(string serialNo, string rev, string ver, string host, string sysName) {
            SigCoreMessage msg = SigCoreMessage.CreateFactoryReset(serialNo, rev, ver, host, sysName);
            await SendCommandAsync(msg);
        }


        // ===============================
        // Chunk 5 — Event Handling / Alerts
        // ===============================

        public SigCoreMessage HandleDInChangeAlert(JObject payload, ulong msgId, ISession session) {
            bool[] values = ((JArray)payload["values"]).ToObject<bool[]>();
            OnAlert(DigitalInChanged, values);
            return null;
        }

        public SigCoreMessage HandleAInChangeAlert(JObject payload, ulong msgId, ISession session) {
            double[] values = ((JArray)payload["values"]).ToObject<double[]>();
            OnAlert(AnalogInChanged, values);
            return null;
        }

        public SigCoreMessage HandleRelayChangeAlert(JObject payload, ulong msgId, ISession session) {
            uint channel = payload.Value<uint>("channel");
            bool state = payload.Value<bool>("value");
            OnAlert(RelayChanged, channel, state);
            return null;
        }

        public SigCoreMessage HandleAOutChangeAlert(JObject payload, ulong msgId, ISession session) {
            uint channel = payload.Value<uint>("channel");
            double value = payload.Value<double>("value");
            bool auto = payload.Value<bool>("auto");
            OnAlert(AnalogOutChanged, channel, value, auto);
            return null;
        }

        public SigCoreMessage HandleLoggingStatus(JObject payload, ulong msgId, ISession session) {
            string status = payload.Value<string>("status");
            bool logging = payload.Value<bool>("logging");
            OnAlert(LoggerStatusChanged, status, logging);
            return null;
        }

        public SigCoreMessage HandlePIDCurValChangeAlert(JObject payload, ulong msgId, ISession session) {
            uint channel = 0;
            JToken channelToken = payload["channel"];
            if (channelToken != null)
                channel = channelToken.Value<uint>();

            JObject values = payload["values"] as JObject;
            if (values == null)
                return null;

            foreach (JProperty property in values.Properties()) {
                string key = property.Name.ToLowerInvariant();
                JToken val = property.Value;

                switch (key) {
                    case "sp":
                        PIDSPChanged?.Invoke(channel, val.Value<double>());
                        break;

                    case "pv":
                        PIDPVChanged?.Invoke(channel, val.Value<double>());
                        break;

                    case "out":
                        PIDOutputChanged?.Invoke(channel, val.Value<double>());
                        break;

                    case "dev":
                        PIDDevChanged?.Invoke(channel, val.Value<double>());
                        break;

                    case "tol":
                        PIDTolChanged?.Invoke(channel, val.Value<double>());
                        break;

                    case "auto":
                        PIDAutoChanged?.Invoke(channel, val.Value<bool>());
                        break;

                    // === New ramp handlers ===
                    case "ramptime":
                        PIDRampTimeChanged?.Invoke(channel, val.Value<double>());
                        break;

                    case "ramptarget":
                        PIDRampTargetChanged?.Invoke(channel, val.Value<double>());
                        break;

                    case "ramp":
                        PIDRampChanged?.Invoke(channel, val.Value<bool>());
                        break;
                }
            }

            return null;
        }

        // ===============================
        // Core Control Handlers
        // ===============================

        public SigCoreMessage HandlePing(JObject payload, ulong msgId, ISession session) {
            Console.WriteLine("Pong");
            return SigCoreMessage.CreatePong(msgId);
        }

        public SigCoreMessage HandleConnect(JObject payload, ulong msgId, ISession session, string ver) {
            uint sessionID = payload.Value<uint>("sessionID");
            IsCommander = payload.Value<bool>("isCommander");
            string serverVer = payload.Value<string>("version")!;

            if (ver != serverVer)
                throw new Exception($"Version mismatch: Server={serverVer}, Client={ver}");

            Console.WriteLine($"Session established: ID={sessionID}, Commander={IsCommander}");
            _connectHandshakeTcs.TrySetResult(true);
            return null;
        }


        // ===============================
        // Response Handlers (Unified Path)
        // ===============================
        public SigCoreMessage TrySetResult(JObject payload, ulong msgId) {
            if (_pending.TryRemove(msgId, out TaskCompletionSource<JObject> tcs)) {
                tcs.TrySetResult(payload);
            }
            return null;
        }


        public SigCoreMessage HandlePong(JObject payload, ulong msgId, ISession session) => TrySetResult(payload, msgId);
        public SigCoreMessage HandleRelay(JObject payload, ulong msgId, ISession session) => TrySetResult(payload, msgId);
        public SigCoreMessage HandleAOut(JObject payload, ulong msgId, ISession session) => TrySetResult(payload, msgId);
        public SigCoreMessage HandleAnalogInValue(JObject payload, ulong msgId, ISession session) => TrySetResult(payload, msgId);
        public SigCoreMessage HandleDIn(JObject payload, ulong msgId, ISession session) => TrySetResult(payload, msgId);
        public SigCoreMessage HandleAInConfig(JObject payload, ulong msgId, ISession session) => TrySetResult(payload, msgId);
        public SigCoreMessage HandleAOutConfig(JObject payload, ulong msgId, ISession session) => TrySetResult(payload, msgId);
        public SigCoreMessage HandleGlobalConfig(JObject payload, ulong msgId, ISession session) => TrySetResult(payload, msgId);
        public SigCoreMessage HandleRelayConfig(JObject payload, ulong msgId, ISession session) => TrySetResult(payload, msgId);
        public SigCoreMessage HandleDInConfig(JObject payload, ulong msgId, ISession session) => TrySetResult(payload, msgId);
        public SigCoreMessage HandlePIDCurVal(JObject payload, ulong msgId, ISession session) => TrySetResult(payload, msgId);
        public SigCoreMessage HandlePIDConfig(JObject payload, ulong msgId, ISession session) => TrySetResult(payload, msgId);
        public SigCoreMessage HandleFRAM(JObject payload, ulong msgId, ISession session) => TrySetResult(payload, msgId);
        public SigCoreMessage HandleError(JObject payload, ulong msgId, ISession session) => TrySetResult(payload, msgId);
        public SigCoreMessage HandleLoggingConfig(JObject payload, ulong msgId, ISession session) => TrySetResult(payload, msgId);
        public SigCoreMessage HandleStatus(JObject payload, ulong msgId, ISession session) => TrySetResult(payload, msgId);

        //
        //
        //

        public SigCoreMessage HandleSetRelay(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleGetAnalogIn(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleGetAInConfig(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleGetDIn(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleGetAOut(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleSetAOut(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandlePWM(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleSetAInConfig(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleGetGlobalConfig(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleSetGlobalConfig(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleGetRelayConfig(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleSetRelayConfig(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleGetDInConfig(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleSetDInConfig(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleGetAOutConfig(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleSetAOutConfig(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleGetPIDConfig(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleSetPIDConfig(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleGetPIDCurVal(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleSetPIDCurVal(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleGetRelay(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleSubscribe(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleGetFRAM(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleResetPID(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleGetLoggingStatus(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleGetLoggingConfig(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleSetLoggingConfig(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleRestart(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleShutdown(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleGetStatus(object payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleFactoryReset(object payload, ulong msgId, ISession session) => null;


        public Task NotifyDInChanged(bool[] vals) => Task.CompletedTask;
        public Task NotifyAInChanged(double[] vals) => Task.CompletedTask;
        public Task NotifyRelayChange(uint channel, bool val) => Task.CompletedTask;
        public Task NotifyAOutChanged(uint channel, double val, bool auto) => Task.CompletedTask;
        public Task NotifyPIDChanged(uint channel, JObject vals) => Task.CompletedTask;
        public Task NotifyLoggerChanged(string status, bool logging) => Task.CompletedTask;

    }
}
