using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SigCoreCommon;
using SigCoreCommon.Payloads;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

// Configuration-only bridge. Existing Core protocol and setters are unchanged.
internal static class DeviceSettingsEndpoints {
    private static JObject PublicNetwork(JObject json) {
        return (JObject)json.DeepClone();
    }
    private static JObject PublicLogger(JObject json) {
        JObject copy = (JObject)json.DeepClone();
        // Preserve the stored password in the settings response so the UI can display it.
        return copy;
    }
    private static async Task<JObject> Body(HttpRequest request) {
        if (request.ContentLength > 16384) throw new ArgumentException("Request exceeds 16 KB.");
        using var reader = new StreamReader(request.Body);
        string input = await reader.ReadToEndAsync();
        if (input.Length > 16384) throw new ArgumentException("Request exceeds 16 KB.");
        return JObject.Parse(input);
    }
    private static void Copy(JObject target, JObject source, params string[] names) {
        foreach (string name in names) {
            if (source.TryGetValue(name, out JToken? value)) {
                if (value == null || value.Type == JTokenType.Null) throw new ArgumentException("Invalid field: " + name);
                target[name] = value.DeepClone();
            }
        }
    }
    private static IResult Json(JObject obj) => Results.Text(obj.ToString(Formatting.None), "application/json");
    private static IResult ErrorJson(string error, int status) => Results.Text(new JObject { ["error"] = error }.ToString(Formatting.None), "application/json", statusCode: status);
    public static void Map(WebApplication app, CoreState state) {
        var core = state.core;
        app.MapGet("/api/device-settings/{kind}", async (string kind) => {
            var timer = Stopwatch.StartNew();
            if (core.ConnectionState != SigCoreConnectionState.Connected) return ErrorJson("Core disconnected", 503);
            try {
                IResult result = kind switch {
                    "system" => Json(PublicSystem((await core.GetGlobalConfigAsync()).ToJson())),
                    "network" => Json(PublicNetwork((await core.GetNetworkConfigAsync()).ToJson())),
                    "logger" => Json(PublicLogger((await core.GetLoggingConfigAsync()).ToJson())),
                    _ => Results.NotFound()
                };
                state.log.Info("DeviceSettings", "ReadCompleted", $"kind={kind}; elapsedMs={timer.ElapsedMilliseconds}", "DeviceSettingsEndpoints.cs");
                return result;
            } catch (Exception e) {
                state.log.Error("DeviceSettings", "ReadFailed", $"kind={kind}; elapsedMs={timer.ElapsedMilliseconds}; {e}", "DeviceSettingsEndpoints.cs");
                return ErrorJson("Unable to load settings", 502);
            }
        });
        app.MapPut("/api/device-settings/{kind}", async (string kind, HttpRequest request) => {
            var timer = Stopwatch.StartNew();
            if (core.ConnectionState != SigCoreConnectionState.Connected) return ErrorJson("Core disconnected", 503);
            if (kind != "system" && kind != "network" && kind != "logger") return Results.NotFound();
            try {
                state.log.Info("DeviceSettings", "WriteReceived", $"kind={kind}", "DeviceSettingsEndpoints.cs");
                JObject changes = await Body(request);
                state.log.Info("DeviceSettings", "WriteParsed", $"kind={kind}; fieldCount={changes.Count}", "DeviceSettingsEndpoints.cs");
                switch (kind) {
                    case "system": {
                        var readTimer = Stopwatch.StartNew();
                        JObject current;
                        try { current = (JObject)(await core.GetGlobalConfigAsync()).ToJson().DeepClone(); }
                        finally { state.log.Info("DeviceSettings", "WritePrereadElapsed", $"kind=system; elapsedMs={readTimer.ElapsedMilliseconds}", "DeviceSettingsEndpoints.cs"); }
                        RejectUnexpected(changes,"systemName","analogSamplingRate","pwm_frequency");
                        Copy(current,changes,"systemName","analogSamplingRate","pwm_frequency");
                        int rate = current.Value<int?>("analogSamplingRate") ?? 1;
                        int pwm = current.Value<int?>("pwm_frequency") ?? 250;
                        if (rate < 0 || rate > 2 || pwm < 1 || pwm > 1000000) throw new ArgumentException("Invalid sampling rate or PWM frequency.");
                        await core.SetGlobalConfigAsync(new SystemConfigPayload(current)); break;
                    }
                    case "network": {
                        RejectUnexpected(changes,"hostname","ethernet","wifi");
                        var readTimer = Stopwatch.StartNew();
                        JObject current;
                        try { current = (JObject)(await core.GetNetworkConfigAsync()).ToJson().DeepClone(); }
                        finally { state.log.Info("DeviceSettings", "WritePrereadElapsed", $"kind=network; elapsedMs={readTimer.ElapsedMilliseconds}", "DeviceSettingsEndpoints.cs"); }
                        if (changes.TryGetValue("hostname",out JToken? host)) {
                            string? h=host?.Value<string>();
                            if (string.IsNullOrWhiteSpace(h) || h.Length>63) throw new ArgumentException("Invalid hostname.");
                            current["hostname"]=h;
                        }
                        foreach (string section in new[]{"ethernet","wifi"}) {
                            if (changes[section] is JObject submitted) {
                                RejectUnexpected(submitted,section=="wifi" ? new[]{"ssid","psk","dhcpEnabled","ipAddress","subnetMask","gateway","dns"} : new[]{"dhcpEnabled","ipAddress","subnetMask","gateway","dns"});
                                JObject dest=(JObject)current[section]!;
                                foreach (var prop in submitted.Properties()) {
                                    if (prop.Value.Type==JTokenType.Null) throw new ArgumentException("Invalid "+prop.Name);
                                    // Blank password means retain current stored PSK.
                                    if (section=="wifi" && prop.Name=="psk" && string.IsNullOrEmpty(prop.Value.Value<string>())) continue;
                                    dest[prop.Name]=prop.Value.DeepClone();
                                }
                            }
                        }
                        state.log.Info("DeviceSettings", "NetworkWriteStart", "Calling SigCoreCommon.SetNetworkConfigAsync", "DeviceSettingsEndpoints.cs");
                        await core.SetNetworkConfigAsync(new NetworkSettingsPayload(current));
                        state.log.Info("DeviceSettings", "NetworkWriteReturned", "SetNetworkConfigAsync returned", "DeviceSettingsEndpoints.cs");
                        break;
                    }
                    case "logger": {
                        RejectUnexpected(changes,"enabled","intervalSec","server","port","database","user","password","tableName","connectionTimeoutSec","commandTimeoutSec");
                        var readTimer = Stopwatch.StartNew();
                        JObject current;
                        try { current = (JObject)(await core.GetLoggingConfigAsync()).ToJson().DeepClone(); }
                        finally { state.log.Info("DeviceSettings", "WritePrereadElapsed", $"kind=logger; elapsedMs={readTimer.ElapsedMilliseconds}", "DeviceSettingsEndpoints.cs"); }
                        if (changes.Value<string>("password")=="") changes.Remove("password");
                        Copy(current,changes,"enabled","intervalSec","server","port","database","user","password","tableName","connectionTimeoutSec","commandTimeoutSec");
                        var payload=new LoggerConfigPayload(current);
                        if (!double.IsFinite(payload.IntervalSec) || payload.IntervalSec<=0 || payload.Port<1 || payload.Port>65535 || payload.ConnectionTimeoutSec<1 || payload.CommandTimeoutSec<1) throw new ArgumentException("Invalid logger settings.");
                        await core.SetLoggingConfigAsync(payload);break;
                    }
                }
                state.log.Info("DeviceSettings", "WriteResponse", $"kind={kind}; accepted=true; elapsedMs={timer.ElapsedMilliseconds}", "DeviceSettingsEndpoints.cs");
                return Results.Text("{\"accepted\":true,\"confirmed\":false}", "application/json");
            } catch (ArgumentException e) {state.log.Error("DeviceSettings","WriteValidationFailed",$"kind={kind}; elapsedMs={timer.ElapsedMilliseconds}; {e}","DeviceSettingsEndpoints.cs");return ErrorJson(e.Message, 400);}
              catch (JsonException) {return ErrorJson("Invalid JSON", 400);}
              catch (Exception e) {state.log.Error("DeviceSettings","WriteFailed",$"kind={kind}; elapsedMs={timer.ElapsedMilliseconds}; {e}","DeviceSettingsEndpoints.cs");return ErrorJson("Unable to transmit settings", 502);}
        });
    }
    private static JObject PublicSystem(JObject json) {
        JObject safe = new JObject();
        foreach(string name in new[]{"systemName","analogSamplingRate","pwm_frequency"})
            if (json.TryGetValue(name,out JToken? val)) safe[name]=val?.DeepClone();
        return safe;
    }
    private static void RejectUnexpected(JObject changes,params string[] allowed) {
        foreach(var prop in changes.Properties()) if (Array.IndexOf(allowed,prop.Name)<0) throw new ArgumentException("Unsupported setting: "+prop.Name);
    }
}
