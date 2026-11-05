using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

// Reboot is issued by WebBridge directly to the host OS, not through Core.
internal static class RestartEndpoints {
    public static void Map(WebApplication app, CoreState state) {
        app.MapPost("/api/restart", async () => {
            try {
                using var process = new Process {
                    StartInfo = new ProcessStartInfo("/usr/bin/sudo") {
                        UseShellExecute = false,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                process.StartInfo.ArgumentList.Add("-n");
                process.StartInfo.ArgumentList.Add("/usr/bin/systemctl");
                process.StartInfo.ArgumentList.Add("reboot");
                process.StartInfo.ArgumentList.Add("--no-block");
                if (!process.Start()) throw new InvalidOperationException("Unable to start reboot command.");
                string stderr = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();
                if (process.ExitCode != 0) {
                    state.log.Error("Maintenance", "RestartFailed", $"OS reboot command exited {process.ExitCode}: {stderr}", "RestartEndpoints.cs");
                    return Results.Json(new { error = "OS rejected restart command. Check WebBridge permissions." }, statusCode: 500);
                }
                state.log.Info("Maintenance", "RestartRequested", "OS accepted reboot request", "RestartEndpoints.cs");
                return Results.Json(new { accepted = true });
            } catch (Exception ex) {
                state.log.Error("Maintenance", "RestartFailed", ex.ToString(), "RestartEndpoints.cs");
                return Results.Json(new { error = "Unable to request device restart." }, statusCode: 500);
            }
        });
    }
}
