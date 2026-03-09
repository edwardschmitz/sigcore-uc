using Newtonsoft.Json.Linq;
using SigCoreCommon;
using SigCoreCommon.Payloads;
using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Tmds.Linux;

namespace SigCoreServer {
    public class CommandDispatcher : IDispatcher {
        HardwareManager hardwareManager;
        OsManager osManager;

        private readonly ConcurrentDictionary<ISession, bool> _subscribers = new ConcurrentDictionary<ISession, bool>();

        public CommandDispatcher(HardwareManager hwMan, OsManager osMan) {
            hardwareManager = hwMan;
            osManager = osMan;
        }





        public SigCoreMessage HandleAck(JObject payload, ulong msgId, ISession session) {
            return null;
        }

        public SigCoreMessage HandleConnect(JObject payload, ulong msgId, ISession session) {
            int version = payload.Value<int>("version");
            return SigCoreMessage.CreateConnectResult(msgId, version);
        }

        public SigCoreMessage HandleError(JObject payload, ulong msgId, ISession session) {
            Console.WriteLine("Error Message Received");
            Console.WriteLine(payload.ToString());
            return null;
        }

        public SigCoreMessage HandlePing(JObject payload, ulong msgId, ISession session) {
            Console.WriteLine("Ping");
            return SigCoreMessage.CreatePong(msgId);
        }

        public SigCoreMessage HandleSetRelay(JObject payload, ulong msgId, ISession session) {

            uint channel = payload.Value<uint>("channel");
            bool state = payload.Value<bool>("state");

            hardwareManager.SetRelay(channel, state);
            return null;
        }

        public SigCoreMessage HandlePong(JObject payload, ulong msgId, ISession session) {
            return null;
        }

        public SigCoreMessage HandleGetAnalogIn(JObject payload, ulong msgId, ISession session) {
            uint channel = payload.Value<uint>("channel");
            double val = hardwareManager.GetAnalogIn(channel);
            SigCoreMessage msg = SigCoreMessage.CreateAnalogInValue(msgId, channel, val);
            return msg;
        }


        public SigCoreMessage HandleGetAInConfig(JObject payload, ulong msgID, ISession session) {
            uint channel = payload.Value<uint>("channel");
            AInConfigPayload configPayload = hardwareManager.GetAnalogInConfig(channel);

            return SigCoreMessage.CreateAInConfig(msgID, configPayload);
        }

        public SigCoreMessage HandleGetDIn(JObject payload, ulong msgID, ISession session) {
            uint channel = payload.Value<uint>("channel");
            bool state = hardwareManager.GetDIn(channel);

            return SigCoreMessage.CreateDIn(msgID, channel, state);
        }


        public SigCoreMessage HandleGetAOut(JObject payload, ulong msgID, ISession session) {
            uint channel = payload.Value<uint>("channel");
            (double val, bool auto)  val = hardwareManager.GetAOutValue(channel);

            return SigCoreMessage.CreateAOut(msgID, channel, val.val, val.auto);
        }

        public SigCoreMessage HandleSetAOut(JObject payload, ulong msgID, ISession session) {
            uint channel = payload.Value<uint>("channel");
            double val = payload.Value<double>("value");

            hardwareManager.SetAOutValue(channel, val);
            return null;
        }
        public SigCoreMessage HandleGetAOutConfig(JObject payload, ulong msgID, ISession session) {
            uint channel = payload.Value<uint>("channel");
            AOutConfigPayload config = hardwareManager.GetAOutConfig(channel);
            return SigCoreMessage.CreateAOutConfig(msgID, config.ToJson());
        }

        public SigCoreMessage HandleSetAOutConfig(JObject payload, ulong msgID, ISession session) {
            AOutConfigPayload config = new AOutConfigPayload(payload);
            hardwareManager.SetAOutConfig(config);
            return null;
        }

        public SigCoreMessage HandleSetAInConfig(JObject payload, ulong msgId, ISession session) {
            AInConfigPayload Payload = new AInConfigPayload(payload);
            hardwareManager.SetAInConfig(Payload); 
            return null;
        }

        public SigCoreMessage HandleGetGlobalConfig(JObject payload, ulong msgId, ISession session) {
            SystemConfigPayload p = hardwareManager.GetGlobalConfig();
            SigCoreMessage msg = SigCoreMessage.CreateGlobalConfig(msgId, p.ToJson());
            return msg;
        }

