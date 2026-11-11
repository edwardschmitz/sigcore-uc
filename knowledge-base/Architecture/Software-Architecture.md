---
title: "Software Architecture"
status: source-draft-review-required
hardware_revisions: "Rev B software context; hardware-specific details in Hardware chapters"
software_versions: "Supplied software source snapshot; exact release and commit not independently established"
source_review_date: 2026-11-06
customer_release_approved: false
---

# Software Architecture

> **Source-grounded engineering draft; not approved for customer distribution.** The process boundaries and code behavior below were reviewed in the supplied development source. They are not a guarantee that every source component is enabled or installed in every deployed release. The public SigCore UC support references govern customer-facing feature claims. See [System Architecture](System-Architecture.md) for the device-wide hardware/software boundary.

## 1. Architectural model

SigCore UC uses a **hardware-owning Core service**, **client-facing applications**, and **separate release-management components**. The primary design rule is that field I/O and control state belong to Core, not to the web server or desktop UI. Applications that display values or request changes do so through the Core client protocol.

### 1.1 Component responsibilities

| Component | Where it runs | Primary responsibility | Implementation evidence |
|---|---|---|---|
| **SigCoreServer (Core)** | Controller, Linux / .NET | Instantiates `HardwareManager`, restores configuration, initializes hardware, starts I/O and PID loops, and serves the controller protocol on TCP 7020 | `Server/SigCoreServer/SigCoreServer.cs`, `libs/SigCoreCommon/HardwareManager.cs` |
| **SigCoreCommon** | Shared .NET library | Core/client protocol, typed payloads, client connection handling, drivers and configuration/control classes | `libs/SigCoreCommon/` |
| **Windows Control Panel** | Windows computer | Human control/configuration client, using the same shared Core client layer | `ControlPanel/`, public Control Panel Reference |
| **WebBridge** | Controller, separate ASP.NET Core service | Maintains one managed Core connection, caches the latest subscribed state, and offers the **private** HTTP API | `WebBridge/Program.cs`, `WebBridge/CoreState.cs`, `WebBridge/Endpoints/` |
| **Device website** | Controller, Apache/PHP and browser-delivered HTML/JavaScript | Browser interface; server-side PHP proxies supported calls to local WebBridge | `UpdaterFiles/www/`, `UpdaterFiles/www/api/_bridge.php` |
| **ChartRecorder** | **Inside WebBridge**, not Core | Samples WebBridge's latest analog state into a RAM-based historical ring buffer, answers history queries, and stores display/recording preferences | `WebBridge/ChartRecorder.cs`, `WebBridge/Endpoints/ChartEndpoints.cs` |
| **SigCoreLauncher** | Controller | Chooses an eligible installed release, switches `current` symlink, launches and supervises that release's configured executables, and falls back after probation failure | `SigCoreLauncher/Launcher.cs`, `SigCoreLauncher/ReleaseManager.cs` |
| **SigCoreUpdater** | Controller | Checks signed update manifests, downloads/verifies `.scu` packages, prepares a release and marks it `READY` | `SigCoreUpdater/Updater.cs`, `VersionInstaller.cs`, `ScuVerifier.cs` |
| **MySQL logger functionality** | **Observed within Core in this source snapshot** | Periodic snapshot/header database operations managed by `HardwareManager` and `MYSQL_LOGGER` | `libs/SigCoreCommon/HardwareManager.cs`, `MYSQL_LOGGER.cs` |

**Logger qualification:** The reviewed `HardwareManager` starts the MySQL logger loop itself. A separately deployed logger process was discussed in project planning, but is **not demonstrated by this source snapshot**. Verify the deployed release before claiming that logging has a separate service or fault boundary.

### 1.2 Connection topology

```text
Windows Control Panel / external .NET client
               |  SigCoreCommon client / TCP 7020
               v
      SigCoreServer (Core) <-------------------------- WebBridge
               |                                  |     ^
               | internal HardwareManager          |     | localhost TCP 7020
               v                                  |     |
  physical I/O, PID, config, logger               |     |
                                                  |     |
 browser --> on-device website --> PHP proxy --> localhost HTTP 8080
                   (HTTP)               (WebBridge private API)

 SigCoreUpdater --> verified package --> /opt/sigcore/versions/<version>
                                              |
                                 releases.json: READY
                                              |
 SigCoreLauncher --> selects eligible release, sets /opt/sigcore/current
                   --> starts/supervises release executables
```

The arrow from the browser website to WebBridge is **through server-side PHP**. A normal browser should not be told to call port 8080 on the device's LAN address. Core protocol TCP 7020 is not the website's HTTP API and should not be described as Modbus TCP.

