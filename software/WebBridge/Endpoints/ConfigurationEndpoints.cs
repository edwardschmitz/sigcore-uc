using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SigCoreCommon;
using SigCoreCommon.Payloads;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
internal sealed partial class ApiActions {
    public void MapConfiguration(WebApplication app) {
        app.MapGet("/api/config/{kind}/{channel:int}", async (string kind, int channel) =>
            await ExecuteRead(async () => {
                uint index = CheckedChannel(channel, ChannelCount(kind));
                return kind.ToLowerInvariant() switch {
                    "relay" => (await core.GetRelayConfigAsync(index)).ToJson(),
                    "di" => (await core.GetDInConfigAsync(index)).ToJson(),
                    "ai" => (await core.GetAInConfigAsync(index)).ToJson(),
                    "ao" => (await core.GetAOutConfigAsync(index)).ToJson(),
                    "pid" => (await core.GetPIDConfigAsync(index)).ToJson(),
                    _ => throw new ArgumentException("Unsupported configuration type.")
                };
            }));
        
        app.MapPut("/api/config/{kind}/{channel:int}", async (string kind, int channel, HttpRequest request) => {
            // A single correlation ID lets one save operation be followed end to end.
            string trace = Guid.NewGuid().ToString("N").Substring(0, 8);
            string stage = "entry";
            log.Info("Configuration", "PutEntered", $"trace={trace}, type={kind}, channel={channel}, method={request.Method}, contentType={request.ContentType ?? "(none)"}, contentLength={request.ContentLength?.ToString() ?? "unknown"}");
            try {
                // ExecuteWrite preserves the existing HTTP responses and connection check.
                IResult result = await ExecuteWrite(async () => {
                    stage = "channel-validation";
                    log.Info("Configuration", "ChannelCheck", $"trace={trace}, kind={kind}, channel={channel}");
                    uint index = CheckedChannel(channel, ChannelCount(kind));
                    log.Info("Configuration", "ChannelValid", $"trace={trace}, index={index}");

                    stage = "request-parsing";
                    JObject body = await ReadBody(request);
                    log.Info("Configuration", "BodyParsed", $"trace={trace}, body={body.ToString(Formatting.None)}");
                    stage = "config-validation";
                    if (body["config"] is not JObject changes || !changes.HasValues)
                        throw new ArgumentException("Nonempty 'config' object is required.");
                    // Channel is identified and validated by the URL, not edited in the body.
                    // GET returns it as metadata; ignore that copy when saving.
                    if (changes.Remove("channel"))
                        log.Info("Configuration", "ChannelFieldIgnored", $"trace={trace}, usingUrlChannel={index}");
                    string name = kind.ToLowerInvariant();
                    string[] allowed = name switch {
                        "relay" => new[] {"name", "defaultState", "failSafeState", "invertedLogic"},
                        "di" => new[] {"name", "debounce", "inverted"},
                        "ai" => new[] {"name", "units", "avgSamples", "precision", "calType", "range", "display", "m", "b", "polyString", "pieceWiseString"},
                        "ao" => new[] {"name", "units", "auto", "precision", "mode", "display", "voltM", "voltB"},
                        "pid" => new[] {"title", "isEnabled", "autoMode", "kp", "ki", "kd", "outputMin", "outputMax", "pvMin", "pvMax", "pvSource", "outputDestination", "integralLimit", "resetOnModeChange"},
                        _ => throw new ArgumentException("Unsupported configuration type.")
                    };
                    log.Info("Configuration", "FieldsCheck", $"trace={trace}, type={name}, supplied={string.Join(",", changes.Properties().Select(p => p.Name))}, required={string.Join(",", allowed)}");
                    foreach (JProperty property in changes.Properties()) {
                        if (Array.IndexOf(allowed, property.Name) < 0) {
                            log.Info("Configuration", "FieldRejected", $"trace={trace}, unsupported={property.Name}");
                            throw new ArgumentException("Unsupported or read-only configuration field: " + property.Name);
                        }
                        if (property.Value.Type == JTokenType.Null ||
                            (property.Value.Type == JTokenType.Float && !double.IsFinite(property.Value.Value<double>()))) {
                            log.Info("Configuration", "FieldRejected", $"trace={trace}, invalid={property.Name}, tokenType={property.Value.Type}");
                            throw new ArgumentException("Invalid configuration field: " + property.Name);
                        }
                    }
                    foreach (string field in allowed) {
                        if (!changes.ContainsKey(field)) {
                            log.Info("Configuration", "FieldMissing", $"trace={trace}, field={field}");
                            throw new ArgumentException("Complete configuration required. Missing: " + field);
                        }
                    }
                    log.Info("Configuration", "Validated", $"trace={trace}, type={name}, channel={index}");

                    stage = "payload-construction";
                    JObject updated = (JObject)changes.DeepClone();
                    updated["channel"] = index;
                    log.Info("Configuration", "PayloadInput", $"trace={trace}, payload={updated.ToString(Formatting.None)}");
                    stage = "setter-call";
                    switch (name) {
                        case "relay": {
                            RelayConfigPayload payload = new RelayConfigPayload(updated);
                            log.Info("Configuration", "PayloadConstructed", $"trace={trace}, type=relay, payload={payload.ToJson().ToString(Formatting.None)}");
                            log.Info("Configuration", "SetterCalling", $"trace={trace}, method=SetRelayConfigAsync, index={index}");
                            await core.SetRelayConfigAsync(index, payload);
                            break;
                        }
                        case "di": {
                            DInConfigPayload payload = new DInConfigPayload(updated);
                            log.Info("Configuration", "PayloadConstructed", $"trace={trace}, type=di, payload={payload.ToJson().ToString(Formatting.None)}");
                            log.Info("Configuration", "SetterCalling", $"trace={trace}, method=SetDInConfigAsync");
                            await core.SetDInConfigAsync(payload);
                            break;
                        }
                        case "ai": {
                            AInConfigPayload payload = new AInConfigPayload(updated);
                            log.Info("Configuration", "PayloadConstructed", $"trace={trace}, type=ai, payload={payload.ToJson().ToString(Formatting.None)}");
                            log.Info("Configuration", "SetterCalling", $"trace={trace}, method=SetAInConfigAsync");
                            await core.SetAInConfigAsync(payload);
                            break;
                        }
                        case "ao": {
                            AOutConfigPayload payload = new AOutConfigPayload(updated);
                            log.Info("Configuration", "PayloadConstructed", $"trace={trace}, type=ao, payload={payload.ToJson().ToString(Formatting.None)}");
                            log.Info("Configuration", "SetterCalling", $"trace={trace}, method=SetAOutConfigAsync");
                            await core.SetAOutConfigAsync(payload);
                            break;
                        }
                        case "pid": {
                            PidConfigPayload payload = new PidConfigPayload(updated);
                            log.Info("Configuration", "PayloadConstructed", $"trace={trace}, type=pid, payload={payload.ToJson().ToString(Formatting.None)}");
                            log.Info("Configuration", "SetterCalling", $"trace={trace}, method=SetPIDConfigAsync");
                            await core.SetPIDConfigAsync(payload);
                            break;
                        }
                    }
                    log.Info("Configuration", "SetterReturned", $"trace={trace}, type={name}, channel={index}; sent, not confirmed applied");
                    stage = "response-construction";
                    JObject response = new JObject { ["accepted"] = true, ["confirmed"] = false,
                        ["channel"] = index, ["type"] = name };
                    log.Info("Configuration", "ResponsePrepared", $"trace={trace}, response={response.ToString(Formatting.None)}");
                    return response;
                });
                log.Info("Configuration", "RequestFinished", $"trace={trace}, lastStage={stage}, resultType={result.GetType().Name}; result can be error status");
                return result;
            } catch (Exception ex) {
                log.Error("Configuration", "UnexpectedFailure", $"trace={trace}, stage={stage}, exception={ex}", "ConfigurationEndpoints.cs");
                throw;
            }
        });

    }
}
