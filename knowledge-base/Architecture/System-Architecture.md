---
title: "System Architecture"
status: source-draft-review-required
hardware_revisions: Rev B design evidence
software_versions: "source snapshot in sources(2).zip; precise commit/release not established"
source_review_date: 2026-11-02
customer_release_approved: false
---

# System Architecture

> **Evidence-based development draft, not yet approved for customers.** This chapter describes behavior visible in the supplied Rev B schematic, product-site references and application source snapshot. A source file's presence alone does not prove a feature is installed, enabled, supported, or tested in a particular released image. Source paths are recorded in **Evidence**. Internal implementation, customer-documented capabilities, and unresolved questions are distinguished below.

## 1. Product boundary

SigCore UC is a Raspberry Pi 4-based controller with local field I/O, onboard control software, a Windows Control Panel, an on-device web interface, and application programming interfaces. The current customer-facing Hardware Reference identifies the Rev B I/O inventory as **eight relay outputs (R1–R8), eight digital inputs (DI1–DI8), four analog inputs (AI1–AI4), and four analog outputs (AO1–AO4)**. The top-panel overlay uses the same channel identifiers and labels relay terminals `NC`, `COM`, and `NO`.

These are distinct elements:

| Element | Responsibility | Evidence and scope |
|---|---|---|
| Rev B PCB and analog/digital hardware | Physical connector and electrical signal conditioning, switching, conversion and output drive | Rev B schematic; Hardware Reference |
| Raspberry Pi 4 and Linux environment | Runs the controller and local services | Hardware Reference; process source |
| **SigCoreServer** (Core) | Instantiates `HardwareManager`; owns the physical I/O configuration, sampling/control routines and Core command dispatch | `Server/SigCoreServer/SigCoreServer.cs`, `libs/SigCoreCommon/HardwareManager.cs` |
| **SigCoreCommon** | Shared .NET types, controller client, TCP message protocol, I/O drivers and configuration payloads | `libs/SigCoreCommon/` |
| **ControlPanel** | Windows application communicating with Core using the common client layer; displays live state and allows settings/control | `ControlPanel/`, product Control Panel Reference |
| **WebBridge** | Separate ASP.NET Core service connecting to Core; exposes local HTTP endpoints and caches subscribed state | `WebBridge/Program.cs`, `WebBridge/CoreState.cs`, `WebBridge/Endpoints/` |
| On-device website | Browser UI and PHP endpoints, forwarding supported requests to the localhost WebBridge HTTP API | `UpdaterFiles/www/`, especially `api/_bridge.php` |
| **ChartRecorder** | WebBridge-owned in-memory, timestamped analog-input history and query endpoints | `WebBridge/ChartRecorder.cs`, `WebBridge/Endpoints/ChartEndpoints.cs` |
| **SigCoreLauncher** | Selects/activates installed release and launches/supervises the configured processes | `SigCoreLauncher/Launcher.cs`, `SigCoreLauncher/ReleaseManager.cs` |
| **SigCoreUpdater** | Device-side software update workflow | `SigCoreUpdater/` |

**Do not confuse:** a device's Core command connection, the private WebBridge HTTP API, the on-device browser interface, and the public product documentation website. They are different interfaces and paths.

## 2. Main runtime communication paths

### 2.1 Core connection and shared API

`SigCoreServer.StartAsync()` binds a TCP listener to **port 7020 on `IPAddress.Any`**. Each accepted connection is handled by a `ClientSession`. A session reads newline-delimited UTF-8 messages, decodes them with `SigCoreProtocol.Decode`, dispatches commands, and writes encoded responses/events with `WriteLine`. The shared `SigCoreSystem` class supplies asynchronous controller methods and subscription events to .NET clients.

Textual flow:

```text
Windows Control Panel / customer .NET application
  -> SigCoreCommon.SigCoreSystem / SigCoreClient
  -> TCP 7020 on controller
  -> SigCoreServer.ClientSession + CommandDispatcher
  -> HardwareManager -> physical I/O, config and control
```

Some commands are request/response, while the API Reference warns that most output/configuration setters do **not** acknowledge confirmed applied state. A caller requiring assurance should read back the relevant state/configuration. An accepted TCP connection alone does not guarantee a successful later operation.

