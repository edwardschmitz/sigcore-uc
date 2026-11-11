---
title: "Maintenance and Service"
status: source-draft-review-required
hardware_revisions: "Rev B; physical service details await verified assembly instructions"
software_versions: "Supplied unversioned updater/launcher, web and Core source snapshot"
source_review_date: 2026-11-06
customer_release_approved: false
---

# Maintenance and Service

> **Engineering draft.** This is an operational maintenance framework grounded in the available software. It does **not** authorize live electrical work, define field-service-replaceable assemblies or prescribe a warranty procedure. Use safe electrical isolation and the operator's equipment-specific maintenance instructions for any attached process.

## 1. Recommended maintenance record

Keep a device-specific history with serial and hardware revision, installed software version, location/network identity, configuration backup identification, error reports, field modifications and service dates. This is **recommended recordkeeping**, not a database automatically maintained by SigCore. Separate **product software version**, **Core connection state**, **OS uptime** and **process health**; they answer different questions.

A practical routine, whose frequency depends on the installation:

1. Check recent diagnostic events, unexpected restarts and Core/WebBridge connectivity.
2. Review current software version and whether a release is prepared (`READY`) or actually active.
3. Compare critical field signals against known independent instruments or feedback, especially where the controller is being used for sustained process operation.
4. Confirm intended input calibration, channel polarity/debounce, relay startup states, PID route/limits and network settings after approved changes.
5. Verify data storage separately: chart history is in WebBridge RAM, optional SQL logger writes depend on the configured database, and text logs are another system.
6. Make and securely store a configuration backup when the setup changes, or at an application-defined interval. Never assume the device website backup includes network/global identity.

Do not assign an arbitrary calibration interval, SD replacement period or relay contact lifetime without application-specific environmental/load data and a verified product specification.

## 2. Automatic update architecture

The reviewed updater **retrieves a signed manifest**, compares versions, downloads a signed `.scu` package, validates integrity/signature and installs a release to a staged location, which then becomes `READY`. **Launcher** chooses and runs a release on startup and supervises child processes. Its initial **30-second probation** tests process survival in the snapshot; it is not an end-to-end hardware I/O acceptance test. Launcher handles fallback if a candidate fails according to its release-manager logic. See [Updates and Recovery](../Core-Software/Updates-and-Recovery.md) for the exact dispositions and limitations.

A package reported as `DOWNLOADED` or `READY` is not necessarily **active**. Before investigating a post-update complaint, capture the *currently running* software version and the installed release status. Supplied source does not prove the current production telemetry pipeline implements every contemplated status event. Do not assert that the vendor can remotely command an update or view customer measurements, I/O states, device configuration or logs without checking the published update and privacy policies for the actual released build.

**Maintenance update checklist:** identify the active version, note process-impacting load/safety constraints, preserve configuration, allow verified installation according to the current policy, re-establish connections after restart, check release version, and perform targeted I/O/controls regression relevant to the application. Do not power off mid-write or interrupt an in-progress service restart to force an update.

## 3. Restart, shutdown and power removal

- **Device-site restart:** `POST /api/restart` sends `/usr/bin/sudo -n /usr/bin/systemctl reboot --no-block` from WebBridge and returns `{ "accepted": true }` when the OS command exits successfully. The response proves the reboot request was accepted, **not** that all child processes shut down gracefully or that reboot later completed.
- **Core client shutdown/restart commands:** `SigCoreSystem` exposes methods corresponding to server OS actions. An application connection may end as the host exits; the request path and resulting field states must be checked for the installed release.
- **Launcher process stop:** the reviewed Launcher terminates its child process trees rather than negotiating an orderly Core shutdown. This differs from OS shutdown behavior. Output safety during either path has not been proven by the source snapshot and must not rely on a software-only fail-safe assumption.
- **Physical power removal:** Raspberry Pi/SD-based storage requires an orderly shutdown policy appropriate to the process. Loss of power also invalidates any expectation that in-RAM chart history survives. Hardware outputs and external loads need independent defined power-loss behavior.

Before maintenance that can interrupt Core, establish what each controlled load should do on loss of communication or control power, then arrange **independent protection** accordingly. For a production process, a connected SCADA/browser screen is not a protective interlock.

## 4. Data and logs for service

| Source | Typical service evidence | Important limitation |
|---|---|---|
| **Device Status** (fresh Core request) | Device info, OS/network/performance/health sections | Unavailable when Core disconnected; some OS metrics have special definitions |
| **Browser live state** | Cached input/output/PID values and Core connection flag | May retain old values through a disconnection; verify freshness |
| **Text logs** | Bridge/Core events, restart or API errors | `/api/logs` serves up to the final **2 MiB** of `/tmp/sigcore-log/log.txt` in the reviewed source; file absence yields 404; no retention guarantee |
| **MySQL logger** | Process-value snapshots and configured event data | Database, schema, credentials, network and optional logger function must all work; older source snapshot has hard-coded connection credentials and inconsistent deployment evidence |
| **Chart Recorder** | Resampled plots of four analog channels | In WebBridge RAM; not durable; interpolation/bucket selection means not a lossless raw archive |

Do not transmit a full configuration export or unredacted diagnostic logs to third parties without checking for hostnames, IP addresses, credentials and other customer-specific data. The source snapshot's administrative settings route can return secret-bearing values.

## 5. Field hardware service boundaries

The enclosure contains a Raspberry Pi 4, main Rev B I/O PCB, connector groups, analog conditioning and electromechanical relay drivers. It is not established that field users may hot-plug boards, pull relays, replace the SD card while energized or alter regulator/ground wiring in normal operation.

For hardware diagnosis, first identify which boundary is failing: input wiring and signal source; signal-conditioning circuit; converter/expander; I²C communication; Core state; client connection; or external load. **Never** bridge a relay contact, short an AO to an external supply, inject an unapproved voltage into an AI/DI, or assume digital inputs are galvanically isolated. Connector pinouts, allowable current, installation torque, and power supply requirements remain subject to engineering verification; follow current approved hardware service documents once available.

A replacement SD card or clean image is a distinct recovery mechanism from (a) an application release rollback, (b) a website configuration restore, and (c) factory defaults. It can affect installed software, identity, credentials and local data. Do not supply generic low-level SD cloning/serializing instructions to customers from an unversioned development snapshot.

## 6. Service escalation packet

Collect: device serial and hardware revision; reported installed release and Control Panel version; observed IP/host and connection state; exact symptom with first-occurrence time; component/channel; relevant settings (redacted); error/status text; whether the event followed a network, power, config or update change; results of safe external measurements; and a list of already attempted actions. If a log contains credentials, redact it before distribution.

For problem-specific steps use [Troubleshooting](Troubleshooting.md); for backup scope use [Configuration and Restore](Configuration-and-Restore.md); for device status structure use [Status and Diagnostics](../User-Interfaces/Status-and-Diagnostics.md).

## 7. Evidence and engineering review

`SigCoreUpdater/{Updater,ManifestValidator,ScuVerifier,VersionInstaller,UpdateEventReporter}.cs`; `SigCoreLauncher/{Launcher,ReleaseManager}.cs`; `WebBridge/Endpoints/{Restart,Status,BackupRestore}Endpoints.cs`; `libs/SigCoreCommon/{HardwareManager,MYSQL_LOGGER}.cs`; `WebBridge/ChartRecorder.cs`; device-site Logs/Device UI. All observations are scoped to the unversioned development snapshot.

**Pending:** approved field-service workflow; hardware supply/environmental ratings; release-specific restart/output-state test; service log retention; deployed logger architecture and credential remediation; end-to-end automatic update and fall-back regression.
