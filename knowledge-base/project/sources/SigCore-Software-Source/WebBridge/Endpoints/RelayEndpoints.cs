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
    public void MapRelay(WebApplication app) {
        app.MapPost("/api/control/relay", async (HttpRequest request) =>
            await ExecuteWrite(async () => {
                JObject body = await ReadBody(request);
                uint channel = RequiredChannel(body, 8);
                bool state = RequiredBool(body, "state");
                await core.SetRelayAsync(channel, state);
                return new JObject { ["channel"] = channel, ["requestedState"] = state,
                    ["accepted"] = true, ["confirmed"] = false };
            }));
    }
}
