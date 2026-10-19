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
    public void MapAnalogOutput(WebApplication app) {
        app.MapPost("/api/control/analog-output", async (HttpRequest request) =>
            await ExecuteWrite(async () => {
                JObject body = await ReadBody(request);
                uint channel = RequiredChannel(body, 4);
                double value = RequiredDouble(body, "value");
                await core.SetAOutValueAsync(channel, value);
                return new JObject { ["channel"] = channel, ["requestedValue"] = value,
                    ["accepted"] = true, ["confirmed"] = false };
            }));
    }
}