## 2. Core: ownership, initialization and execution

`SigCoreServer` creates `HardwareManager`, creates `CommandDispatcher` and `OsManager`, connects dispatcher to the hardware manager, calls `HardwareManager.Initialize()`, and then starts its listener. Thus, hardware/configuration initialization **precedes** accepting TCP clients in this code path.

During initialization `HardwareManager` loads the stored configuration, applies per-channel relay, analog-input, analog-output, digital-input and PID settings, initializes these modules, and starts separate background loops for analog inputs, digital inputs, PID control and logging (if enabled). The central manager provides methods used by dispatched Core commands and publishes change notifications to subscribing sessions.

| Activity | Implemented scheduling / semantics | Do **not** infer |
|---|---|---|
| Analog sampling | `SampleAnalogInLoop` requests a 1 ms task delay between calls to `AnalogIn.GetValue()`; analog ADC data rate also depends on configuration | A guaranteed 1 ms sample per channel, hard real-time jitter, or uniform channel conversion rate |
| Digital-input sampling | `SampleDigitalInLoop` requests a 1 ms delay and notifies subscribers when `DigitalIn.SampleInputs()` reports a change | Guaranteed minimum pulse capture width or exact sampling frequency |
| PID computation | `SamplePIDLoop` waits 100 ms and then calls `PIDs.ComputeAll()` | Exactly 10 Hz control or a deterministic 100 ms loop period under all loads |
| Database logging | A `MYSQL_LOGGER` loop uses the configured interval if enabled | That MySQL logging and the chart recorder are the same data store |

Sampling and configuration share live device resources. For example, `SetAInConfig` in `HardwareManager` stops, updates and restarts the analog acquisition loop rather than merely changing a presentation label. Detailed calibration, timing and I/O behavior belong in the dedicated chapters, not in assumptions based on this overview.

## 3. Controller protocol and client connection lifecycle

### 3.1 Core TCP transport

Core listens on **TCP 7020** using `TcpListener(IPAddress.Any, 7020)` in the reviewed source. Each client gets a `ClientSession` that receives **newline-delimited UTF-8 protocol messages**, decodes them using `SigCoreProtocol`, dispatches commands through `CommandDispatcher`, and writes responses or subscribed events. The message type enumeration and payloads are owned by `SigCoreCommon`, not by the web API.

`SigCoreSystem` is the higher-level .NET façade. Its managed connection tracks states including connecting, handshaking, subscribing, connected and reconnecting; it handles reconnect and re-subscription while started. The class default `ReconnectDelay` is **one second**, but this is a programmed delay, not an availability guarantee. A raw TCP socket being open is weaker evidence than a completed handshake/subscription.

### 3.2 Requests, state changes, and acknowledgement

Commands may perform an operation without returning a confirmation of its applied physical state. Subscribers receive change alerts for analog inputs, digital inputs, relays, analog outputs, PID values and logger status. An API state value is **software-reported state**, not an independent electrical measurement of a relay contact or analog output.

A controller command, its success/acceptance, the next Core state notification, WebBridge's current cache and physical field response are **different observations**. Do not conflate them. For safety- or process-critical verification, use appropriate independent measurement and the product's documented guarantees.

### 3.3 Device discovery

`SigCoreDiscovery` discovers `_sigcore._tcp.local.` mDNS advertisements and uses a reachability probe of TCP 7020. Its returned result is a discovery/connection aid, not device authentication, authorization or an uptime guarantee. Exact mDNS publication setup and default network security policy must be confirmed from deployment configuration.

## 4. Browser software and the private HTTP bridge

`WebBridge/Program.cs` creates one `CoreState`, starts its managed `SigCoreSystem` connection to **127.0.0.1:7020**, registers its endpoints and hosts its HTTP service on **127.0.0.1:8080**. `CoreState` stores the latest subscribed values in lock-protected arrays and dictionaries. Consequently, an HTTP `/api/state` read is a **cached snapshot**, not a direct simultaneous ADC conversion or synchronous command to every I/O channel.

The device website, located in `UpdaterFiles/www/`, has HTML/JavaScript pages and PHP handlers in `api/`. `_bridge.php` directs requests to `http://127.0.0.1:8080` using server-side HTTP calls. The reviewed bridge has endpoint groups for status, relay control, analog-output control, configuration, PID, device settings, restart, backup/restore, and chart history/configuration. The **PHP-exposed paths, accepted methods, argument validation and authorization must be checked separately**; a private WebBridge endpoint does not by itself prove an identical public HTTP route exists.

