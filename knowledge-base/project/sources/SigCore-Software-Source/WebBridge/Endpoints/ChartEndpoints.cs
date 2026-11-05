using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SigCoreCommon;
using System;
using System.IO;
using System.Threading.Tasks;

// Uses the existing loopback HTTP API. Apache/PHP remains the public web layer.
internal static class ChartEndpoints {
    private static IResult Json(JObject result) => Results.Text(result.ToString(Formatting.None), "application/json");
    private static IResult Fail(string message, int status) =>
        Results.Text(new JObject { ["error"] = message }.ToString(Formatting.None), "application/json", statusCode: status);

    public static void Map(WebApplication app, ChartRecorder recorder, CoreState state) {
        app.MapGet("/api/chart/history", (HttpRequest request) => {
            try {
                if (!long.TryParse(request.Query["start"], out long start) ||
                    !long.TryParse(request.Query["length"], out long length) ||
                    !int.TryParse(request.Query["samples"], out int samples))
                    return Fail("start (Unix milliseconds), length (milliseconds), and samples are required.", 400);
                return Json(recorder.Query(start, length, samples));
            } catch (ArgumentException ex) { return Fail(ex.Message, 400); }
              catch (Exception ex) {
                state.log.Error("Chart", "HistoryFailure", ex.ToString(), "ChartEndpoints.cs");
                return Fail("Unable to retrieve chart history.", 500);
              }
        });

        app.MapGet("/api/chart/config", () => Json(recorder.GetConfiguration()));

        app.MapPut("/api/chart/config", async (HttpRequest request) => {
            try {
                if (request.ContentLength > 16384) return Fail("Configuration exceeds 16 KB.", 400);
                using var reader = new StreamReader(request.Body);
                string raw = await reader.ReadToEndAsync();
                if (raw.Length > 16384) return Fail("Configuration exceeds 16 KB.", 400);
                JObject changes = JObject.Parse(raw);
                return Json(recorder.UpdateConfiguration(changes));
            } catch (ArgumentException ex) { return Fail(ex.Message, 400); }
              catch (JsonException) { return Fail("Invalid JSON configuration.", 400); }
              catch (Exception ex) {
                state.log.Error("Chart", "ConfigFailure", ex.ToString(), "ChartEndpoints.cs");
                return Fail("Unable to save chart configuration.", 500);
              }
        });
    }
}
