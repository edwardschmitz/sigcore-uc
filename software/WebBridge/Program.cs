using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SigCoreCommon;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

Log log = new Log();
Log.Initialize("WebBridge");

SigCoreSystem core = new SigCoreSystem();

object stateLock = new object();

bool[] digitalInputs = Array.Empty<bool>();
double[] analogInputs = Array.Empty<double>();
bool[] relays = new bool[8];
double[] analogOutputs = new double[4];
bool[] analogOutputAuto = new bool[4];

Dictionary<uint, double> pidSetPoints = new Dictionary<uint, double>();
Dictionary<uint, double> pidProcessValues = new Dictionary<uint, double>();
Dictionary<uint, double> pidOutputs = new Dictionary<uint, double>();
Dictionary<uint, double> pidTolerances = new Dictionary<uint, double>();
Dictionary<uint, double> pidDeviations = new Dictionary<uint, double>();
Dictionary<uint, bool> pidAuto = new Dictionary<uint, bool>();
Dictionary<uint, double> pidRampTimes = new Dictionary<uint, double>();
Dictionary<uint, double> pidRampTargets = new Dictionary<uint, double>();
Dictionary<uint, bool> pidRampEnabled = new Dictionary<uint, bool>();

string loggerStatus = string.Empty;
bool loggerActive = false;

// ---------------------------------------------------------------------
// SigCore state cache
// ---------------------------------------------------------------------

core.DigitalInChanged += values => {
    lock (stateLock) {
        digitalInputs = (bool[])values.Clone();
    }
};

core.AnalogInChanged += values => {
    lock (stateLock) {
        analogInputs = (double[])values.Clone();
    }
};

core.RelayChanged += (channel, state) => {
    lock (stateLock) {
        EnsureLength(ref relays, (int)channel + 1);
        relays[channel] = state;
    }
};

core.AnalogOutChanged += (channel, value, auto) => {
    lock (stateLock) {
        EnsureLength(ref analogOutputs, (int)channel + 1);
        EnsureLength(ref analogOutputAuto, (int)channel + 1);

        analogOutputs[channel] = value;
        analogOutputAuto[channel] = auto;
    }
};

core.PIDSPChanged += (channel, value) => {
    lock (stateLock) {
        pidSetPoints[channel] = value;
    }
};

core.PIDPVChanged += (channel, value) => {
    lock (stateLock) {
        pidProcessValues[channel] = value;
    }
};

core.PIDOutputChanged += (channel, value) => {
    lock (stateLock) {
        pidOutputs[channel] = value;
    }
};

core.PIDTolChanged += (channel, value) => {
    lock (stateLock) {
        pidTolerances[channel] = value;
    }
};

core.PIDDevChanged += (channel, value) => {
    lock (stateLock) {
        pidDeviations[channel] = value;
    }
};

core.PIDAutoChanged += (channel, value) => {
    lock (stateLock) {
        pidAuto[channel] = value;
    }
};

core.PIDRampTimeChanged += (channel, value) => {
    lock (stateLock) {
        pidRampTimes[channel] = value;
    }
};

core.PIDRampTargetChanged += (channel, value) => {
    lock (stateLock) {
        pidRampTargets[channel] = value;
    }
};

core.PIDRampChanged += (channel, value) => {
    lock (stateLock) {
        pidRampEnabled[channel] = value;
    }
};

core.LoggerStatusChanged += (status, logging) => {
    lock (stateLock) {
        loggerStatus = status;
        loggerActive = logging;
    }
};

core.ConnectionStateChanged += state => {
    Console.WriteLine($"[Bridge] SigCore connection state: {state}");
};

core.ConnectionWarning += warning => {
    Console.WriteLine($"[Bridge] SigCore warning: {warning}");
};

// ---------------------------------------------------------------------
// Maintain the local Core connection.
//
// StartAsync owns connect, handshake, subscription, and reconnect.
// ---------------------------------------------------------------------

_ = Task.Run(async () => {
    try {
        await core.StartAsync("127.0.0.1", 7020);
        Console.WriteLine("[Bridge] Connected to SigCore Core Server.");
    } catch (Exception ex) {
        Console.WriteLine("[Bridge] SigCore connection failed: " + ex.Message);
    }
});

// ---------------------------------------------------------------------
// Private HTTP data service
//
// This process is intentionally bound to loopback only.
// Apache/PHP is the public web layer.
// WebBridge serves SigCore data only.
// ---------------------------------------------------------------------

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
WebApplication app = builder.Build();

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

        lock (stateLock) {
            state = new JObject {
                ["connection"] = new JObject {
                    ["state"] = core.ConnectionState.ToString(),
                    ["connected"] = core.ConnectionState == SigCoreConnectionState.Connected
                },
                ["digitalInputs"] = ToBoolJArray(digitalInputs),
                ["analogInputs"] = ToDoubleJArray(analogInputs),
                ["relays"] = ToBoolJArray(relays),
                ["analogOutputs"] = new JObject {
                    ["values"] = ToDoubleJArray(analogOutputs),
                    ["auto"] = ToBoolJArray(analogOutputAuto)
                },
                ["pid"] = new JObject {
                    ["setPoints"] = ToDoubleJObject(pidSetPoints),
                    ["processValues"] = ToDoubleJObject(pidProcessValues),
                    ["outputs"] = ToDoubleJObject(pidOutputs),
                    ["tolerances"] = ToDoubleJObject(pidTolerances),
                    ["deviations"] = ToDoubleJObject(pidDeviations),
                    ["auto"] = ToBoolJObject(pidAuto),
                    ["rampTimes"] = ToDoubleJObject(pidRampTimes),
                    ["rampTargets"] = ToDoubleJObject(pidRampTargets),
                    ["rampEnabled"] = ToBoolJObject(pidRampEnabled)
                },
                ["logger"] = new JObject {
                    ["status"] = loggerStatus,
                    ["logging"] = loggerActive
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

// Read-only local text log endpoint. WebBridge runs in the logger's mount namespace.
// Only Apache/PHP can reach the loopback listener at 127.0.0.1:8080.
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

app.Lifetime.ApplicationStopping.Register(() => {
    Console.WriteLine("[Bridge] Stopping...");
    core.Stop();
    core.Dispose();
});

Console.WriteLine("[Bridge] Private data service listening on 127.0.0.1:8080");
app.Run("http://127.0.0.1:8080");

static void EnsureLength<T>(ref T[] array, int length) {
    if (array.Length >= length)
        return;

    Array.Resize(ref array, length);
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

