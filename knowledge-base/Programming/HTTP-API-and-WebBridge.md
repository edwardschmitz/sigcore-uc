---
title: "HTTP API and WebBridge"
status: source-draft-review-required
hardware_revisions: "Rev B"
software_versions: "Supplied unversioned WebBridge, PHP and device-site snapshot; verify deployed release"
source_review_date: 2026-11-06
customer_release_approved: false
---

# HTTP API and WebBridge

> **Engineering source draft, not a promise of public or authenticated remote API service.** The on-device website provides HTTP entry points via Apache/PHP. PHP forwards requests to WebBridge at `http://127.0.0.1:8080`. WebBridge is **loopback only**; do not instruct remote users to call `device-ip:8080`. The separate native Core API uses **TCP 7020** and is not an HTTP service. See [Device Website](../User-Interfaces/Device-Website.md), [.NET API](DotNet-API.md), and [Status and Diagnostics](../User-Interfaces/Status-and-Diagnostics.md).

## 1. Request path and channel addressing

`HTTP client -> Apache (typically port 80) -> /api/*.php -> _bridge.php -> 127.0.0.1:8080/api/* -> SigCoreSystem -> Core TCP 7020 -> hardware`.

The public-facing PHP routes and WebBridge's internal route paths **differ**; the table below identifies both. The supplied snapshot's installed Apache configuration and public access restrictions are not independently verified. **Never presume that HTTP is encrypted or that exposed controls are authenticated.** Use only an operator-approved local network/installation, and keep privileged endpoints isolated until deployment security has been verified.

Channels are **zero-based**: `relay`/`di` `0..7`; `ai`/`ao`/`pid` `0..3`. Returned `connection.connected=true` reflects the bridge's managed protocol connection, not a guarantee that all I/O readings are current or external loads responded.

## 2. Read endpoints

| Public PHP URL | Method | Internal WebBridge URL | Semantics |
|---|---|---|---|
| `/api/connection.php` | GET | `/api/connection` | `{ "state": "...", "connected": true/false }` |
| `/api/state.php` | GET | `/api/state` | Cached DI, AI, relays, AO, PID, logger state; includes connection state |
| `/api/status.php` | GET | `/api/status` | Fresh request to Core; `503` when not connected |
| `/api/network.php` | GET | `/api/network` | Sanitized network summary, without Wi-Fi PSK |
| `/api/config.php?kind=relay&channel=0` | GET | `/api/config/relay/0` | One full channel config (`kind` = relay, di, ai, ao, pid) |
| `/api/device-settings.php?kind=system` | GET | `/api/device-settings/system` | System/network/logger settings; **may include sensitive fields** |
| `/api/chart-history.php?start=...&length=...&samples=...` | GET | `/api/chart/history?...` | Time-window resampled four-AI history |
| `/api/chart-config.php` | GET | `/api/chart/config` | Recorder/display settings, sample counts and capacities |
| `/api/config-backup.php` | GET | `/api/config-backup` | Download non-global/non-network configuration snapshot; treat as sensitive |

`/api/state.php` is **an in-memory cache** in WebBridge, populated by Core alerts and initial reads. When Core disconnects, cached values may remain present. Treat `connection.connected=false` as stale values, not a valid measurement. Output states are Core logical state, not actual relay contact sensing.

An illustrative (not live) state object contains `digitalInputs` and `analogInputs` arrays, `relays` boolean array, `analogOutputs:{values,auto}` arrays, `pid:{setPoints,processValues,outputs,tolerances,deviations,auto,rampTimes,rampTargets,rampEnabled}` with channel-index **string keys**, and `logger:{status,logging}`.

## 3. Output control: POST

| Public PHP URL | Internal endpoint | Required JSON | Response meaning |
|---|---|---|---|
| `/api/control.php?operation=relay` | `POST /api/control/relay` | `{"channel":0,"state":true}` | `accepted:true`, `confirmed:false` |
| `/api/control.php?operation=analog-output` | `POST /api/control/analog-output` | `{"channel":0,"value":2.5}` | `accepted:true`, `confirmed:false` |
| `/api/control.php?operation=pid&channel=0&action=setpoint` | `POST /api/control/pid/0/setpoint` | `{"value":10.0}` | `accepted:true`, `confirmed:false` |

PID actions: `setpoint`, `output`, `tolerance`, `auto`, `ramp-time`, `ramp-target`, `ramp`, `reset`. Actions `auto` and `ramp` require a JSON boolean `value`; numeric operations require a finite number; `reset` does not read a value but the endpoint still parses a JSON object, so send `{}`. The PHP route validates the action and PID channel before forwarding.

**Read-only polling example:**

```bash
curl --fail-with-body --max-time 5 'http://DEVICE-ADDRESS/api/state.php'
```

**Write request form, for an operator-authorized test system only:**

```bash
curl --fail-with-body -X POST 'http://DEVICE-ADDRESS/api/control.php?operation=relay' \
  -H 'Content-Type: application/json' \
  --data '{"channel":0,"state":false}'
```

This command can **change a physical relay**. Do not run it against equipment without establishing the safe state, active process, output wiring, and ownership. The reply's `accepted:true` only means the bridge transmitted the command, **not** that the physical relay moved.

