---
title: "Logging and History"
status: source-draft-review-required
hardware_revisions: "Rev B runtime context"
software_versions: "Supplied source snapshot; exact commit/release unknown"
source_review_date: 2026-11-06
customer_release_approved: false
---

# Logging and History

> **Engineering draft.** SigCore UC has more than one use of the word *log*: structured application/event logs, SQL measurement snapshots, and the Chart Recorder's volatile time-series history. They have different owners, storage and persistence guarantees. The supplied source implements MySQL snapshot logging **inside Core**, while recent product plans describe a separate logger process. Do not assert one deployed topology without confirming the release.

## 1. Three independent data paths

| Feature | Owner in reviewed source | Data | Lifetime and storage |
|---|---|---|---|
| Application/event logs | `SigCoreCommon.Log` and process logging | Software events, warnings, error details, statuses | Process logging infrastructure; not the same format as SQL samples or the chart buffer |
| MySQL measurement logger | `HardwareManager.LoggerLoop` + `MYSQL_LOGGER` | Snapshot dictionary for AI, AO, DI, relay, PID | External MySQL tables `config_current` and `data_log` in reviewed implementation |
| Chart Recorder | `WebBridge.ChartRecorder` | UTC timestamp plus four latest analog readings | WebBridge memory only; configuration stored separately in JSON |

A value appearing in the live interface does **not** imply that SQL logging is enabled, that an SQL row was written, or that the chart has retained that time period.

## 2. MySQL snapshot model (source snapshot)

When logging is enabled, `HardwareManager.StartLoggerLoop()` attempts to write a configuration header and then periodically runs `GetSnapshot() → LogSnapshotAsync(snapshot)`. The snapshot contains:

- `AI0`–`AI3`: calibrated analog-input values;
- `AO0`–`AO3` and `AO<n>_AUTO` values: software values/mode information;
- `DI0`–`DI7`: debounced logical digital-input states encoded as 0/1;
- `RELAY0`–`RELAY7`: commanded logical relay states encoded as 0/1;
- `PID0_SP`, `PID0_PV`, `PID0_OUT`, `PID0_AUTO`, `PID0_DEV`, `PID0_TOL`, and analogous fields through PID3.

The logger uses a UTC timestamp per snapshot, then expands its dictionary into rows containing `timestamp`, `config_id`, `channel_name`, and `value` in `data_log`. The config header is inserted into `config_current` as JSON so a record can be associated with the captured configuration. These exact table names and schema must be checked against the **current distributable SQL schema**. The logger currently substitutes `0.0` for NaN when composing database parameters; an SQL zero therefore should **not** automatically be interpreted as a genuinely measured zero in error cases.

The configured logger `intervalSec` defaults to `1.0`. In the reviewed Core loop, logging work is performed and *then* it waits the configured interval; this is not a synchronized interval timer. A database failure updates logger status and execution continues, but no persistent offline-queue/guaranteed later delivery is demonstrated.

### 2.1 Configuration and security issue

`LoggerConfigPayload` exposes `enabled`, `intervalSec`, `server`, `port`, `database`, `user`, `password`, `tableName`, `schemaVersion`, connection timeout, command timeout and status. **The reviewed `MYSQL_LOGGER.cs` also contains hard-coded database connection credentials in its SQL connection paths**, meaning the configurable connection fields are **not reliably used for the active connection** in this snapshot. This is a **high-priority security/functional defect** requiring correction and deployed-release verification. The credentials are intentionally not reproduced here.

A separate maintenance concern: the WebBridge configuration backup excludes global/network but includes the logger section. Backup JSON **can carry a database password**. Handle such files as confidential and avoid placing them in public support examples or untrusted upload locations.

The screenshot from the supplied reference contains a real-looking database host/user and is intentionally **not embedded** in this KB draft pending creation of a sanitized product example.

## 3. Chart Recorder architecture

WebBridge owns a single circular history for analog channels `AI1`–`AI4` as cached from Core. The recorder takes one **periodic snapshot of all four cached values**, storing a UTC timestamp and four doubles. Its `ChartSample` uses **40 bytes per record** before collection/object overhead: an 8-byte timestamp plus four 8-byte floating-point values.

