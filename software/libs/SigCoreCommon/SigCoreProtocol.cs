using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace SigCoreCommon {


    public class SigCoreMessage {

        // Adding a new message: 
        //     Add enum value. 
        //     Add handler to IDispatcher and its implementation(s). 
        //     Add switch case in Dispatch.
        //     Add factory method(s). 
        //     Define payload schema(if applicable). 
        //     Add Handlers to client and server
        //     Add Routines to SigCoreSystem class

        public enum MsgType {
            None = 0,
            Connect = 1,
            Ping = 2,
            Pong = 3,
            SetRelay = 4,
            GetRelay = 5,
            Relay = 6,
            Status = 7,
            Error = 8,
            GetAnalogIn = 9,
            AnalogInValue = 10,
            AInConfig = 11,
            GetAInConfig = 12,
            SetAInConfig = 13,
            GetDIn = 14,
            DIn = 15,
            GetAOut = 16,
            SetAOut = 17,
            AOut = 18,
            GetAOutConfig = 19,
            SetAOutConfig = 20,
            AOutConfig = 21,
            GlobalConfig = 22,
            GetGlobalConfig = 23,
            SetGlobalConfig = 24,
            GetRelayConfig = 25,
            SetRelayConfig = 26,
            RelayConfig = 27,
            GetDInConfig = 28,
            SetDInConfig = 29,
            DInConfig = 30,
            SetPIDCurVal = 31,
            GetPIDCurVal = 32,
            PIDCurVal = 33,
            SetPIDConfig = 34,
            GetPIDConfig = 35,
            PIDConfig = 36,
            Subscribe = 37,
            DInChangeAlert = 38,
            AInChangeAlert = 39,
            RelayChangeAlert = 40,
            AOutChangeAlert = 41,
            PIDCurValChangeAlert = 42,
            GetFRAM = 43,
            FRAM = 44,
            ResetPID = 45,
            GetLoggingConfig = 46,
            SetLoggingConfig = 47,
            LoggingConfig = 50,
            GetLoggingStatus = 51,
            LoggingStatus = 52,
            Restart = 53,
            Shutdown = 54,
            GetStatus = 55,
            FactoryReset = 56,
        }

        private const string Version = "2";

        public ulong MsgId { get; set; }
        public MsgType Command { get; set; } = MsgType.None;
        public JObject Payload { get; set; } = new JObject();
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;

        [JsonConstructor]
        public SigCoreMessage() { }

        public SigCoreMessage? Dispatch(IDispatcher dispatcher, ISession session) {
            switch (Command) {
                case MsgType.Ping: return dispatcher.HandlePing(Payload, MsgId, session);
                case MsgType.Pong: return dispatcher.HandlePong(Payload, MsgId, session);
                case MsgType.SetRelay: return dispatcher.HandleSetRelay(Payload, MsgId, session);
                case MsgType.GetRelay: return dispatcher.HandleGetRelay(Payload, MsgId, session);
                case MsgType.Relay: return dispatcher.HandleRelay(Payload, MsgId, session);
                case MsgType.Status: return dispatcher.HandleStatus(Payload, MsgId, session);
                case MsgType.Error: return dispatcher.HandleError(Payload, MsgId, session);
                case MsgType.Connect: return dispatcher.HandleConnect(Payload, MsgId, session, Version);
                case MsgType.GetAnalogIn: return dispatcher.HandleGetAnalogIn(Payload, MsgId, session);
                case MsgType.AnalogInValue: return dispatcher.HandleAnalogInValue(Payload, MsgId, session);
                case MsgType.AInConfig: return dispatcher.HandleAInConfig(Payload, MsgId, session);
                case MsgType.GetAInConfig: return dispatcher.HandleGetAInConfig(Payload, MsgId, session);
                case MsgType.SetAInConfig: return dispatcher.HandleSetAInConfig(Payload, MsgId, session);
                case MsgType.GetDIn: return dispatcher.HandleGetDIn(Payload, MsgId, session);
                case MsgType.DIn: return dispatcher.HandleDIn(Payload, MsgId, session);
                case MsgType.GetAOut: return dispatcher.HandleGetAOut(Payload, MsgId, session);
                case MsgType.SetAOut: return dispatcher.HandleSetAOut(Payload, MsgId, session);
                case MsgType.AOut: return dispatcher.HandleAOut(Payload, MsgId, session);
                case MsgType.GetAOutConfig: return dispatcher.HandleGetAOutConfig(Payload, MsgId, session);
                case MsgType.SetAOutConfig: return dispatcher.HandleSetAOutConfig(Payload, MsgId, session);
                case MsgType.AOutConfig: return dispatcher.HandleAOutConfig(Payload, MsgId, session);
                case MsgType.GetGlobalConfig: return dispatcher.HandleGetGlobalConfig(Payload, MsgId, session);
                case MsgType.SetGlobalConfig: return dispatcher.HandleSetGlobalConfig(Payload, MsgId, session);
                case MsgType.GlobalConfig: return dispatcher.HandleGlobalConfig(Payload, MsgId, session);
                case MsgType.GetRelayConfig: return dispatcher.HandleGetRelayConfig(Payload, MsgId, session);
                case MsgType.SetRelayConfig: return dispatcher.HandleSetRelayConfig(Payload, MsgId, session);
                case MsgType.RelayConfig: return dispatcher.HandleRelayConfig(Payload, MsgId, session);
                case MsgType.GetDInConfig: return dispatcher.HandleGetDInConfig(Payload, MsgId, session);
                case MsgType.SetDInConfig: return dispatcher.HandleSetDInConfig(Payload, MsgId, session);
                case MsgType.DInConfig: return dispatcher.HandleDInConfig(Payload, MsgId, session);
                case MsgType.GetPIDConfig: return dispatcher.HandleGetPIDConfig(Payload, MsgId, session);
                case MsgType.SetPIDConfig: return dispatcher.HandleSetPIDConfig(Payload, MsgId, session);
                case MsgType.PIDConfig: return dispatcher.HandlePIDConfig(Payload, MsgId, session);
                case MsgType.GetPIDCurVal: return dispatcher.HandleGetPIDCurVal(Payload, MsgId, session);
                case MsgType.SetPIDCurVal: return dispatcher.HandleSetPIDCurVal(Payload, MsgId, session);
                case MsgType.PIDCurVal: return dispatcher.HandlePIDCurVal(Payload, MsgId, session);
                case MsgType.Subscribe: return dispatcher.HandleSubscribe(Payload, MsgId, session);
                case MsgType.DInChangeAlert: return dispatcher.HandleDInChangeAlert(Payload, MsgId, session);
                case MsgType.AInChangeAlert: return dispatcher.HandleAInChangeAlert(Payload, MsgId, session);
                case MsgType.RelayChangeAlert: return dispatcher.HandleRelayChangeAlert(Payload, MsgId, session);
                case MsgType.AOutChangeAlert: return dispatcher.HandleAOutChangeAlert(Payload, MsgId, session);
                case MsgType.PIDCurValChangeAlert: return dispatcher.HandlePIDCurValChangeAlert(Payload, MsgId, session);
                case MsgType.GetFRAM: return dispatcher.HandleGetFRAM(Payload, MsgId, session);
                case MsgType.FRAM: return dispatcher.HandleFRAM(Payload, MsgId, session);
                case MsgType.ResetPID: return dispatcher.HandleResetPID(Payload, MsgId, session);
                case MsgType.GetLoggingConfig: return dispatcher.HandleGetLoggingConfig(Payload, MsgId, session);
                case MsgType.SetLoggingConfig: return dispatcher.HandleSetLoggingConfig(Payload, MsgId, session);
                case MsgType.LoggingConfig: return dispatcher.HandleLoggingConfig(Payload, MsgId, session);
                case MsgType.GetLoggingStatus: return dispatcher.HandleGetLoggingStatus(Payload, MsgId, session);
                case MsgType.LoggingStatus: return dispatcher.HandleLoggingStatus(Payload, MsgId, session);
                case MsgType.Restart: return dispatcher.HandleRestart(Payload, MsgId, session);
                case MsgType.Shutdown: return dispatcher.HandleShutdown(Payload, MsgId, session);
                case MsgType.GetStatus: return dispatcher.HandleGetStatus(Payload, MsgId, session);
                case MsgType.FactoryReset: return dispatcher.HandleFactoryReset(Payload, MsgId, session);
                default: return null;
            }
        }

        public override string ToString() {
            return JsonConvert.SerializeObject(this, Formatting.None);
        }

        public static SigCoreMessage FromJson(string json) {
            SigCoreMessage? msg = JsonConvert.DeserializeObject<SigCoreMessage>(json);
            if (msg == null) throw new InvalidOperationException("Failed to deserialize SigCoreMessage from JSON.");
            return msg;
        }

        // ---------------- Factory Methods ----------------

        private static SigCoreMessage CreateBase(MsgType command, JObject payload = null) {
            if (payload == null) payload = new JObject();
            return new SigCoreMessage { Command = command, Payload = payload, MsgId = MessageIdGenerator.NextID };
        }

        private static SigCoreMessage CreateBase(ulong msgID, MsgType command, JObject payload = null) {
            if (payload == null) payload = new JObject();
            return new SigCoreMessage { 
                Command = command, 
                Payload = payload, 
                MsgId = msgID,
            };
        }

        public static SigCoreMessage CreateConnect(uint sessionID, bool isCommander) {
            Console.WriteLine($"SessionID: {sessionID}, IsCommander: {isCommander}, Ver: {Version.ToString()}");
            JObject payload = new JObject { 
                ["sessionID"] = sessionID, 
                ["isCommander"] = isCommander,
                ["version"] = Version.ToString(),
            };
            return CreateBase(MsgType.Connect, payload);
        }

        public static SigCoreMessage CreatePing() => CreateBase(MsgType.Ping);

        public static SigCoreMessage CreatePong(ulong msgId) => new SigCoreMessage { Command = MsgType.Pong, Payload = new JObject(), MsgId = msgId };

        public static SigCoreMessage CreateSetRelay(uint channel, bool state) {
            JObject payload = new JObject { ["channel"] = channel, ["state"] = state };
            return CreateBase(MsgType.SetRelay, payload);
        }

        public static SigCoreMessage CreateGetRelay(uint channel) {
            JObject payload = new JObject { ["channel"] = channel };
            return CreateBase(MsgType.GetRelay, payload);
        }

        public static SigCoreMessage CreateRelay(ulong msgId, uint channel, bool state) {
            JObject payload = new JObject { ["channel"] = channel, ["state"] = state };
            return new SigCoreMessage {
                Command = MsgType.Relay,
                MsgId = msgId,
                Payload = payload
            };
        }

        // ----------- Request/Response Pairs -----------

        public static SigCoreMessage CreateGetAnalogIn(uint chan) {
            JObject payload = new JObject { ["channel"] = chan };
            return CreateBase(MsgType.GetAnalogIn, payload);
        }
        public static SigCoreMessage CreateAnalogInValue(ulong msgId, uint chan, double val) {
            JObject payload = new JObject { ["channel"] = chan, ["value"] = val };
            return new SigCoreMessage { Command = MsgType.AnalogInValue, Payload = payload, MsgId = msgId };
        }

        public static SigCoreMessage CreateGetAInConfig(uint chan) {
            JObject payload = new JObject { ["channel"] = chan };
            return CreateBase(MsgType.GetAInConfig, payload);
        }
        public static SigCoreMessage CreateAInConfig(ulong msgId, JObject payload) {
            return new SigCoreMessage { Command = MsgType.AInConfig, Payload = payload, MsgId = msgId };
        }

        public static SigCoreMessage CreateGetAOut(uint chan) {
            JObject payload = new JObject { ["channel"] = chan };
            return CreateBase(MsgType.GetAOut, payload);
        }
        public static SigCoreMessage CreateAOut(ulong msgId, uint chan, double val, bool auto) {
            JObject payload = new JObject { ["channel"] = chan, ["value"] = val, ["auto"] = auto };
            return new SigCoreMessage { Command = MsgType.AOut, Payload = payload, MsgId = msgId };
        }

        public static SigCoreMessage CreateGetAOutConfig(uint chan) {
            JObject payload = new JObject { ["channel"] = chan };
            return CreateBase(MsgType.GetAOutConfig, payload);
        }
        public static SigCoreMessage CreateAOutConfig(ulong msgId, JObject payload) {
            return new SigCoreMessage { Command = MsgType.AOutConfig, Payload = payload, MsgId = msgId };
        }

        public static SigCoreMessage CreateGetRelayConfig(uint chan) {
            JObject payload = new JObject { ["channel"] = chan };
            return CreateBase(MsgType.GetRelayConfig, payload);
        }
        public static SigCoreMessage CreateRelayConfig(ulong msgId, JObject payload) {
            return new SigCoreMessage { Command = MsgType.RelayConfig, Payload = payload, MsgId = msgId };
        }

        public static SigCoreMessage CreateGetGlobalConfig() => CreateBase(MsgType.GetGlobalConfig);
        public static SigCoreMessage CreateGlobalConfig(ulong msgId, JObject payload) {
            return new SigCoreMessage { Command = MsgType.GlobalConfig, Payload = payload, MsgId = msgId };
        }

        public static SigCoreMessage CreateGetDIn(uint chan) {
            JObject payload = new JObject { ["channel"] = chan };
            return CreateBase(MsgType.GetDIn, payload);
        }
        public static SigCoreMessage CreateDIn(ulong msgId, uint chan, bool state) {
            JObject payload = new JObject { ["channel"] = chan, ["state"] = state };
            return new SigCoreMessage { Command = MsgType.DIn, Payload = payload, MsgId = msgId };
        }

        public static SigCoreMessage CreateGetDInConfig(uint chan) {
            JObject payload = new JObject { ["channel"] = chan };
            return CreateBase(MsgType.GetDInConfig, payload);
        }
        public static SigCoreMessage CreateDInConfig(ulong msgId, JObject payload) {
            return new SigCoreMessage { Command = MsgType.DInConfig, Payload = payload, MsgId = msgId };
        }


        // =======================
        // PID CONFIG MESSAGES
        // =======================

        public static SigCoreMessage CreateGetPIDConfig(uint channel) {
            JObject payload = new JObject { ["channel"] = channel };
            return CreateBase(MsgType.GetPIDConfig, payload);
        }

        public static SigCoreMessage CreatePIDConfig(ulong msgId, uint channel, JObject payload) {
            payload["channel"] = channel;
            return CreateBase(msgId, MsgType.PIDConfig, payload);
        }

        public static SigCoreMessage CreateSetPIDConfig(uint channel, JObject payload) {
            payload["channel"] = channel;
            return CreateBase(MsgType.SetPIDConfig, payload);
        }


        // =======================
        // PID CURRENT VALUE MESSAGES (PIDCurVal)
        // =======================

        public static SigCoreMessage CreateGetPIDCurVal(uint channel) {
            JObject payload = new JObject { ["channel"] = channel };
            return CreateBase(MsgType.GetPIDCurVal, payload);
        }

        public static SigCoreMessage CreatePIDCurVal(ulong msgId, uint channel, JObject payload) {
            return CreateBase(msgId, MsgType.PIDCurVal, payload);
        }

        public static SigCoreMessage CreateSetPIDSP(uint channel, double sp) {
            JObject vals = new JObject() {
                ["sp"] = sp,
            };
            JObject payload = new JObject() {
                ["channel"] = channel,
                ["values"] = vals,
            };

            return CreateBase(MsgType.SetPIDCurVal, payload);
        }
        public static SigCoreMessage CreateSetPIDOutput(uint channel, double output) {
            JObject vals = new JObject() {
                ["out"] = output,
            };
            JObject payload = new JObject() {
                ["channel"] = channel,
                ["values"] = vals,
            };

            return CreateBase(MsgType.SetPIDCurVal, payload);
        }

        public static SigCoreMessage CreateSetPIDTol(uint channel, double val) {
            JObject vals = new JObject() {
                ["tol"] = val,
            };
            JObject payload = new JObject() {
                ["channel"] = channel,
                ["values"] = vals,
            };

            return CreateBase(MsgType.SetPIDCurVal, payload);
        }
        public static SigCoreMessage CreateSetPIDAuto(uint channel, bool val) {
            JObject vals = new JObject() {
                ["auto"] = val,
            };
            JObject payload = new JObject() {
                ["channel"] = channel,
                ["values"] = vals,
            };

            return CreateBase(MsgType.SetPIDCurVal, payload);
        }

        internal static SigCoreMessage CreateSetPIDRampTime(uint channel, double rampTimeSec) {
            JObject vals = new JObject() {
                ["rampTime"] = rampTimeSec,
            };
            JObject payload = new JObject() {
                ["channel"] = channel,
                ["values"] = vals,
            };

            return CreateBase(MsgType.SetPIDCurVal, payload);
        }

        internal static SigCoreMessage CreateSetPIDRampTarget(uint channel, double rampTarget) {
            JObject vals = new JObject() {
                ["rampTarget"] = rampTarget,
            };
            JObject payload = new JObject() {
                ["channel"] = channel,
                ["values"] = vals,
            };

            return CreateBase(MsgType.SetPIDCurVal, payload);
        }

        internal static SigCoreMessage CreateSetPIDRamp(uint channel, bool ramp) {
            JObject vals = new JObject() {
                ["ramp"] = ramp,
            };
            JObject payload = new JObject() {
                ["channel"] = channel,
                ["values"] = vals,
            };

            return CreateBase(MsgType.SetPIDCurVal, payload);
        }
        // ----------- Standalone Commands -----------

        public static SigCoreMessage CreateSetAOut(uint chan, double val) {
            JObject payload = new JObject { ["channel"] = chan, ["value"] = val };
            return CreateBase(MsgType.SetAOut, payload);
        }
        public static SigCoreMessage CreateSetAInConfig(uint channel, JObject payload) {
            payload["channel"] = channel;
            return CreateBase(MsgType.SetAInConfig, payload);
        }

        public static SigCoreMessage CreateSetAOutConfig(uint channel, JObject payload) {
            payload["channel"] = channel;
            return CreateBase(MsgType.SetAOutConfig, payload);
        }

        public static SigCoreMessage CreateSetRelayConfig(uint channel, JObject payload) {
            payload["channel"] = channel;
            return CreateBase(MsgType.SetRelayConfig, payload);
        }

        public static SigCoreMessage CreateSetGlobalConfig(JObject payload) {
            // Global config has no channel field
            return CreateBase(MsgType.SetGlobalConfig, payload);
        }

        public static SigCoreMessage CreateSetDInConfig(uint channel, JObject payload) {
            payload["channel"] = channel;
            return CreateBase(MsgType.SetDInConfig, payload);
        }
        public static SigCoreMessage CreateGetFRAM() {
            return CreateBase(MsgType.GetFRAM);
        }
        public static SigCoreMessage CreateFRAM(ulong id, JObject payload) {
            return CreateBase(id, MsgType.FRAM, payload);
        }

        public static SigCoreMessage CreateGetStatus() {
            return CreateBase(MsgType.GetStatus);
        }
        public static SigCoreMessage CreateStatus(JObject payload, ulong msgId) {
            return CreateBase(msgId, MsgType.Status, payload);
        }


        public static SigCoreMessage CreateFactoryReset(string serialNo, string rev, string ver, string host, string systemName) {
            JObject payload = new JObject {
                ["serial"] = serialNo,
                ["revision"] = rev,
                ["version"] = ver,
                ["hostname"] = host,
                ["systemName"] = systemName
            };

            return CreateBase(MsgType.FactoryReset, payload);
        }

        // ----------- Errors and Status -----------

        public static SigCoreMessage CreateError(MsgType forCommand, string errorMessage, ulong msgId) {
            return new SigCoreMessage {
                Command = MsgType.Error,
                Payload = new JObject(),
                MsgId = msgId,
                Status = "error",
                Message = $"Cmd:{forCommand} {errorMessage}"
            };
        }

        public static SigCoreMessage CreateSubscribe() {
            return CreateBase(MsgType.Subscribe);
        }
        public static SigCoreMessage CreateDInChangeAlert(bool[] vals) {
            JArray dinArray = new JArray();
            foreach (bool v in vals) {
                dinArray.Add(v ? 1 : 0); // Send as 1/0 integers
            }

            JObject payload = new JObject {
                ["values"] = dinArray
            };

            return CreateBase(MsgType.DInChangeAlert, payload);
        }
        public static SigCoreMessage CreateAInChangeAlert(double[] vals) {
            JObject payload = new JObject {
                ["values"] = JArray.FromObject(vals)
            };
            return CreateBase(MsgType.AInChangeAlert, payload);
        }
        public static SigCoreMessage CreateRelayChangeAlert(uint channel, bool val) {
            JObject payload = new JObject {
                ["channel"] = channel,
                ["value"] = val
            };
            return CreateBase(MsgType.RelayChangeAlert, payload);
        }
        public static SigCoreMessage CreateAOutChangeAlert(uint channel, double val, bool auto) {
            JObject payload = new JObject {
                ["channel"] = channel,
                ["value"] = val,
                ["auto"] = auto,
            };
            return CreateBase(MsgType.AOutChangeAlert, payload);
        }
        public static SigCoreMessage CreatePIDCurValChangeAlert(uint channel, JObject payload) {
            payload["channel"] = channel;          
            return CreateBase(MsgType.PIDCurValChangeAlert, payload);
        }
        public static SigCoreMessage CreateResetPID(uint channel) {
            JObject payload = new JObject() {
                ["channel"] = channel,
            };
            return CreateBase(MsgType.ResetPID, payload);
        }

        public static SigCoreMessage CreateGetLoggingConfig() {
            return CreateBase(MsgType.GetLoggingConfig);
        }
        public static SigCoreMessage CreateSetLoggingConfig(JObject payload) {
            return CreateBase(MsgType.SetLoggingConfig, payload);
        }
        public static SigCoreMessage CreateLoggimgConfig(ulong msgId, JObject payload) {
            return CreateBase(msgId, MsgType.LoggingConfig, payload);
        }
        public static SigCoreMessage CreateGetLoggingStatus() {
            return CreateBase(MsgType.GetLoggingStatus);
        }
        public static SigCoreMessage CreateLoggingStatus(string status, bool logging) {
            JObject payload = new JObject() {
                ["status"] = status,
                ["logging"] = logging,
            };
            return CreateBase(MsgType.LoggingStatus, payload);
        }
        public static SigCoreMessage CreateRestart() {
            return CreateBase(MsgType.Restart);
        }
        public static SigCoreMessage CreateShutdown() {
            return CreateBase(MsgType.Shutdown);
        }
    }

    internal static class MessageIdGenerator {
        private static ulong _sessionId;
        private static ulong _counter;
        private const ulong Multiplier = 100_000_000_000;

        public static ulong NextID => (_sessionId * Multiplier) + (ulong)Interlocked.Increment(ref Unsafe.As<ulong, long>(ref _counter));

        public static ulong SessionID {
            get { return _sessionId; }
            set {
                _sessionId = value;
                Interlocked.Exchange(ref Unsafe.As<ulong, long>(ref _counter), 0);
            }
        }
    }

    public static class SigCoreProtocol {
        public static string Encode(SigCoreMessage msg) => msg.ToString() + "\n";
        public static SigCoreMessage Decode(string raw) => SigCoreMessage.FromJson(raw);
    }
}