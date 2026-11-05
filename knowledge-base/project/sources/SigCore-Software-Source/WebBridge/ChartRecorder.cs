using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SigCoreCommon;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

// WebBridge-only recorder. Core's wire protocol and persisted configuration are untouched.
internal sealed class ChartRecorder : IDisposable {
    private const int Channels = 4;
    private const int SegmentSize = 4096;
    private const int SampleBytes = 40; // UTC ticks (Int64) + 4 x Double
    private const int MaxRequestedSamples = 10000;
    private static readonly string ConfigPath =
        Environment.GetEnvironmentVariable("SIGCORE_WEBBRIDGE_CHART_CONFIG") ??
        (OperatingSystem.IsWindows() ? Path.Combine(AppContext.BaseDirectory, "webbridge-chart.json") : "/opt/sigcore/webbridge-chart.json");

    private readonly CoreState state;
    private readonly object gate = new();
    private JObject config;
    private List<ChartSample[]> segments = new();
    private long capacity;
    private long count;
    private long head;
    private long generation;
    private CancellationTokenSource? samplerCts;
    private Task? samplerTask;

    private readonly struct ChartSample {
        public readonly long Ticks;
        public readonly double V0, V1, V2, V3;
        public ChartSample(long ticks, double[] values) {
            Ticks = ticks;
            V0 = values[0]; V1 = values[1]; V2 = values[2]; V3 = values[3];
        }
        public double Value(int index) => index switch {
            0 => V0, 1 => V1, 2 => V2, _ => V3
        };
    }

    public ChartRecorder(CoreState state) {
        this.state = state;
        config = ReadSettings();
        capacity = CalcCapacity(config);
        StartSampler();
    }

    private static JObject Defaults() => new() {
        ["recordIntervalSec"] = 1.0,
        ["maxMemoryMiB"] = 1024,
        ["display"] = new JObject {
            ["windowSeconds"] = 3600,
            ["traces"] = new JArray(true, true, true, true),
            ["colors"] = new JArray("#2563eb", "#dc2626", "#16a34a", "#a855f7"),
            ["autoScale"] = true,
            ["min"] = 0.0,
            ["max"] = 100.0
        }
    };

    private JObject ReadSettings() {
        var defaults = Defaults();
        try {
            if (!File.Exists(ConfigPath)) return defaults;
            JObject saved = JObject.Parse(File.ReadAllText(ConfigPath));
            Merge(defaults, saved);
            Validate(defaults);
            return defaults;
        } catch (Exception ex) {
            state.log.Error("Chart", "LoadConfigFailed", ex.ToString(), "ChartRecorder.cs");
            return Defaults();
        }
    }

    private static void Merge(JObject target, JObject changes) {
        foreach (var property in changes.Properties()) {
            if (!target.ContainsKey(property.Name)) throw new ArgumentException("Unsupported chart setting: " + property.Name);
            if (property.Name == "display") {
                if (property.Value is not JObject display) throw new ArgumentException("display must be an object.");
                var current = (JObject)target["display"]!;
                foreach (var p in display.Properties()) {
                    if (!current.ContainsKey(p.Name)) throw new ArgumentException("Unsupported chart display setting: " + p.Name);
                    current[p.Name] = p.Value.DeepClone();
                }
            } else target[property.Name] = property.Value.DeepClone();
        }
    }