## 4. Configuration reads and writes

`GET|PUT /api/config.php?kind=<relay|di|ai|ao|pid>&channel=<index>` maps to the corresponding WebBridge route. GET yields a full JSON configuration payload. PUT requires a request of the shape:

```json
{"config":{"name":"Pump Ready","defaultState":false,"failSafeState":false,"invertedLogic":false}}
```

The example is for a **relay** channel. The URL identifies its channel; any `channel` field inside `config` is ignored/removed. Unlike a JSON PATCH, the reviewed implementation **requires every allowed field**, not just changed ones. For relays those fields are `name`, `defaultState`, `failSafeState`, `invertedLogic`; other types have different sets. Read the existing configuration first, retain fields, modify intended values, then submit the complete set and read back. Do not interpret accepted writes as validated hardware behavior. The bridge delegates final configuration interpretation to Core; some local field checks validate only presence/null/nonfinite numbers rather than full semantic ranges.

For **device settings**: `/api/device-settings.php?kind=system|network|logger` supports GET and PUT, and PUT bodies are *partial changes* under that selected kind. This differs from the per-channel `/api/config.php` PUT shape. System keys include `systemName`, `analogSamplingRate`, `pwm_frequency`. Network accepts `hostname`, and `ethernet`/`wifi` nested updates. Logger includes `enabled`, `intervalSec`, `server`, `port`, `database`, `user`, `password`, `tableName` and timeouts. Saving network configuration may interrupt connectivity; these are not ordinary telemetry endpoints.

**Sensitive-data finding:** In the reviewed source `DeviceSettingsEndpoints.PublicNetwork` returns a deep copy of the full network config and `PublicLogger` returns the full logger config, **including the stored Wi-Fi PSK and database password when present**. The PHP proxy exposes those same endpoints unless additional access controls are installed. Treat `/api/device-settings.php` as a sensitive administrative interface, never log/redistribute its raw response, and do not assert remote access is secure without checking the deployed protections.

## 5. Chart Recorder

`GET /api/chart-history.php?start=UNIX_MS&length=DURATION_MS&samples=N` accepts start and duration in **milliseconds** and `1..10000` samples. The response has `start`, `length`, `samples`, `intervalMs`, `firstRecordedMs`, `lastRecordedMs`, and `data` containing five parallel arrays: `[timestampsUnixSeconds, AI1, AI2, AI3, AI4]`. Timestamps in `data[0]` are **Unix seconds**, not milliseconds. Missing or nonfinite data is serialized as JSON `null`, not the string `NaN`.

For each requested bucket the recorder uses the **last sample inside that bucket**, if one exists. If no raw sample falls in the bucket but both bounding readings are available, it interpolates. If a neighbor is unavailable, it emits `null`. This is a display-oriented resampling contract and is **not** a lossless raw-sample export. The recorder's history resides in WebBridge process RAM and is lost on its restart.

`GET|PUT /api/chart-config.php` maps to `/api/chart/config`. Config fields include `recordIntervalSec` (0.1..60), `maxMemoryMiB` (integer 16..1024) and `display` preferences (`windowSeconds`, four `traces`, four hex `colors`, `autoScale`, `min`, `max`). The **default recording interval is 1 second** in the supplied implementation. Changing recording interval or allocated memory **clears history** and restarts sampling. Changing display preferences does not itself clear stored samples.

## 6. Backup, restore, reboot and log-related functions

- `GET /api/config-backup.php` downloads configuration excluding `global` and `network`; this may still include **logger database credentials**. The filename based on the unit serial is a suggested download filename, not proof the backup excludes secrets.
- `POST /api/config-restore.php` uploads a JSON backup, enforces size/layout constraints, atomically replaces the properties file and queues Launcher restart; successful response is `{ "accepted": true, "restarting": true }`, but process restart can still fail afterward.
- `POST /api/restart.php` requests an OS reboot through WebBridge; it depends on privileged installed service permissions. Expect the website to become unreachable during restart.
- Core and WebBridge also expose internal log/status functions, but **the supplied PHP `api/` directory does not contain a public `/api/logs.php` proxy**; don't invent a public endpoint from the internal `/api/logs` route.

## 7. HTTP status/errors and request limits

The bridge's standard helpers return **400** for invalid argument/JSON, **503** if Core is disconnected, and **502** when a Core operation fails. Some routes return **404**, **500**, or custom plain text on failure; check both HTTP status and JSON `error` where present, rather than assuming every error has the same schema. Standard write/config JSON body size is capped at **16 KB**; backup restore has a separate **1 MB** limit. PHP proxies use finite upstream timeouts, so a gateway **502** can indicate WebBridge timeout rather than proof Core rejected an action. If a write times out, determine state before retrying to avoid duplicate side effects.

## Evidence / scope

WebBridge `Program.cs`, `Endpoints/*.cs`, `ChartRecorder.cs`, `CoreState.cs`; device site `UpdaterFiles/www/api/*.php`, especially `_bridge.php`, `control.php`, `config.php`, `device-settings.php`, and `chart-history.php`. All routes and behaviors require validation against the **deployed** software. The PHP API is documented here as an implementation interface, not an unconditional public-Internet integration promise.