        public SigCoreMessage HandleSetGlobalConfig(JObject payload, ulong msgId, ISession session) {
            SystemConfigPayload config = new SystemConfigPayload(payload);

            hardwareManager.ApplyGlobalConfig(config);
            return null;
        }


        public SigCoreMessage HandleSetNetworkConfig(JObject payload, ulong msgId, ISession session) {
            NetworkSettingsPayload p = new NetworkSettingsPayload(payload);

                // Persist the network settings
            hardwareManager.SetNetworkConfig(p);

                // Apply the settings to the OS
            osManager.ApplyNetworkConfig(p);
            return null;
        }
        public SigCoreMessage HandleGetNetworkConfig(JObject payload, ulong msgId, ISession session) {
            NetworkSettingsPayload settings = hardwareManager.GetNetworkConfig();
            SigCoreMessage msg = SigCoreMessage.CreateNetworkConfig(msgId, settings.ToJson());
            return msg;
        }


        public SigCoreMessage HandleGetRelayConfig(JObject payload, ulong msgId, ISession session) {
            uint channel = payload.Value<uint>("channel");
            RelayConfigPayload configPayload = hardwareManager.GetRelayConfig(channel);
            return SigCoreMessage.CreateRelayConfig(msgId, configPayload);

        }
        public SigCoreMessage HandleSetRelayConfig(JObject payload, ulong msgId, ISession session) {
            RelayConfigPayload config = new RelayConfigPayload(payload);
            hardwareManager.SetRelayConfig(config); // how does this become null???
            return null;
        }

        public SigCoreMessage HandleGetDInConfig(JObject payload, ulong msgId, ISession session) {
            uint channel = payload.Value<uint>("channel");
            DInConfigPayload configPayload = hardwareManager.GetDInConfig(channel);
            return SigCoreMessage.CreateDInConfig(msgId, configPayload);
        }

        public SigCoreMessage HandleSetDInConfig(JObject payload, ulong msgId, ISession session) {
            DInConfigPayload dInConfigPayload = new DInConfigPayload(payload);
            hardwareManager.SetDInConfig(dInConfigPayload); // how does this become null???
            return null;
        }


        public SigCoreMessage HandleGetRelay(JObject payload, ulong msgId, ISession session) {
            uint channel = payload.Value<uint>("channel");
            bool val = hardwareManager.GetRelayOutput(channel);
            return SigCoreMessage.CreateRelay(msgId, channel, val);
        }

        public SigCoreMessage HandleGetPIDConfig(JObject payload, ulong msgId, ISession session) {
            uint channel = payload.Value<uint>("channel");
            JObject configPayload = hardwareManager.GetPIDConfig(channel);
            return SigCoreMessage.CreatePIDConfig(msgId, channel, configPayload);
        }

        public SigCoreMessage HandleSetPIDConfig(JObject payload, ulong msgId, ISession session) {
            hardwareManager.SetPIDConfig(payload);
            return null;
        }

        public SigCoreMessage HandleGetPIDCurVal(JObject payload, ulong msgId, ISession session) {
            uint channel = payload.Value<uint>("channel");
            JObject curValPayload = hardwareManager.GetPIDCurVal(channel);
            return SigCoreMessage.CreatePIDCurVal(msgId, channel, payload);
        }

        public SigCoreMessage HandleSetPIDCurVal(JObject payload, ulong msgId, ISession session) {
            hardwareManager.SetPIDCurVal(payload);
            return null;
        }


        public SigCoreMessage HandleSubscribe(JObject payload, ulong msgId, ISession session) {
            _subscribers.TryAdd(session, true);
            return null; 
        }
        public SigCoreMessage HandleGetFRAM(JObject payload, ulong msgId, ISession session) {
            JObject fram = hardwareManager.ReadConfigFromFram();
            return SigCoreMessage.CreateFRAM(msgId, fram);
        }
        public SigCoreMessage HandleGetLoggingConfig(JObject payload, ulong msgId, ISession session) {
            JObject config = hardwareManager.GetLoggerConfig();
            return SigCoreMessage.CreateLoggimgConfig(msgId, config);
        }

        public SigCoreMessage HandleSetLoggingConfig(JObject payload, ulong msgId, ISession session) {
            hardwareManager.SetLoggerConfig(payload);
            return null;
        }
        public SigCoreMessage HandleGetLoggingStatus(JObject payload, ulong msgId, ISession session) {
            string status = hardwareManager.GetLoggerStatus();
            bool logging = hardwareManager.IsLoggingEnabled();
            return SigCoreMessage.CreateLoggingStatus(status, logging);
        }
        public SigCoreMessage HandleRestart(JObject payload, ulong msgId, ISession session) {
            osManager.RestartDevice();
            return null;
        }