    private static void Validate(JObject value) {
        double interval = value.Value<double?>("recordIntervalSec") ?? double.NaN;
        if (!double.IsFinite(interval) || interval < 0.1 || interval > 60)
            throw new ArgumentException("recordIntervalSec must be from 0.1 to 60 seconds.");
        if (value["maxMemoryMiB"]?.Type != JTokenType.Integer)
            throw new ArgumentException("maxMemoryMiB must be an integer.");
        int mib = value.Value<int>("maxMemoryMiB");
        if (mib < 16 || mib > 1024)
            throw new ArgumentException("maxMemoryMiB must be from 16 to 1024.");
        JObject display = (JObject)value["display"]!;
        double window = display.Value<double?>("windowSeconds") ?? double.NaN;
        if (!double.IsFinite(window) || window < 1 || window > 10 * 365 * 24 * 3600.0)
            throw new ArgumentException("Invalid display windowSeconds.");
        foreach (var field in new[]{"traces", "colors"}) {
            if (display[field] is not JArray array || array.Count != Channels)
                throw new ArgumentException(field + " must contain four entries.");
        }
        foreach (JToken trace in (JArray)display["traces"]!)
            if (trace.Type != JTokenType.Boolean) throw new ArgumentException("traces must contain booleans.");
        foreach (JToken color in (JArray)display["colors"]!) {
            string? s = color.Type == JTokenType.String ? color.Value<string>() : null;
            if (s == null || s.Length != 7 || s[0] != '#' || !int.TryParse(s.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
                throw new ArgumentException("colors must be #RRGGBB values.");
        }
        if (display["autoScale"]?.Type != JTokenType.Boolean)
            throw new ArgumentException("autoScale must be boolean.");
        double min = display.Value<double?>("min") ?? double.NaN;
        double max = display.Value<double?>("max") ?? double.NaN;
        if (!double.IsFinite(min) || !double.IsFinite(max) || min >= max)
            throw new ArgumentException("Invalid minimum/maximum scale.");
    }

    private static long CalcCapacity(JObject value) =>
        ((long)value.Value<int>("maxMemoryMiB") * 1024 * 1024) / SampleBytes;

    public JObject GetConfiguration() {
        lock (gate) {
            var obj = (JObject)config.DeepClone();
            obj["recordedSamples"] = count;
            obj["maxSamples"] = capacity;
            return obj;
        }
    }

    public JObject UpdateConfiguration(JObject changes) {
        lock (gate) {
            var next = (JObject)config.DeepClone();
            Merge(next, changes);
            Validate(next);
            bool reset = next.Value<double>("recordIntervalSec") != config.Value<double>("recordIntervalSec") ||
                         next.Value<int>("maxMemoryMiB") != config.Value<int>("maxMemoryMiB");
            SaveSettings(next);  // Do not change runtime state when persistence fails.
            config = next;
            if (reset) {
                generation++;
                samplerCts?.Cancel();
                segments = new List<ChartSample[]>();
                count = 0; head = 0;
                capacity = CalcCapacity(config);
                StartSampler();
            }
            return GetConfiguration();
        }
    }

    private static void SaveSettings(JObject value) {
        string? directory = Path.GetDirectoryName(ConfigPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        string temp = ConfigPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try {
            File.WriteAllText(temp, value.ToString(Formatting.Indented) + "\n");
            File.Move(temp, ConfigPath, true);
        } finally {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    private void StartSampler() {
        var cts = new CancellationTokenSource();
        samplerCts = cts;
        long currentGeneration = generation;
        double interval = config.Value<double>("recordIntervalSec");
        samplerTask = Task.Run(async () => {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(interval));
            try {
                while (await timer.WaitForNextTickAsync(cts.Token)) {
                    if (state.core.ConnectionState != SigCoreConnectionState.Connected) continue;
                    double[] snapshot;
                    lock (state.stateLock) {
                        if (state.analogInputs.Length < Channels) continue;
                        snapshot = new double[Channels];
                        Array.Copy(state.analogInputs, snapshot, Channels);
                    }
                    lock (gate) {
                        if (generation == currentGeneration)
                            Append(new ChartSample(Math.Max(DateTime.UtcNow.Ticks, count == 0 ? 0 : At(count - 1).Ticks + 1), snapshot));
                    }
                }
            } catch (OperationCanceledException) when (cts.IsCancellationRequested) { }
              catch (Exception ex) {
                state.log.Error("Chart", "RecordingFailed", ex.ToString(), "ChartRecorder.cs");
              }
            finally { cts.Dispose(); }
        });
    }

    // Caller holds gate. Allocates 4096-record segments as needed, not 1 GiB at startup.
    private void Append(ChartSample sample) {
        long slot = count < capacity ? (head + count) % capacity : head;
        long segmentIndex = slot / SegmentSize;
        while (segments.Count <= segmentIndex)
            segments.Add(new ChartSample[SegmentSize]);
        segments[(int)segmentIndex][(int)(slot % SegmentSize)] = sample;
        if (count < capacity) count++;
        else head = (head + 1) % capacity;
    }

    private ChartSample At(long logicalIndex) {
        long slot = (head + logicalIndex) % capacity;
        return segments[(int)(slot / SegmentSize)][(int)(slot % SegmentSize)];
    }

    // First sample with timestamp >= ticks.
    private long LowerBound(long ticks) {
        long low = 0, high = count;
        while (low < high) {
            long mid = low + (high - low) / 2;
            if (At(mid).Ticks < ticks) low = mid + 1;
            else high = mid;
        }
        return low;
    }

    private static double? JsonNumber(double v) => double.IsFinite(v) ? v : null;

    // One request serves initial load, historical navigation, and live-window advances.
    // start and length are in UTC Unix milliseconds. End of interval is exclusive.
    public JObject Query(long startMs, long lengthMs, int samples) {
        if (samples < 1 || samples > MaxRequestedSamples) throw new ArgumentException("samples must be 1..10000.");
        if (lengthMs <= 0 || lengthMs > (long)TimeSpan.FromDays(3650).TotalMilliseconds)
            throw new ArgumentException("Invalid length (milliseconds).");
        DateTime start;
        try {
            start = DateTimeOffset.FromUnixTimeMilliseconds(startMs).UtcDateTime;
            _ = DateTimeOffset.FromUnixTimeMilliseconds(checked(startMs + lengthMs));
        } catch (ArgumentOutOfRangeException) { throw new ArgumentException("Invalid start or length."); }
          catch (OverflowException) { throw new ArgumentException("Invalid start or length."); }
        var timestamps = new JArray();
        JArray[] channels = {new(),new(),new(),new()};
        lock (gate) {
            // Timestamp positioning calculated in double for sub-millisecond intervals.
            double step = (double)lengthMs / samples;
            for (int i = 0; i < samples; i++) {
                double tMs = startMs + step * i;
                double rightMs = startMs + step * (i + 1);
                timestamps.Add(tMs / 1000.0); // uPlot timestamp in Unix seconds.
                long tTicks = start.Ticks + (long)Math.Round(step * i * TimeSpan.TicksPerMillisecond);
                long rightTicks = start.Ticks + (long)Math.Round(step * (i + 1) * TimeSpan.TicksPerMillisecond);
                long first = LowerBound(tTicks);
                long after = LowerBound(rightTicks);

                if (after > first) {
                    // Use the last reading in each populated output interval.
                    ChartSample latest = At(after - 1);
                    for (int c = 0; c < Channels; c++) channels[c].Add(JsonNumber(latest.Value(c)));
                } else if (first > 0 && first < count) {
                    // Sample time lies between recorded points: interpolate only when both exist.
                    ChartSample prev = At(first - 1), next = At(first);
                    double span = (double)(next.Ticks - prev.Ticks);
                    double fraction = span <= 0 ? double.NaN : (tTicks - prev.Ticks) / span;
                    for (int c = 0; c < Channels; c++) {
                        double a = prev.Value(c), b = next.Value(c);
                        channels[c].Add(JsonNumber(a + (b - a) * fraction));
                    }
                } else {
                    // Missing either neighbor. JSON null represents NaN in browser.
                    for (int c = 0; c < Channels; c++) channels[c].Add(JValue.CreateNull());
                }
            }
            return new JObject {
                ["start"] = startMs,
                ["length"] = lengthMs,
                ["samples"] = samples,
                ["intervalMs"] = (double)lengthMs / samples,
                ["firstRecordedMs"] = count == 0 ? JValue.CreateNull() : new JValue(new DateTimeOffset(At(0).Ticks, TimeSpan.Zero).ToUnixTimeMilliseconds()),
                ["lastRecordedMs"] = count == 0 ? JValue.CreateNull() : new JValue(new DateTimeOffset(At(count - 1).Ticks, TimeSpan.Zero).ToUnixTimeMilliseconds()),
                ["data"] = new JArray(timestamps, channels[0], channels[1], channels[2], channels[3])
            };
        }
    }

    public void Dispose() {
        lock (gate) {
            generation++;
            samplerCts?.Cancel();
        }
    }
}