This design gives two distinct client surfaces:

- **Native client:** Control Panel or other Core-protocol client talks to TCP 7020.
- **Browser client:** Browser talks to the device website; PHP talks to loopback WebBridge; WebBridge talks to Core using the native client.

The public `sigcoreuc.com` product website and its support references are separate from the website hosted on the device. The public reference pages are the authoritative human-facing documentation; obsolete local copies of those references are not an independent source of product specifications.

## 5. Data ownership, configuration and persistence

| Data class | Owner | Storage / behavior visible in source | Restart expectation |
|---|---|---|---|
| Hardware settings, identity and network-related properties | Core `HardwareManager.Config` | `/opt/sigcore/properties.json`; valid file preferred; legacy FRAM fallback/migration when applicable | File-backed configuration can be reloaded after Core restart; changing live output values is a separate question |
| Active release metadata and dispositions | Launcher/Updater via `ReleaseStateStore` | `/opt/sigcore/releases.json`, installed versions in `/opt/sigcore/versions/`, active `/opt/sigcore/current` symlink | Survives normal process restart as filesystem state |
| Selected release's startup executable list | Launcher | `/opt/sigcore/current/config.json` | Loaded during release activation; distinct from `properties.json` |
| WebBridge chart recorder preferences | WebBridge | `/opt/sigcore/webbridge-chart.json` on Linux unless environment override is used | Preferences persist when saved successfully |
| WebBridge chart historical samples | `ChartRecorder` inside WebBridge | Segmented **RAM ring buffer**, not stored in Core or a history file; default interval **1 second** | **Lost** if WebBridge restarts/exits; changing record interval or memory limit resets buffer in reviewed code |
| Recent browser state | `CoreState` in WebBridge | In-memory, event-driven cache received from Core | Cache is rebuilt from new notifications/requests after reconnection; do not assert instantaneous complete state at startup |
| MySQL measurement logger data | `MYSQL_LOGGER` invoked from Core in snapshot | External MySQL storage when configured and operating; separate from ChartRecorder history | Database persistence depends on server configuration and successful writes, not the chart buffer |

`ChartRecorder` defaults to a 1-second record interval and a **1024 MiB configured memory ceiling** in this source. That ceiling is **not RAM reserved in advance**: the code allocates 4096-record segments as samples arrive and discards the oldest samples when full. The default must not be misrepresented as actual Pi free memory or an experimentally verified recording duration.

## 6. Release management, updates and recovery

### 6.1 Two distinct jobs

**Updater prepares; Launcher activates.** In the supplied source:

1. `SigCoreUpdater` processes pending installation tasks from the active release at startup, then checks the update feed at approximately **six-minute intervals** when running.
2. It downloads and cryptographically validates the manifest, checks hardware revision and major-version policy, then downloads the release archive and verifies its **signature and SHA-256 digest**.
3. It stages and prepares the version under `/opt/sigcore/versions/`, recording `STAGING` followed by `READY` after preparation. Its code does **not** directly switch `/opt/sigcore/current` in the reviewed path.
4. `SigCoreLauncher` selects the highest eligible `READY`/`GOOD` installed release when starting, atomically updates `/opt/sigcore/current` to that release, loads its `config.json`, and starts the configured executable files.
5. A newly selected `READY` release must keep its configured processes alive for the Launcher's **30-second probation** before it becomes `GOOD`. A failure can mark it `FAILED` and trigger fallback to the highest `GOOD` version.
6. After probation, Launcher checks the configured child processes at a **two-second** interval and attempts a restart if one has exited.

The source blocks automatic **major-version** jumps; these require a separate manual upgrade path. Activation trigger/timing after a package reaches `READY` is **not established** by this chapter. Do not claim automatic immediate cutover, remote control of customer equipment, or functional hardware validation during process-survival probation.

### 6.2 What a release-state flag means

| State | Source meaning | Limitation |
|---|---|---|
| `STAGING` | Preparation in progress or attempted | Not executable approval |
| `READY` | Downloaded/validated/prepared for selection | Does not mean it has run successfully |
| `GOOD` | Passed Launcher's configured process-survival probation | Does not independently prove sensors, relays or external services are healthy |
| `FAILED` | Launcher recorded a failed activation/probation | Failure cause requires logs/diagnosis |
| `REJECTED` | Enum exists in common release state | Specific transition policy not established in this chapter |

The current `ReleaseDisposition` enum lists `STAGING`, `READY`, `GOOD`, `FAILED` and `REJECTED`. Do not describe planned `REGRESSED` release-selection behavior as already implemented just because it appears in design notes.

