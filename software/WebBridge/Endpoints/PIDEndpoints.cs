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
    public void MapPID(WebApplication app) {
        app.MapPost("/api/control/pid/{channel:int}/{action}", async (int channel, string action, HttpRequest request) =>
            await ExecuteWrite(async () => {
                uint index = CheckedChannel(channel, 4);
                JObject body = await ReadBody(request);
                switch (action.ToLowerInvariant()) {
                    case "setpoint": await core.SetPIDSPAsync(index, RequiredDouble(body, "value")); break;
                    case "output": await core.SetPIDOutputAsync(index, RequiredDouble(body, "value")); break;
                    case "tolerance": await core.SetPIDTolAsync(index, RequiredDouble(body, "value")); break;
                    case "auto": await core.SetPIDAutoAsync(index, RequiredBool(body, "value")); break;
                    case "ramp-time": await core.SetPIDRampTimeAsync(index, RequiredDouble(body, "value")); break;
                    case "ramp-target": await core.SetPIDRampTargetAsync(index, RequiredDouble(body, "value")); break;
                    case "ramp": await core.SetPIDRampAsync(index, RequiredBool(body, "value")); break;
                    case "reset": await core.ResetPIDAsync(index); break;
                    default: throw new ArgumentException("Unsupported PID action.");
                }
                return new JObject { ["accepted"] = true, ["confirmed"] = false,
                    ["channel"] = index, ["action"] = action };
            }));
    }
}