### 2.2 Device website and WebBridge

`WebBridge/Program.cs` binds HTTP on **`127.0.0.1:8080`**, not on the controller's external network interfaces. Its `CoreState` starts a `SigCoreSystem` client connection to **`127.0.0.1:7020`**, subscribes to Core events, and maintains a locked, most-recent-state cache.

The on-device website includes local PHP API handlers. `UpdaterFiles/www/api/_bridge.php` defines the base URL `http://127.0.0.1:8080` and forwards requests to the private bridge, rather than exposing the internal bridge listener directly.

```text
Browser -> controller's device website (HTTP server)
             -> PHP /api/*.php
                -> 127.0.0.1:8080 (WebBridge)
                   -> SigCoreSystem client -> 127.0.0.1:7020 (Core)
                                                -> HardwareManager
```

`GET /api/state` on WebBridge returns an HTTP JSON snapshot including the Core connection state, digital inputs, analog inputs, relay states, analog output values/modes, PID state and logger status. Values are maintained by subscription callbacks; they should not be described as synchronous hardware reads taken at the instant of the browser request.

`GET /api/config/{kind}/{channel}` and `PUT /api/config/{kind}/{channel}` support `relay`, `di`, `ai`, `ao`, and `pid` configurations at the private WebBridge API. The PUT handler requires a complete `config` object containing its allowed fields and returns an **accepted-but-not-confirmed** response. The website's PHP proxy and browser JavaScript must be consulted before describing a directly reachable external HTTP route as identical to a private WebBridge route.

### 2.3 Device discovery

`SigCoreDiscovery` uses mDNS service type **`_sigcore._tcp.local.`**, filters advertised IPv4 addresses, and checks whether TCP port 7020 accepts a connection before returning an eligible discovered device. The public API Reference covers this behavior. Successful discovery verifies reachability at scan time, not permission, identity, or continued connectivity. The service-advertisement installation and configuration should be verified separately; the discovery consumer alone does not demonstrate how advertisements are published.

## 3. Where measurement and control values originate

The `HardwareManager` owns four analog channels through `A_IN` and an ADS1115 driver, four analog outputs through `A_OUT`, eight digital inputs through `D_IN`, eight relays through `RELAY_OUT`, and four PID controllers through `PID_LOOP`.

An analog input normally takes this path:

```text
Field signal -> Rev B input terminal + signal conditioning / TMUX1209
 -> ADS1115 -> I2C read -> A_IN raw electrical sample
 -> configured channel calibration -> HardwareManager.CurrentReading
 -> Core event / Core request result
 -> Control Panel, customer API or WebBridge's cached state
 -> browser display / ChartRecorder sampling
```

The exact analog range, electrical conversions and caveats are in [Analog Inputs](../Hardware/Analog-Inputs.md). **Formatting the displayed value is different from changing the raw electrical measurement.**

Digital inputs, relays, analog outputs and PID loops have separate drivers and state handling; do not infer their fail-safe or startup behavior from the analog signal chain. They remain dedicated chapters.

## 4. Configuration and identity

Current source loads persistent device properties primarily from **`/opt/sigcore/properties.json`**. In `HardwareManager.Config.Load`, an existing valid JSON file takes precedence; code retains a migration/fallback path that can read legacy FRAM when the file is absent and the FRAM device exists. The current hardware does **not** require FRAM. Device configuration is built from a JSON object with these top-level groups:

```text
global, network, relays, analogInputs, analogOutputs,
digitalInputs, pids, logger
```

`HardwareManager.SaveConfig()` saves the constructed configuration to the file-based configuration system. Per-channel analog input configuration updates stop the analog sampling loop, apply the new payload, persist the configuration, and start the loop again (`HardwareManager.SetAInConfig`). That operation is not equivalent to merely changing a display format in a browser.

Software release identity and selected release reside in a different part of the system: `SigCoreLauncher` uses the `/opt/sigcore/versions` area and `/opt/sigcore/current` symlink. Do not treat the editable global properties and installed release selection as the same mechanism.

## 5. Chart recording and diagnostic logging are different

