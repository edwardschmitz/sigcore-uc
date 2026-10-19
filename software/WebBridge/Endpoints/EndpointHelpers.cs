using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SigCoreCommon;
using SigCoreCommon.Payloads;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
internal sealed partial class ApiActions {
    private readonly SigCoreSystem core;
    private readonly Log log;
    public ApiActions(CoreState state) { core = state.core; log = state.log; }
    async Task<IResult> ExecuteRead(Func<Task<JObject>> operation) {
        if (core.ConnectionState != SigCoreConnectionState.Connected)
            return Results.Json(new { error = "Core is not connected." }, statusCode: 503);
        try { return Results.Text((await operation()).ToString(Formatting.None), "application/json"); }
        catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
        catch (Exception ex) {
            log.Error("API", "ReadFailure", ex.ToString(), "Program.cs");
            return Results.Json(new { error = "Core request failed." }, statusCode: 502);
        }
    }
    
    async Task<IResult> ExecuteWrite(Func<Task<JObject>> operation) {
        if (core.ConnectionState != SigCoreConnectionState.Connected)
            return Results.Json(new { error = "Core is not connected." }, statusCode: 503);
        try { return Results.Text((await operation()).ToString(Formatting.None), "application/json"); }
        catch (ArgumentException ex) {
            log.Info("Configuration", "WriteValidationFailure", ex.Message);
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (Newtonsoft.Json.JsonException) { return Results.BadRequest(new { error = "Invalid JSON request." }); }
        catch (Exception ex) {
            log.Error("API", "WriteFailure", ex.ToString(), "Program.cs");
            return Results.Json(new { error = "Core command failed." }, statusCode: 502);
        }
    }
    
    static async Task<JObject> ReadBody(HttpRequest request) {
        if (request.ContentLength > 16384)
            throw new ArgumentException("Request body exceeds 16 KB.");
        using StreamReader reader = new StreamReader(request.Body);
        string content = await reader.ReadToEndAsync();
        if (content.Length > 16384) throw new ArgumentException("Request body exceeds 16 KB.");
        try { return JObject.Parse(content); }
        catch (Newtonsoft.Json.JsonException) { throw new ArgumentException("Invalid JSON request."); }
    }
    
    static uint CheckedChannel(int channel, int count) {
        if (channel < 0 || channel >= count)
            throw new ArgumentException($"Channel must be between 0 and {count - 1}.");
        return (uint)channel;
    }
    static uint RequiredChannel(JObject body, int count) {
        if (body["channel"]?.Type != JTokenType.Integer)
            throw new ArgumentException("Integer 'channel' is required.");
        return CheckedChannel(body.Value<int>("channel"), count);
    }
    static int ChannelCount(string kind) => kind.ToLowerInvariant() switch {
        "relay" or "di" => 8,
        "ai" or "ao" or "pid" => 4,
        _ => throw new ArgumentException("Unsupported configuration type.")
    };
    static bool RequiredBool(JObject body, string key) {
        if (body[key]?.Type != JTokenType.Boolean)
            throw new ArgumentException($"Boolean '{key}' is required.");
        return body.Value<bool>(key);
    }
    static double RequiredDouble(JObject body, string key) {
        if (body[key]?.Type != JTokenType.Integer && body[key]?.Type != JTokenType.Float)
            throw new ArgumentException($"Numeric '{key}' is required.");
        double value = body.Value<double>(key);
        if (!double.IsFinite(value)) throw new ArgumentException($"Invalid '{key}'.");
        return value;
    }
 }
