using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

// Backup/restore is managed by WebBridge. Core reloads properties.json after Launcher restart.
internal static class BackupRestoreEndpoints {
    private const string PropertiesPath = "/opt/sigcore/properties.json";
    private const int MaxBackupBytes = 1024 * 1024;
    private static readonly object FileLock = new();
    // Use JSON text responses so no anonymous System.Text.Json metadata is required.
    private static IResult ErrorJson(string message, int status) =>
        Results.Text(new JObject { ["error"] = message }.ToString(Formatting.None), "application/json", statusCode: status);
    private static bool IsDeviceSetting(string key) =>
        !string.Equals(key, "global", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(key, "network", StringComparison.OrdinalIgnoreCase);

    public static void Map(WebApplication app, CoreState state) {
        app.MapGet("/api/config-backup", () => {
            try {
                JObject backup;
                lock (FileLock) {
                    JObject current = JObject.Parse(File.ReadAllText(PropertiesPath));
                    backup = new JObject();
                    foreach (var item in current.Properties())
                        if (IsDeviceSetting(item.Name)) backup[item.Name] = item.Value.DeepClone();
                }
                if (!backup.Properties().Any()) throw new InvalidDataException("No configuration sections found.");
                string serial = "SigCore";
                // Filename is only a download suggestion; device identity is never included in the backup.
                lock (FileLock) {
                    var global = JObject.Parse(File.ReadAllText(PropertiesPath))["global"] as JObject;
                    serial = global?.Value<string>("systemSerialNumber") ?? serial;
                }
                serial = new string(serial.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());
                return (IResult)Results.File(System.Text.Encoding.UTF8.GetBytes(backup.ToString(Formatting.Indented) + "\n"),
                    "application/json", $"{serial}-backup.json");
            } catch (Exception ex) {
                state.log.Error("Maintenance", "BackupFailed", ex.ToString(), "BackupRestoreEndpoints.cs");
                return (IResult)ErrorJson("Unable to create configuration backup.", 500);
            }
        });

        app.MapPost("/api/config-restore", async (HttpRequest request) => {
            try {
                if (request.ContentLength > MaxBackupBytes)
                    return ErrorJson("Backup exceeds 1 MB.", 400);
                using var reader = new StreamReader(request.Body);
                string raw = await reader.ReadToEndAsync();
                if (System.Text.Encoding.UTF8.GetByteCount(raw) > MaxBackupBytes)
                    return ErrorJson("Backup exceeds 1 MB.", 400);
                if (JToken.Parse(raw) is not JObject upload)
                    return ErrorJson("Backup must be a JSON object.", 400);
                if (upload.Properties().Any(p => !IsDeviceSetting(p.Name)))
                    return ErrorJson("Backup must not contain global or network sections.", 400);
                lock (FileLock) {
                    JObject current = JObject.Parse(File.ReadAllText(PropertiesPath));
                    var sections = current.Properties().Where(p => IsDeviceSetting(p.Name)).Select(p => p.Name).ToArray();
                    var provided = upload.Properties().Select(p => p.Name).ToArray();
                    if (sections.Length == 0 || provided.Length != sections.Length ||
                        provided.Any(name => !sections.Contains(name, StringComparer.Ordinal)))
                        throw new ArgumentException("Backup sections do not match this device's configuration.");
                    foreach (string section in sections) {
                        JToken old = current[section]!;
                        JToken incoming = upload[section]!;
                        if (incoming.Type != old.Type || (incoming is not JObject && incoming is not JArray))
                            throw new ArgumentException("Invalid section: " + section);
                        if (old is JArray oldArray && incoming is JArray newArray) {
                            if (oldArray.Count != newArray.Count || newArray.Any(x => x.Type != JTokenType.Object))
                                throw new ArgumentException("Invalid entries in section: " + section);
                            // Preserve channel layout so restoring can't change the hardware configuration shape.
                            for (int i = 0; i < oldArray.Count; i++) {
                                if (!JToken.DeepEquals(oldArray[i]?["channel"], newArray[i]?["channel"]))
                                    throw new ArgumentException("Channel mismatch in section: " + section);
                            }
                        }
                        if (old is JObject oldObject && incoming is JObject newObject &&
                            !oldObject.Properties().Select(p => p.Name).OrderBy(x=>x).SequenceEqual(newObject.Properties().Select(p=>p.Name).OrderBy(x=>x)))
                            throw new ArgumentException("Fields mismatch in section: " + section);
                    }
                    foreach (string section in sections) current[section] = upload[section]!.DeepClone();
                    // Write alongside target then atomically replace, with no partial properties file.
                    string temp = PropertiesPath + ".restore-" + Guid.NewGuid().ToString("N");
                    try {
                        File.WriteAllText(temp, current.ToString(Formatting.Indented) + "\n");
                        File.SetUnixFileMode(temp, File.GetUnixFileMode(PropertiesPath));
                        File.Move(temp, PropertiesPath, true);
                    } finally { if (File.Exists(temp)) File.Delete(temp); }
                }
                state.log.Info("Maintenance", "RestoreAccepted", "Application configuration restored; launcher restart queued", "BackupRestoreEndpoints.cs");
                // Queue the restart only after ASP.NET has completed the HTTP response.
                // This lets the PHP proxy receive success before WebBridge is terminated.
                request.HttpContext.Response.OnCompleted(() => {
                    _ = Task.Run(async () => {
                        await Task.Delay(1500);
                        try {
                            using var process = new Process { StartInfo = new ProcessStartInfo("/usr/bin/sudo") {
                                UseShellExecute = false, RedirectStandardError = true, CreateNoWindow = true
                            }};
                            process.StartInfo.ArgumentList.Add("-n");
                            process.StartInfo.ArgumentList.Add("/usr/bin/systemctl");
                            process.StartInfo.ArgumentList.Add("restart");
                            process.StartInfo.ArgumentList.Add("--no-block");
                            process.StartInfo.ArgumentList.Add("sigcorelauncher.service");
                            process.Start();
                            string error = await process.StandardError.ReadToEndAsync();
                            await process.WaitForExitAsync();
                            if (process.ExitCode != 0)
                                state.log.Error("Maintenance", "RestoreRestartFailed", $"exit={process.ExitCode}; {error}", "BackupRestoreEndpoints.cs");
                        } catch (Exception ex) {
                            state.log.Error("Maintenance", "RestoreRestartFailed", ex.ToString(), "BackupRestoreEndpoints.cs");
                        }
                    });
                    return Task.CompletedTask;
                });
                return Results.Text("{\"accepted\":true,\"restarting\":true}", "application/json");
            } catch (JsonReaderException) { return ErrorJson("Invalid JSON backup.", 400); }
              catch (ArgumentException ex) { return ErrorJson(ex.Message, 400); }
              catch (Exception ex) {
                state.log.Error("Maintenance", "RestoreFailed", ex.ToString(), "BackupRestoreEndpoints.cs");
                return ErrorJson("Unable to restore configuration.", 500);
            }
        });
    }
}