`WebBridge/ChartRecorder.cs` defines a **WebBridge-owned** four-channel analog-input recorder. Its default record interval in the supplied source is **one second**; it copies the latest WebBridge state into an in-memory timestamped circular history while connected. Configuration is stored separately at `/opt/sigcore/webbridge-chart.json` on Linux. **Recorded samples themselves are stored in memory in this implementation**; do not promise that the history survives restart or a WebBridge process exit.

Diagnostic event logs, logger configuration and the MySQL data logger are different concepts from the Chart Recorder. The website has a Log Viewer, and Core has `MYSQL_LOGGER` support. These should be documented independently, with source-verified persistence and error handling.

## 6. Software startup and updates

The supplied Launcher source selects and activates a release, reads `/opt/sigcore/current/config.json`, starts its configured executable processes, and supervises them. A release with `READY` disposition undergoes a **30-second process-survival probation** in the supplied Launcher implementation before being marked `GOOD`; probation failure can trigger fallback. This does **not** establish a comprehensive health/functional certification of all I/O.

`SigCoreUpdater` provides device-side update logic. Exact install scheduling, configured service definitions, telemetry and recovery rules are not fully established by this overview. Avoid representing planned future version-selection changes as present in this source.

## 7. Boundaries and unsupported inferences

- TCP 7020 is a Core endpoint. **Do not identify it as Modbus TCP solely because the repository also contains Modbus-related files.** The documented primary .NET client uses the SigCore message protocol.
- WebBridge's port 8080 is bound to localhost; the device website is the user-facing browser entry point. A deployment's firewall exposure and authorization must be assessed independently.
- Do not claim hard-real-time control, end-to-end safety certification, measured latency, or guaranteed measurement accuracy from the software architecture alone.
- The customer-facing reference pages on the **public product website** are authoritative for documented features. Obsolete reference copies formerly kept on the device website are not part of the canonical documentation.
- **Source snapshot version is unproven.** The supplied source package contains implementation code and current-looking references, but no verified commit ID linking the complete snapshot to one installed software release. Do not claim all described implementation details are present on every unit reporting a particular version.

## 8. Evidence

The paths below are relative to the supplied development-source folder `project/sources/SigCore-Software-Source/`, except where stated.

| Claim group | Primary evidence |
|---|---|
| I/O counts and documented hardware | `project/sources/documentation/hardware-reference.html` (identical to public product-site reference snapshot), `project/sources/hardware/SCH_Schematic1_2026-08-15.pdf`, `project/sources/hardware/top-overlay.pdf` |
| Core TCP and session transport | `Server/SigCoreServer/SigCoreServer.cs`, `Server/SigCoreServer/ClientSession.cs`, `libs/SigCoreCommon/SigCoreProtocol.cs` |
| I/O and persistent config | `libs/SigCoreCommon/HardwareManager.cs` and its `Config` class |
| Client API and discovery | `libs/SigCoreCommon/SigCoreSystem.cs`, `libs/SigCoreCommon/SigCoreDiscovery.cs`, `project/sources/documentation/api-reference.html` |
| Private bridge and browser proxy | `WebBridge/Program.cs`, `WebBridge/CoreState.cs`, `WebBridge/Endpoints/StatusEndpoints.cs`, `UpdaterFiles/www/api/_bridge.php` |
| Chart history | `WebBridge/ChartRecorder.cs` |
| Release supervision | `SigCoreLauncher/Launcher.cs`, `SigCoreLauncher/ReleaseManager.cs`, `SigCoreUpdater/` |

## 9. Follow-on chapters and open checks

Deepen the individual topics in [Processes and Lifecycle](../Core-Software/Processes-and-Lifecycle.md), [Data Model and Configuration](../Core-Software/Data-Model-and-Configuration.md), [Logging and History](../Core-Software/Logging-and-History.md), [Discovery and Connections](../Programming/Discovery-and-Connections.md), and [HTTP API and WebBridge](../Programming/HTTP-API-and-WebBridge.md).

Engineering review is still required for installation/service definitions, the snapshot's exact release identity, Modbus feature status, network exposure defaults, and complete process-failure handling. See the internal [Verification Register](../project/Verification-Register.md).

## Images

Suitable future assets: architecture/block diagram, runtime services map, field I/O signal chain. No image is required to establish the connectivity described above; diagrams must reflect these verified paths rather than introduce additional links.