        public SigCoreMessage HandleShutdown(JObject payload, ulong msgId, ISession session) {
            osManager.ShutdownDevice();
            return null;
        }
        public SigCoreMessage HandleGetStatus(JObject payload, ulong msgId, ISession session) {
            JObject status = new JObject();

            status["sections"] = new JArray();

            hardwareManager.GetSystemStatus(status);
            osManager.GetSystemStatus(status);
            hardwareManager.GetWiFiStatus(status);
            return SigCoreMessage.CreateStatus(status, msgId);
        }


        public SigCoreMessage HandleStatus(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleAnalogInValue(JObject payload, ulong msgID, ISession session) => null;
        public SigCoreMessage HandleAInConfig(JObject payload, ulong msgID, ISession session) => null;
        public SigCoreMessage HandleDIn(JObject payload, ulong msgID, ISession session) => null;
        public SigCoreMessage HandleAOut(JObject payload, ulong msgID, ISession session) => null;
        public SigCoreMessage HandleAOutConfig(JObject payload, ulong msgID, ISession session) => null;
        public SigCoreMessage HandleGlobalConfig(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleRelayConfig(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleDInConfig(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleRelay(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandlePIDConfig(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandlePIDCurVal(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleLoggingStatus(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleDInChangeAlert(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleAInChangeAlert(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleRelayChangeAlert(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleAOutChangeAlert(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandlePIDCurValChangeAlert(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleFRAM(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleLoggingConfig(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleNetworkConfig(JObject payload, ulong msgId, ISession session) => null;
        public SigCoreMessage HandleConnectResult(JObject payload, ulong msgId, ISession session) => null;


        public async Task NotifyDInChanged(bool[] vals) {
            SigCoreMessage msg = SigCoreMessage.CreateDInChangeAlert(vals);
            await Broadcast(msg);
        }

        public async Task NotifyAInChanged(double[] vals) {
            SigCoreMessage msg = SigCoreMessage.CreateAInChangeAlert(vals);
            await Broadcast(msg);
        }

        public async Task NotifyRelayChange(uint channel, bool val) {
            SigCoreMessage msg = SigCoreMessage.CreateRelayChangeAlert(channel, val);
            await Broadcast(msg);
        }

        public async Task NotifyAOutChanged(uint channel, double val, bool auto) {
            SigCoreMessage msg = SigCoreMessage.CreateAOutChangeAlert(channel, val, auto);
            await Broadcast(msg);
        }

        public async Task NotifyPIDChanged(uint channel, JObject vals) {
            SigCoreMessage msg = SigCoreMessage.CreatePIDCurValChangeAlert(channel, vals);
            await Broadcast(msg);
        }
        public async Task NotifyLoggerChanged(string status, bool logging) {
            SigCoreMessage msg = SigCoreMessage.CreateLoggingStatus(status, logging);
            await Broadcast(msg);
        }

        public SigCoreMessage HandleResetPID(JObject payload, ulong msgId, ISession session) {
            uint channel = payload.Value<uint>("channel");
            hardwareManager.ResetPID(channel);
            return null;
        }

        public async Task Broadcast(SigCoreMessage msg) {
            foreach (var kvp in _subscribers) {
                ISession session = kvp.Key;
                if (session.IsConnected)
                    await session.SendOrDiscard(msg);
            }
        }

        public SigCoreMessage HandleFactoryReset(JObject payloadObj, ulong msgId, ISession session) {
            JObject payload = payloadObj as JObject;
            if (payload == null)
                return null;

            string serial = (string)payload["serial"];
            string systemName = (string)payload["systemName"];
            string revision = (string)payload["revision"];
            string version = (string)payload["version"];
            string hostname = (string)payload["hostname"];
            string manifestURL = (string)payload["manifest_url"];
            string manifestSigUrl = (string)payload["manifest_sig_url"];
            string URL = (string)payload["url"];
            string sigUrl = (string)payload["signature"];

            LogFile log = new LogFile();
            log.Info("HandleFactoryReset");
            hardwareManager.FactoryReset(serial, systemName, version, revision, manifestURL, manifestSigUrl, URL, sigUrl, hostname);
            osManager.FactoryReset(hostname);

            return null;
        }

    }
}
