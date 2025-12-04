using Newtonsoft.Json.Linq;

namespace SigCoreCommon {
    public interface IDispatcher {
        SigCoreMessage HandlePing(JObject payload, ulong msgId, ISession session);
        SigCoreMessage HandlePong(JObject payload, ulong msgId, ISession session);
        SigCoreMessage HandleSetRelay(JObject payload, ulong msgId, ISession session);
        SigCoreMessage HandleGetRelay(JObject payload, ulong msgId, ISession session);
        SigCoreMessage HandleRelay(JObject payload, ulong msgId, ISession session);
        SigCoreMessage HandleStatus(JObject payload, ulong msgId, ISession session);
        SigCoreMessage HandleError(JObject payload, ulong msgId, ISession session);
        SigCoreMessage HandleConnect(JObject payload, ulong msgId, ISession session, string ver);
        SigCoreMessage HandleGetAnalogIn(JObject payload, ulong msgId, ISession session);
        SigCoreMessage HandleAnalogInValue(JObject payload, ulong msgId, ISession session);
        SigCoreMessage HandleAInConfig(JObject payload, ulong msgId, ISession session);
        SigCoreMessage HandleGetAInConfig(JObject payload, ulong msgId, ISession session);
        SigCoreMessage HandleSetAInConfig(JObject payload, ulong msgId, ISession session); // 🔹 Added
        SigCoreMessage HandleGetDIn(JObject payload, ulong msgId, ISession session);
        SigCoreMessage HandleDIn(JObject payload, ulong msgId, ISession session);
        SigCoreMessage HandleGetAOut(JObject payload, ulong msgId, ISession session);
        SigCoreMessage HandleSetAOut(JObject payload, ulong msgId, ISession session);
        SigCoreMessage HandleAOut(JObject payload, ulong msgId, ISession session);
        SigCoreMessage HandleGetAOutConfig(JObject payload, ulong msgId, ISession session);
        SigCoreMessage HandleSetAOutConfig(JObject payload, ulong msgId, ISession session);
        SigCoreMessage HandleAOutConfig(JObject payload, ulong msgId, ISession session);
        SigCoreMessage HandleGetGlobalConfig(JObject payload, ulong msgId, ISession session);
        SigCoreMessage HandleSetGlobalConfig(JObject payload, ulong msgId, ISession session);
        SigCoreMessage HandleGlobalConfig(JObject payload, ulong msgId, ISession session);
        SigCoreMessage HandleGetRelayConfig(JObject payload, ulong msgId, ISession session);
        SigCoreMessage HandleSetRelayConfig(JObject payload, ulong msgId, ISession session);
        SigCoreMessage HandleRelayConfig(JObject payload, ulong msgId, ISession session);
        SigCoreMessage HandleGetDInConfig(JObject payload, ulong msgId, ISession session);
        SigCoreMessage HandleSetDInConfig(JObject payload, ulong msgId, ISession session);
        SigCoreMessage HandleDInConfig(JObject payload, ulong msgId, ISession session);
        SigCoreMessage HandleGetPIDConfig(JObject payload, ulong msgId, ISession session);
        SigCoreMessage HandleSetPIDConfig(JObject payload, ulong msgId, ISession session);
        SigCoreMessage HandlePIDConfig(JObject payload, ulong msgId, ISession session);
        SigCoreMessage HandleGetPIDCurVal(JObject payload, ulong msgId, ISession session);
        SigCoreMessage HandleSetPIDCurVal(JObject payload, ulong msgId, ISession session);
        SigCoreMessage HandlePIDCurVal(JObject payload, ulong msgId, ISession session);
        SigCoreMessage HandleSubscribe(JObject payload, ulong msgId, ISession session);
        SigCoreMessage HandleDInChangeAlert(JObject payload, ulong msgId, ISession session);
        SigCoreMessage HandleAInChangeAlert(JObject payload, ulong msgId, ISession session);
        SigCoreMessage HandleRelayChangeAlert(JObject payload, ulong msgId, ISession session);
        SigCoreMessage HandleAOutChangeAlert(JObject payload, ulong msgId, ISession session);
        SigCoreMessage HandlePIDCurValChangeAlert(JObject payload, ulong msgId, ISession session);
        SigCoreMessage HandleGetFRAM(JObject payload, ulong msgId, ISession session);
        SigCoreMessage HandleFRAM(JObject payload, ulong msgId, ISession session);
        SigCoreMessage HandleResetPID(JObject payload, ulong msgId, ISession session);
        SigCoreMessage HandleGetLoggingConfig(JObject payload, ulong msgId, ISession session);
        SigCoreMessage HandleSetLoggingConfig(JObject payload, ulong msgId, ISession session);
        SigCoreMessage HandleLoggingConfig(JObject payload, ulong msgId, ISession session);
        SigCoreMessage HandleGetLoggingStatus(JObject payload, ulong msgId, ISession session);
        SigCoreMessage HandleLoggingStatus(JObject payload, ulong msgId, ISession session);
        SigCoreMessage HandleRestart(JObject payload, ulong msgId, ISession session);
        SigCoreMessage HandleShutdown(JObject payload, ulong msgId, ISession session);
        SigCoreMessage HandleGetStatus(Object payload, ulong msgId, ISession session);
        SigCoreMessage HandleFactoryReset(Object payload, ulong msgId, ISession session);


        Task NotifyDInChanged(bool[] vals);
        Task NotifyAInChanged(double[] vals);
        Task NotifyRelayChange(uint channel, bool val);
        Task NotifyAOutChanged(uint channel, double val, bool auto);
        Task NotifyPIDChanged(uint channel, JObject payload );
        Task NotifyLoggerChanged(string status, bool logging);
    }
}
