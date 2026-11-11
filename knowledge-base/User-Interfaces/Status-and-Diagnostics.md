---
title: "Status and Diagnostics"
status: source-draft-review-required
hardware_revisions: "Rev B context"
software_versions: "Supplied unversioned Core, WebBridge and UI source snapshot"
source_review_date: 2026-11-06
customer_release_approved: false
---

# Status and Diagnostics

> **Engineering source draft.** Distinguish the identity of the source, its freshness, and the kind of evidence returned. A responsive website or TCP session is not a proof of hardware/control-loop health; a commanded logical output is not a physical contact readback. Status is often a snapshot, not a continuous test.

## 1. Three different status paths

| Diagnostic path | Access | Source, meaning and limitations |
|---|---|---|
| Core status protocol | Control Panel `GetStatusAsync`, .NET client over TCP **7020** | Fresh Core request constructs a JSON object with a `sections` array plus machine-readable `update`; built by `CommandDispatcher.HandleGetStatus` and the `HardwareManager`/`OsManager` providers |
| WebBridge connection and cached state | Browser PHP `/api/connection.php` and `/api/state.php` -> WebBridge `127.0.0.1:8080` | Connection state and most recently subscribed I/O/PID/logger values; cache may be stale or incomplete while Core disconnected |
| WebBridge status relay | Browser PHP `/api/status.php` -> WebBridge `/api/status` -> Core | Live Core status request; when Core is not connected returns HTTP **503**, and when Core request fails it returns **502** |

The Dashboard generally polls status and connection every 5 seconds when visible. The Control page polls cached I/O more frequently. The Windows Control Panel uses Core notifications and status requests directly. These are *different update cadences and sources*; the operator should not assume all screens show one atomic synchronized snapshot.

## 2. Core status structure and interpretation

`Server/SigCoreServer/CommandDispatcher.cs` creates a `JObject`, initializes `status["sections"]` as an array, and requests system, device/OS and Wi-Fi sections. It adds a separate `update` object from `HardwareManager.GetUpdateConfig()`. The `sections` entries contain a `section` label and ordered `items` with `label` and `data` values. Field labels and formatting are application-defined; client code normalizes names defensively rather than depending on one schema revision.

The OS status builder in `Server/SigCoreServer/OsManager.cs` collects the system hostname, kernel, uptime, CPU model/revision/serial, CPU temperature, memory, filesystem usage and CPU load. Examples of implementation details that affect interpretation:

- CPU temperature is rendered in **°C** and may be missing if the OS sensor read fails.
- Memory usage uses Linux `/proc/meminfo` **MemAvailable**, not merely MemFree, to calculate used/available memory. On error the field may say `Unknown`.
- Disk usage in the supplied code reads the root-filesystem usage percentage (`df -h /`), not the free bytes on every mounted volume.
- The CPU-load percentage is derived from the **15-minute load average divided by processor count**. It is not an instantaneous CPU-utilization measurement and need not equal a task-manager percentage.
- Ethernet and Wi-Fi sections may differ based on interface state and installed network configuration.

For precise maintenance decisions, retain the raw JSON and the timestamp of the observation rather than inferring that one human-readable value is a comprehensive health check.

## 3. Connection and live-value payload

WebBridge's `/api/connection` responds with a connection `state` string and boolean `connected` that is true only when `SigCoreSystem.ConnectionState` is `Connected`. Its `/api/state` returns an object of approximately this shape (illustrative keys and types, **not live readings**):

```json
{
  "connection": { "state": "Connected", "connected": true },
  "digitalInputs": [false, false],
  "analogInputs": [0.0, 0.0],
  "relays": [false, false],
  "analogOutputs": { "values": [0.0, 0.0], "auto": [false, false] },
  "pid": {
    "setPoints": {}, "processValues": {}, "outputs": {},
    "tolerances": {}, "deviations": {}, "auto": {},
    "rampTimes": {}, "rampTargets": {}, "rampEnabled": {}
  },
  "logger": { "status": "", "logging": false }
}
```

**Important:** In actual operation arrays normally cover eight DI, four AI, eight relays and four AO. This abbreviated JSON is a schema illustration only. PID dictionaries are keyed by channel-number strings. The values are **subscription cache**, not independent physical measurement or command confirmation. When disconnected, previously received cache values may remain present; applications must inspect `connection.connected` and not act on them as fresh measurements.

## 4. Logs and event history