The chart's configuration file on Linux is `/opt/sigcore/webbridge-chart.json`; it does not contain historical sample records. A default maximum allocation of **1024 MiB** gives approximately **26.84 million records** (around **311 days** at an uninterrupted one-second recording interval), calculated from 40 bytes/record and excluding runtime overhead, missed samples and real-world memory contention. This is a **source-derived capacity estimate**, not a verified product retention guarantee. The maximum allocation field can be configured from **16 to 1024 MiB** in the reviewed code. The buffer grows in segments of 4096 records; once full, newer records overwrite the oldest.

**Chart settings in source:**

| Setting | Default | Validated range / shape |
|---|---|---|
| `recordIntervalSec` | `1.0` | `0.1..60` seconds |
| `maxMemoryMiB` | `1024` | Integer `16..1024` |
| `display.windowSeconds` | `3600` | `1` to 10 years in seconds per code limit |
| `display.traces` | Four enabled | Array of four Boolean values |
| `display.colors` | Four color strings | Four `#RRGGBB` strings |
| `display.autoScale` | `true` | Boolean |
| `display.min`, `display.max` | `0`, `100` | Finite numbers; min strictly less than max |

Changing recording interval or memory capacity **clears the sample buffer** and restarts its sampler. Changing display settings alone does not trigger that reset. Restarting WebBridge, killing its process or power cycling the unit loses the recorded **chart history**, although saved display/recording settings may be reloaded.

## 4. History request and resampling

The private WebBridge endpoints (Apache/PHP public-layer routing handled elsewhere) are:

- `GET /api/chart/history?start=<unix-ms>&length=<ms>&samples=<n>`
- `GET /api/chart/config`
- `PUT /api/chart/config` (JSON configuration update)

The history endpoint expects **start and duration in UTC Unix milliseconds** and `samples` from **1 through 10,000**, with positive duration up to 3650 days. Response fields include `start`, `length`, `samples`, `intervalMs`, `firstRecordedMs`, `lastRecordedMs`, and `data`.

`data` is a five-array structure: `[timestamps, AI1, AI2, AI3, AI4]`; timestamps are **Unix seconds** appropriate for uPlot, while request `start` and `length` are **milliseconds**. Each output interval has an evenly spaced start time. For each channel:

1. If original records fall in the output bucket `[t, next-t)`, return the **last recorded value within that bucket**.
2. Otherwise, if recorded points bracket the requested sample instant, **linearly interpolate** between the previous and next readings.
3. If either bracketing neighbor is unavailable, return JSON **`null`** (representing missing/NaN in the browser).

This method is explicitly a **resampled history display**, not raw sample export, an unbiased time-window average, or a guarantee of preserving brief peaks. A request for fewer output samples than raw measurements can drop transient extrema. A missing interval can interpolate across a gap when both adjacent records exist; consumers must avoid assuming every displayed point was measured at that exact time.

## 5. Recording behavior and network boundaries

The `ChartRecorder` samples only when WebBridge's managed Core connection is `Connected` and a four-element analog-input cache exists. The Core cache is updated through subscriptions; a one-second recorder interval does not cause a one-second new ADC hardware conversion. Chart history is not stored in Core and is not recovered by reconnecting a browser after WebBridge itself restarts.

The browser chart is designed to keep the window stationary until a new reading exceeds the right edge, then advance the viewport by 10% of its length; **Go Live** positions the present time at 90% of the viewport (right edge 10% into the future). These are UI behavior requirements from the project specification; inspect deployed chart frontend before treating them as implemented and tested.

## 6. Source evidence and unresolved engineering items

- `libs/SigCoreCommon/MYSQL_LOGGER.cs` and `HardwareManager.cs` (`GetSnapshot`, `StartLoggerLoop`, `LoggerLoop`, `SetLoggerConfig`).
- `libs/SigCoreCommon/Payloads/LoggerConfigPayload.cs`; `libs/SigCoreCommon/Log.cs`.
- `WebBridge/ChartRecorder.cs`, `WebBridge/CoreState.cs`, `WebBridge/Endpoints/ChartEndpoints.cs`, `WebBridge/Endpoints/BackupRestoreEndpoints.cs`.

**Priority checks:** remove and rotate any real credentials embedded in source, ensure logger connections use configuration, reconcile separate-process logger deployment, validate packaged SQL schema, prevent sensitive backup disclosure, verify chart memory-pressure behavior and user-visible history semantics.
