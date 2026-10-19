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
internal static class StatusEndpoints {
    public static void Map(WebApplication app, CoreState cache) {
        var core = cache.core;
        var log = cache.log;
        app.MapGet("/api/connection", () => {
            try {
                JObject response = new JObject {
                    ["state"] = core.ConnectionState.ToString(),
                    ["connected"] = core.ConnectionState == SigCoreConnectionState.Connected
                };
        
                return Results.Text(response.ToString(Formatting.None), "application/json");
            } catch (Exception ex) {
                log.Error("API", "ConnectionFailure",
                    $"/api/connection failed: {ex}", "Program.cs");
        
                JObject response = new JObject {
                    ["error"] = ex.Message
                };
        
                return Results.Text(
                    response.ToString(Formatting.None),
                    "application/json",
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        });
        
        app.MapGet("/api/state", () => {
            try {
                JObject state;
        
                lock (cache.stateLock) {
                    state = new JObject {
                        ["connection"] = new JObject {
                            ["state"] = core.ConnectionState.ToString(),
                            ["connected"] = core.ConnectionState == SigCoreConnectionState.Connected
                        },
                        ["digitalInputs"] = ToBoolJArray(cache.digitalInputs),
                        ["analogInputs"] = ToDoubleJArray(cache.analogInputs),
                        ["relays"] = ToBoolJArray(cache.relays),
                        ["analogOutputs"] = new JObject {
                            ["values"] = ToDoubleJArray(cache.analogOutputs),
                            ["auto"] = ToBoolJArray(cache.analogOutputAuto)
                        },
                        ["pid"] = new JObject {
                            ["setPoints"] = ToDoubleJObject(cache.pidSetPoints),
                            ["processValues"] = ToDoubleJObject(cache.pidProcessValues),
                            ["outputs"] = ToDoubleJObject(cache.pidOutputs),
                            ["tolerances"] = ToDoubleJObject(cache.pidTolerances),
                            ["deviations"] = ToDoubleJObject(cache.pidDeviations),
                            ["auto"] = ToBoolJObject(cache.pidAuto),
                            ["rampTimes"] = ToDoubleJObject(cache.pidRampTimes),
                            ["rampTargets"] = ToDoubleJObject(cache.pidRampTargets),
                            ["rampEnabled"] = ToBoolJObject(cache.pidRampEnabled)
                        },
                        ["logger"] = new JObject {
                            ["status"] = cache.loggerStatus,
                            ["logging"] = cache.loggerActive
                        }
                    };
                }
        
                return Results.Text(state.ToString(Formatting.None), "application/json");
            } catch (Exception ex) {
                log.Error("API", "StateFailure",
                    $"/api/state failed: {ex}", "Program.cs");
        
                JObject response = new JObject {
                    ["error"] = ex.Message
                };
        
                return Results.Text(
                    response.ToString(Formatting.None),
                    "application/json",
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        });
        
        app.MapGet("/api/logs", async () => {
            const string logPath = "/tmp/sigcore-log/log.txt";
            const int maxBytes = 2 * 1024 * 1024;
        
            try {
                using FileStream stream = new FileStream(logPath, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                long start = Math.Max(0, stream.Length - maxBytes);
                stream.Seek(start, SeekOrigin.Begin);
        
                // A tail may begin inside a log record; skip that partial line.
                if (start > 0) {
                    int current;
                    while ((current = stream.ReadByte()) != -1 && current != '\n') { }
                }
        
                using MemoryStream buffer = new MemoryStream();
                await stream.CopyToAsync(buffer);
                return Results.Bytes(buffer.ToArray(), "text/plain; charset=utf-8");
            } catch (FileNotFoundException) {
                return Results.NotFound("Text log is not available.");
            } catch (DirectoryNotFoundException) {
                return Results.NotFound("Text log is not available.");
            } catch (Exception ex) {
                log.Error("API", "LogsFailure", $"/api/logs failed: {ex}", "Program.cs");
                return Results.StatusCode(StatusCodes.Status500InternalServerError);
            }
        });
        
        // Dashboard network information; never expose the Wi-Fi pre-shared key.
        app.MapGet("/api/network", async () => {
            if (core.ConnectionState != SigCoreConnectionState.Connected)
                return Results.Json(new { error = "SigCore Core Server is not connected." }, statusCode: 503);

            try {
                var config = await core.GetNetworkConfigAsync();
                JObject source = config.ToJson();
                JObject response = new JObject {
                    ["hostname"] = source.Value<string>("hostname"),
                    ["ethernet"] = new JObject {
                        ["dhcpEnabled"] = source["ethernet"]?["dhcpEnabled"]?.DeepClone(),
                        ["ipAddress"] = source["ethernet"]?["ipAddress"]?.DeepClone(),
                        ["subnetMask"] = source["ethernet"]?["subnetMask"]?.DeepClone(),
                        ["gateway"] = source["ethernet"]?["gateway"]?.DeepClone(),
                        ["dns"] = source["ethernet"]?["dns"]?.DeepClone()
                    },
                    ["wifi"] = new JObject {
                        ["ssid"] = source["wifi"]?["ssid"]?.DeepClone(),
                        ["dhcpEnabled"] = source["wifi"]?["dhcpEnabled"]?.DeepClone(),
                        ["ipAddress"] = source["wifi"]?["ipAddress"]?.DeepClone(),
                        ["subnetMask"] = source["wifi"]?["subnetMask"]?.DeepClone(),
                        ["gateway"] = source["wifi"]?["gateway"]?.DeepClone(),
                        ["dns"] = source["wifi"]?["dns"]?.DeepClone()
                    }
                };
                return Results.Text(response.ToString(Formatting.None), "application/json");
            } catch (Exception ex) {
                log.Error("API", "NetworkFailure", $"/api/network failed: {ex}", "StatusEndpoints.cs");
                return Results.Json(new { error = "Unable to retrieve network configuration." }, statusCode: 502);
            }
        });

        app.MapGet("/api/status", async () => {
            if (core.ConnectionState != SigCoreConnectionState.Connected) {
                JObject response = new JObject {
                    ["error"] = "SigCore Core Server is not connected.",
                    ["state"] = core.ConnectionState.ToString()
                };
        
                return Results.Text(
                    response.ToString(Formatting.None),
                    "application/json",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        
            try {
                JObject status = await core.GetStatusAsync();
        
                return Results.Text(
                    status.ToString(Formatting.None),
                    "application/json");
            } catch (Exception ex) {
                log.Error("API", "StatusFailure",
                    $"/api/status failed: {ex}", "Program.cs");
        
                JObject response = new JObject {
                    ["error"] = ex.Message
                };
        
                return Results.Text(
                    response.ToString(Formatting.None),
                    "application/json",
                    statusCode: StatusCodes.Status502BadGateway);
            }
        });
    }
static JArray ToBoolJArray(bool[] values) {
    JArray result = new JArray();
    foreach (bool value in values)
        result.Add(value);
    return result;
}

static JArray ToDoubleJArray(double[] values) {
    JArray result = new JArray();
    foreach (double value in values)
        result.Add(value);
    return result;
}

static JObject ToDoubleJObject(Dictionary<uint, double> values) {
    JObject result = new JObject();
    foreach (KeyValuePair<uint, double> pair in values)
        result[pair.Key.ToString()] = pair.Value;
    return result;
}

static JObject ToBoolJObject(Dictionary<uint, bool> values) {
    JObject result = new JObject();
    foreach (KeyValuePair<uint, bool> pair in values)
        result[pair.Key.ToString()] = pair.Value;
    return result;
}


}