## 7. Failure domains and diagnostic interpretation

| Symptom | Possible software boundary | What the symptom does *not* prove |
|---|---|---|
| Windows Control Panel loses connection | PC network, device networking, TCP 7020, Core, handshake/subscription | That device I/O physically stopped |
| Device website loads but reports Core disconnected | Apache/PHP still accessible, but WebBridge-to-Core path or Core unavailable | That website page availability establishes Core health |
| WebBridge HTTP unavailable | Private bridge process, loopback server, PHP proxy operation | That Core TCP protocol is necessarily down |
| Chart history disappears after WebBridge restart | Expected RAM-only recorder lifecycle | That the MySQL logger database was erased |
| A `READY` release does not appear active immediately | Updater/Launcher are distinct; activation schedule not proven | That download or verification failed |
| Relay state API reads `ON` | Core software logical state | That relay coil/contact position was independently measured |

**Security and safety scope:** TCP 7020 is bound on all network interfaces in Core code; WebBridge 8080 is loopback-only. The deployment's web server exposure, firewall, authentication, interlock behavior, remote-access policy and service sandboxing cannot be established solely from these listeners. No hard-real-time or safety-certification guarantee follows from this process architecture.

## 8. Source evidence and applicability

All source paths are relative to the **development-only** folder `project/sources/SigCore-Software-Source/`; these are evidence pointers for engineering review, not customer installation paths or customer-distribution links.

| Topic | Evidence |
|---|---|
| Core construction, TCP listener and sessions | `Server/SigCoreServer/SigCoreServer.cs`, `ClientSession.cs`, `CommandDispatcher.cs` |
| Hardware manager, sampling, config and logger loop | `libs/SigCoreCommon/HardwareManager.cs`, `MYSQL_LOGGER.cs` |
| Native messaging and reconnection | `libs/SigCoreCommon/SigCoreProtocol.cs`, `SigCoreSystem.cs`, `SigCoreClient.cs`, `SigCoreDiscovery.cs` |
| WebBridge bind, state cache and API | `WebBridge/Program.cs`, `CoreState.cs`, `Endpoints/*.cs` |
| Website forwarding | `UpdaterFiles/www/api/_bridge.php`, related PHP endpoints and pages |
| Chart defaults, RAM storage and reset behavior | `WebBridge/ChartRecorder.cs`, `Endpoints/ChartEndpoints.cs` |
| Release selection/probation/supervision | `SigCoreLauncher/Launcher.cs`, `ReleaseManager.cs`, `libs/SigCoreCommon/Update/ReleaseStateStore.cs`, `ReleaseState.cs` |
| Update source and verification | `SigCoreUpdater/Updater.cs`, `ScuVerifier.cs`, `ManifestValidator.cs`, `VersionInstaller.cs` |
| Product-supported UI and APIs | `project/sources/documentation/control-panel-reference.html`, `api-reference.html` (snapshot; verify live public reference before release) |

## 9. Unresolved release/deployment questions

These are **internal verification items**, not evidence of product defects:

1. Identify the exact software commit and installed release(s) corresponding to this bundled source snapshot. Do not copy these observations to a different release without checking its code.
2. Review the installed systemd units, Apache configuration, device firewall and service start order to establish the real boot topology and network exposure.
3. Confirm which executables are listed in deployed release `config.json`, and whether Updater is supervised independently of Launcher.
4. Confirm when a downloaded `READY` release is selected in production, including reboot/restart orchestration outside the Updater source path.
5. Reconcile the source's **Core-owned** MySQL logger loop with any newer separately deployed logger architecture.
6. Review whether Launcher's process-survival check is intended to be the only release probation gate; do not mistake it for hardware functional testing.

## 10. Related KB chapters

- [System Architecture](System-Architecture.md): hardware-to-software boundaries and signal flow.
- [Hardware Overview](../Hardware/Hardware-Overview.md): Rev B PCB, Pi, I/O subsystem and power paths.
- [Analog Inputs](../Hardware/Analog-Inputs.md), [Analog Outputs](../Hardware/Analog-Outputs.md), [Digital Inputs](../Hardware/Digital-Inputs.md), and [Relay Outputs](../Hardware/Relay-Outputs.md): actual I/O behavior, limitations and wiring.
- [AI Instructions](../AI-Instructions/Instructions.md): directions to the consuming AI about answering accurately, incorporating linked images and distinguishing facts from inference.

**Illustrations:** This chapter uses text topology diagrams; no verified software/process diagram was supplied. Do not present a speculative diagram as an official SigCore UC network or security design.