The device's `logviewer.php` offers `Auto (MySQL / Text)`, `MySQL`, or `Text` as log source. In automatic mode it first tries MySQL (using stored logger settings from `/opt/sigcore/properties.json`) and, when that fails, displays a notice and switches to the local text-log endpoint. In forced MySQL mode a connection failure is presented as an error instead of silently switching sources.

- SQL log viewer: selects from device logging tables, supports session/serial filters and event fields, and can reveal application events from earlier sessions if database history is retained.
- Text log: `/api/logs` returns up to the last **2 MiB** of `/tmp/sigcore-log/log.txt`, skipping a partially read leading line. A missing file returns 404; text log lifetime/rotation is not defined by the HTTP endpoint.
- Supported UI filters include severity (`INFO`, `WARN`, `ERROR`, `LOGGER-DEBUG`), application, source, category, event, message and limits **100/250/500/1000**; defaults are not guarantees that all records are still available.
- Chart Recorder trends are **analog-input samples**, not system event logs. They are stored in WebBridge RAM and do not survive that process restarting. See [Logging and History](../Core-Software/Logging-and-History.md).

Database failures can be an issue with authentication, connectivity, SQL setup, permissions or the source-snapshot bug concerning hard-coded database credentials. **Do not paste unredacted logs, `properties.json`, configuration backups or SQL connection details into public support conversations.**

## 5. Diagnostic decision sequence

1. Identify the intended device using **serial number**, hardware revision and software version from Core status when possible.
2. Check whether the OS/device website is reachable. This only establishes web/network service reachability.
3. Request `/api/connection.php` or inspect Control Panel `ConnectionState`. A failed Core connection isolates a different problem than an unreachable Apache page.
4. Request `/api/status.php` for fresh Core/OS status; inspect exact HTTP response and errors. A 503 indicates WebBridge cannot currently reach Core; a 502 means its Core status request failed in the reviewed endpoint.
5. Check `/api/state.php` and the **time/freshness** of readings. For a physically changing input, verify acquisition state and wiring rather than assuming cache freshness.
6. Check SQL/text logs with an appropriate session/time filter and distinguish logging subsystem outages from Core status failures.
7. For output problems, verify configured mode, command acknowledgment, logical state, physical relay/voltage and connected load independently. See hardware chapters.
8. For release problems inspect release disposition, active symlink and Launcher/Updater logs; `READY` is not synonymous with running, and `GOOD` probation is process survival. See [Updates and Recovery](../Core-Software/Updates-and-Recovery.md).

## 6. Status troubleshooting table

| Observation | Possible causes and next verification |
|---|---|
| Dashboard loads, status says unavailable | PHP/WebBridge/Core status request failure; check `/api/connection.php` and 502/503 handling |
| UI says Connected, some AI values are constant | Constant real inputs, stalled acquisition, channel config, expired cache; verify separately |
| Relays report ON but load has no power | Inversion, N/O or N/C wiring, supply/load or relay hardware; logical status is **not** physical feedback |
| Dashboard performance number seems inconsistent | 15-minute load average versus instant CPU load; memory based on MemAvailable; disk root usage |
| Logs empty | SQL service/permissions, text file absent, source filters, startup lifetime/retention, logger config |
| SQL source fails but text works | Database-specific problem; Core/WebBridge can still be operational |
| Update says `READY` but software version unchanged | Prepared release not activated; verify Launcher selection and current symlink |
| Status values differ between browser and desktop | Poll cadence, cache age, configuration/version mismatch; request fresh Core status and record timestamps |

## 7. Engineering verification required

- Define and implement explicit **last updated** timestamps for subscribed cache values if consumers need a freshness guarantee.
- Verify whether Core's `Ready` field and device site green/red heuristics mean anything beyond process/protocol availability; do not promise equipment health from those colors.
- Confirm status payload's exact labels/units for the shipping build; record schema compatibility boundaries.
- Verify runtime logger architecture and storage/retention paths; update after the separate logger deployment is confirmed.
- Test missing status/partial Core connections and disk/memory sensor errors on a physical Raspberry Pi.

## Evidence inventory

`Server/SigCoreServer/CommandDispatcher.cs` (`HandleGetStatus`); `Server/SigCoreServer/OsManager.cs`; `WebBridge/Endpoints/StatusEndpoints.cs`; `WebBridge/CoreState.cs`; `UpdaterFiles/www/js/dashboard.js`; `UpdaterFiles/www/js/control.js`; `UpdaterFiles/www/logviewer.php`; `ControlPanel/MainWindowVM.cs`; shared logger implementation. All are in the supplied development source snapshot.
