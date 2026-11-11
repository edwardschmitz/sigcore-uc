---
title: "Processes and Lifecycle"
status: source-draft-review-required
hardware_revisions: "Rev B runtime context"
software_versions: "Supplied software source snapshot; exact release/commit not established"
source_review_date: 2026-11-06
customer_release_approved: false
---

# Processes and Lifecycle

> **Source-grounded engineering draft; engineering review required.** This document describes **what the supplied source code implements**, not an independently verified set of processes and systemd services installed on every SigCore UC. Do not infer the production unit-file configuration, boot-order guarantees, restart policy, or exact release number from application source alone. For component boundaries see [Software Architecture](../Architecture/Software-Architecture.md).

## 1. Runtime process model

The controller's normal runtime has several separately identifiable responsibilities. Some are independent executables; others are tasks **within** an executable. These distinctions determine which state persists after a crash or restart.

| Component | Process boundary in supplied code | Lifecycle owner and purpose | Important limitation |
|---|---|---|---|
| **SigCoreLauncher** | Own program, `SigCoreLauncher/Program.cs` | Selects eligible installed release, starts its configured executable children, checks child survival and restarts failed children | Its **own** Linux service restart policy is not present in reviewed source |
| **SigCoreServer / Core** | Own executable | Constructs and initializes `HardwareManager`, runs internal I/O/PID work and listens for clients on TCP 7020 | There is no explicit Core shutdown/disposal sequence in reviewed `Server/SigCoreServer/Program.cs` |
| **WebBridge** | Own ASP.NET Core executable | Starts managed connection to Core, caches values, hosts private HTTP service on `127.0.0.1:8080`, owns chart recorder | HTTP availability does not prove the connection to Core is healthy |
| **ChartRecorder** | **Task/object in WebBridge**, not a separate OS process | Periodically records cached analog inputs to memory, serves history, persists settings | Samples and history are lost on WebBridge process exit or chart-buffer reset |
| **SigCoreUpdater** | Own executable | Runs active-release installation tasks once on startup, polls update feed and prepares release directories | Whether Updater is supervised by Launcher versus an external service depends on deployed `config.json` and service files |
| **Windows Control Panel** | Program on user's Windows computer | Client connection, control and configuration; independently reconnects as supported by its client library | Its loss of connectivity is not proof that Core or field I/O stopped |
| **MySQL logger** | Logger functionality starts **inside Core** in this snapshot | Configuration/header and snapshot logging code | An independent Logger process is **not demonstrated** by the reviewed snapshot |

A release's `config.json` supplies the **relative executable paths** Launcher starts. It is not safe to claim a fixed list of children until the actual deployed release config has been inspected. The source tree does **not** include a verified inventory of installed systemd units or Apache startup dependencies.

### 1.1 Principal process and data paths

```text
OS startup / service manager (installed unit files not verified)
       |
       v
 SigCoreLauncher
       |  select READY/GOOD release -> /opt/sigcore/current symlink
       |  load current/config.json
       +--> configured child: SigCoreServer (Core)
       |        +--> hardware init -> analog/digital/PID/background work
       |        +--> TCP 7020 -> Control Panel, WebBridge, .NET clients
       +--> configured child: WebBridge (if listed)
       |        +--> client connection to 127.0.0.1:7020
       |        +--> chart sampler in RAM
       |        +--> private HTTP 127.0.0.1:8080 <-- PHP/Apache website
       +--> other configured executables (check installed config)

 SigCoreUpdater (own executable; supervisor arrangement unverified)
       +--> installation tasks of ACTIVE release at startup
       +--> signed manifest/package checking, verification and staging
       +--> releases.json updated to READY

 Linux reboot/shutdown --> external OS lifecycle
```

This is an architectural illustration of code paths, **not** a verified boot-order or systemd wiring diagram.

## 2. Initial startup of Core

1. `Server/SigCoreServer/Program.cs` initializes the application's log session and constructs `SigCoreServer`.
2. The `SigCoreServer` constructor creates `HardwareManager`, `OsManager`, and `CommandDispatcher`, attaches dispatcher to the hardware manager, then calls `HardwareManager.Initialize()`.
3. `HardwareManager.Initialize()` reads persisted configuration and processes relay, analog input, analog output, digital input, PID, logger and network settings. Missing mandatory I/O or PID sections throw rather than continuing with an assumed complete configuration.
4. Core initializes relay outputs, analog inputs, analog outputs, digital inputs and PID objects, sets up a snapshot, and starts analog, digital, PID and logging-related routines.
5. **Only after construction/initialization**, `SigCoreServer.StartAsync()` binds `TcpListener(IPAddress.Any, 7020)` and begins accepting connections.
6. Each accepted socket receives a session ID and an independently running `ClientSession` task. Session cleanup removes it from the active session dictionary upon completion.
7. The program awaits `Task.Delay(Timeout.Infinite)` after starting the listener. The reviewed program does not register cancellation handlers or invoke a Core-wide stop/dispose method.

**Failure during startup:** An exception in configuration/hardware initialization prevents reaching the listener; a bind error is logged and thrown. Launcher may observe the resulting process exit, but its response differs depending on whether the release is still in probation (see §5).

**Safety caution:** Hardware initialization may set physical outputs. Neither a newly opened network connection nor a successful process start establishes what connected equipment is doing. Actual relay and analog-output startup behavior is documented in [Relay Outputs](../Hardware/Relay-Outputs.md) and [Analog Outputs](../Hardware/Analog-Outputs.md).

### 2.1 Internal loops are not independent services

| Loop or function | Owner | Scheduling/error behavior seen in source | Recovery scope |
|---|---|---|---|
| Analog acquisition | `HardwareManager` | Managed task with cancellation/restart support; nominal 1 ms delay between iterations, driver timing also applies | Loop can be stopped/restarted by configuration code without restarting Core |
| Digital-input polling | `HardwareManager` | Task requests 1 ms delay; reports state change to subscribed sessions; catches and logs per-iteration exceptions | Iteration retries after error delay while Core continues |
| PID computation | `HardwareManager` | Task requests 100 ms delay then computes PID controllers; catches/logs exceptions | PID task continues unless stopped/canceled or a fatal condition occurs |
| Database logger code | `HardwareManager` / `MYSQL_LOGGER` | Includes a logger initialization path and an interval-based snapshot loop in source | Do not infer an independent daemon or continuous sampling guarantee |
| Chart sampling | `ChartRecorder` inside WebBridge | Default 1-second recording interval; samples CoreState cache, records in RAM | WebBridge lifetime, not Core lifetime |

Task delays are **requested delays**, not deterministic scan-period specifications. Loop exceptions may be handled internally without causing a process exit, so Launcher's process-survival check is not a substitute for checking application functionality.

## 3. Client connection and reconnection lifecycle

### 3.1 TCP client session (Core side)

Core accepts multiple TCP sessions. `ClientSession` handles protocol messages and subscription notifications separately for each. A client session can disconnect without terminating Core. The `SigCoreServer` dictionary removes the session after its `RunAsync` task ends.

### 3.2 Managed .NET connection

The high-level `SigCoreSystem.StartAsync(host, port)` implements long-lived connection management. Its state progression can include:

`Disconnected` → `Connecting` → `Handshaking` → `Subscribing` → `Connected`.

After transport loss it enters `Reconnecting`, closes the failed transport, and attempts to reconnect, including a fresh protocol handshake and subscription. `ReconnectDelay` defaults to **one second** in this class. A **protocol-rejected** connection moves to `Faulted`, and the managed connection loop stops rather than endlessly retrying an incompatible/rejected handshake. `Stop()` cancels further reconnects, closes the transport and fails pending requests.

`IsConnected` reports underlying transport connectivity. For an application depending on live state notifications, `ConnectionState == Connected` is the stronger test because it includes handshake/subscription completion.

### 3.3 WebBridge startup and restart

`WebBridge/Program.cs` initializes logging, constructs `CoreState`, calls `state.Start()` (asynchronously begins connecting to `127.0.0.1:7020`), creates `ChartRecorder`, maps HTTP endpoints and binds `127.0.0.1:8080`. Therefore the web process may accept HTTP requests while its connection to Core is still pending or recovering.

On ASP.NET application stopping, WebBridge invokes `chartRecorder.Dispose()`, `state.Stop()` and disposal of the client connection. Chart history is **volatile**. The recorder reads settings from `/opt/sigcore/webbridge-chart.json` on Linux (or an environment override), but the samples themselves are not written to that file. Changing the recorder's sample interval or memory allocation can also reset the in-RAM history without restarting WebBridge.

**Website boundary:** Apache/PHP serves the browser and proxies supported requests to WebBridge locally. The website being reachable does not prove WebBridge is running; WebBridge being reachable does not prove Core is fully connected. The normal browser should not be directed to call `:8080` from the LAN.

## 4. Updater startup, periodic checks and one-time tasks

`SigCoreUpdater.Updater.RunAsync()` establishes its work directories, loads its running release's `config.json`, runs pending installation tasks, then enters an update-check loop. The check interval is a constant **six minutes**, measured after each check finishes (not a guaranteed wall-clock schedule).

### 4.1 Installation tasks at Updater startup

The task runner looks at the active release's `/opt/sigcore/current/installation-tasks.json`. It first confirms that the active release's version and hardware revision match the **running Updater's** configuration; otherwise it postpones the tasks. Tasks have unique IDs; completed IDs are recorded in `/opt/sigcore/completed-tasks.json`. Each successful task is recorded promptly. Failed tasks are logged and remain eligible for retry at a **later Updater startup**, not continuously retried within the current process.

An optional `check` command may establish that the desired state is already satisfied. If a task's command succeeds but the completion record cannot be committed, the check on the next startup is important for safe retry. Installation-task failure is deliberately handled without blocking normal update checks.

### 4.2 Update preparation sequence

1. Download manifest and signature; verify manifest signature using installed public key.
2. Validate applicable hardware revision, release version and policy, including **no automatic major-version upgrade**.
3. If not already prepared, record version in `/opt/sigcore/releases.json` as `STAGING`.
4. Download `.scu` package/signature; require valid signature and expected SHA-256 digest.
5. Extract to `.staging-<version>`, validate release metadata and executable files, set executable permissions, then move directory to `/opt/sigcore/versions/<version>`.
6. Mark successfully prepared release `READY`. Existing active release is not switched here.

The updater logs preparation errors and continues checking on later intervals. An interrupted `STAGING` attempt can be cleaned/rebuilt. An `UpdateEventReporter` may report a verified `DOWNLOADED` event through an optional HTTPS telemetry configuration; a telemetry failure does **not** block update installation. Do not interpret telemetry as remote command/control of the device or as a transfer of measurements and I/O state.

**Activation timing:** The supplied `Updater.cs` does not instruct Launcher to activate `READY` immediately. Launcher selects an eligible release when **Launcher starts**. Any production mechanism that restarts Launcher or otherwise triggers selection requires independent deployment evidence. Thus "downloaded", "ready", "active", and "good" are distinct milestones.

## 5. Launcher release selection and process supervision

### 5.1 Selection and activation

On entering `Launcher.RunAsync`, `StartSelectedReleaseAsync` selects the **highest version** whose disposition is `READY` or `GOOD`. Version eligibility is evaluated using parsed `System.Version` values, not lexical filename order. The Launcher atomically switches `/opt/sigcore/current` to the selected `/opt/sigcore/versions/<version>` directory using a temporary symlink and `rename()`. It rejects a `current` path that already exists but is not a symbolic link.

Launcher loads `/opt/sigcore/current/config.json` and checks that its version matches the selected release, that its executable list is nonempty, and that each path is relative, remains under the release directory and resolves to a file. It then starts each configured program with the working directory set to `/opt/sigcore/current`. A newly selected `READY` version enters probation; an already `GOOD` version does not.

### 5.2 READY release probation

For **30 seconds**, Launcher checks approximately every second that every configured child process is still alive. If all survive, it marks the release `GOOD` in `releases.json`. These checks do **not** verify that Core bound port 7020, that WebBridge can reach Core, that sensors are updating, or that field outputs respond correctly.

If a READY version fails to activate or any configured child exits during probation, Launcher marks it `FAILED`, stops its child processes and attempts to activate the **highest `GOOD`** release. The fallback is a **whole-release** transition, not restart of the failed version's individual programs. The fallback is given one two-second supervision interval to confirm its configured processes have started; this is not a new full probation or hardware test. If there is **no** `GOOD` release, selection/fallback can fail rather than silently inventing a recoverable state.

### 5.3 After probation: child-process restart

In steady state Launcher wakes on a requested **two-second** interval. If a configured child has exited, it attempts `StartProcess` on the same relative executable path. A successful restart of the process is logged; failure is logged and can be retried on the next pass. In this steady-state path, a child crash does **not** automatically mark the already `GOOD` release `FAILED` or force version fallback.

This difference is important:

| Event | During READY probation | After release is GOOD |
|---|---|---|
| Configured child exits | Probation fails; mark version `FAILED`, try highest `GOOD` | Restart that child in active release |
| Child starts but is functionally unhealthy | May still pass if it stays running | No built-in application-health test in this loop |
| Updater prepares a newer READY version | Updater only records availability | No automatic activation demonstrated without Launcher restart/other orchestration |
| Launcher itself exits | Not handled by its own child-process loop | Depends on OS service manager; installed policy not verified |

## 6. Shutdown, reboot and termination

### 6.1 Launcher termination is forced, not a graceful Core stop

`SigCoreLauncher/Program.cs` registers `ProcessExit` and `Console.CancelKeyPress` handlers that cancel its token. When `Launcher.RunAsync` stops, it calls `StopAsync()`, which terminates configured children using `Process.Kill(entireProcessTree: true)` followed by `WaitForExit(5000)` for each. Launcher then waits 800 ms after clearing its process list.

This is a **process-tree kill**. It does **not** request an orderly application-level shutdown over TCP, drain ongoing hardware operations, guarantee a final MySQL write, explicitly set relays to a fail-safe state, or wait for the chart recorder to persist its RAM samples. The WebBridge `ApplicationStopping` callback defines an orderly cleanup **if that callback executes**, but the Launcher code does not guarantee it will do so before forced termination.

### 6.2 Core process termination

The reviewed Core `Program.Main` calls `StartAsync()` then awaits indefinitely and has no explicit cancellation handler/stop/dispose path. The operating system and service manager ultimately handle Core termination. Therefore **do not promise defined hardware outputs on process termination, crash, reboot or power failure** based on this lifecycle code. For configured relay fail-safe behavior see [Relay Outputs](../Hardware/Relay-Outputs.md), including its unresolved implementation finding.

### 6.3 Device reboot and halt commands

The Core protocol's `HandleRestart` and `HandleShutdown` call `OsManager.RestartDevice()` and `OsManager.ShutdownDevice()`, which run `sudo reboot` and `sudo shutdown -h now` respectively. The web API has a separate `/api/restart` endpoint that invokes noninteractive `sudo -n /usr/bin/systemctl reboot --no-block` locally and returns `accepted: true` if the command exits successfully. An **accepted reboot request** is not confirmation that the device has already restarted, recovered network connectivity or reached operational readiness.

The web restart endpoint does not go through the Core TCP restart handler. Neither route is evidence that user-configured outputs complete a safe shutdown transition. The required sudo permissions and installed systemd behavior must be checked on deployed hardware.

## 7. Persistence and process boundaries

| State / artifact | Storage and ownership | Effect of process restart |
|---|---|---|
| Core I/O/PID configuration and device identity | Device configuration under `/opt/sigcore/` (`properties.json` in reviewed Core architecture) | Configuration can be reloaded on Core startup; live state is initialized according to implementation, not guaranteed retained |
| Active release identity | `/opt/sigcore/current` symlink | Remains on disk unless selection changes it |
| Installed release dispositions | `/opt/sigcore/releases.json` | Persist across program/process restart; Launcher uses this for selection/fallback |
| Updater completed tasks | `/opt/sigcore/completed-tasks.json` | Completed IDs persist; incomplete tasks reconsidered at next Updater startup |
| Chart recorder settings | `/opt/sigcore/webbridge-chart.json` by default on Linux | Loaded by new WebBridge process |
| Chart recorder history | WebBridge RAM ring buffer | **Lost** on WebBridge restart; also reset on certain config changes |
| WebBridge cached Core state | WebBridge process memory | Rebuilt from events after reconnect; avoid treating stale/unavailable cache as a live measurement |
| SQL logger data | Configured external database, if successfully written | Depends on database durability and configured availability; not equivalent to chart RAM |
| Connection subscriptions | Client/Core session runtime | Connection/handshake/subscription reestablished by managed clients after reconnect |

Do not confuse **persisted configuration** with **restored real-time control state**. In particular, retained relay settings do not necessarily imply the physical relay maintains its pre-reboot contact position.

## 8. Diagnostic decision guide

| Observed symptom | First boundary to check | What not to conclude |
|---|---|---|
| No response on TCP 7020 | Core process, early initialization logs, listener bind, host/network | That every process on the controller is down |
| Web UI renders; I/O values stop updating | Core reachability from WebBridge, managed connection state and event cache | That a page refresh proves actual I/O sampling |
| `:8080` not reachable from another computer | Expected localhost-only WebBridge binding | That private service is malfunctioning |
| Chart history vanished, while AI readings still work | WebBridge process restart or chart-buffer reset | That a separate SQL logger's historical database was deleted |
| A new release is `READY` but old version remains active | Distinguish preparation from Launcher selection; check restart/orchestration | That the signed package necessarily failed verification |
| Child repeatedly dies after GOOD activation | Launcher per-child restart logs; failing child's logs and startup dependencies | That Launcher will automatically roll back the version |
| READY release falls back | Activation or child-process exit during 30-second probation; check FAILED reason | That output wiring or measurement accuracy was tested |
| After a reboot, a relay is in an unexpected position | Relay initialization/polarity/configuration plus real contact wiring | That the prior logical state must have persisted |
| `accepted: true` from restart HTTP API | OS accepted a command | That boot is complete and the controller is ready |

### 8.1 Engineering observations to collect on a real unit

For lifecycle verification, record the running version and hardware revision, the complete deployed release's `config.json` executable list, installed systemd unit files, current symlink destination, `releases.json`, process tree, listener/socket state, core/bridge/updater logs, and observed I/O safety behavior under **planned** restart and failure scenarios. On a production machine, use the manufacturer's approved shutdown procedure and do not simulate unsafe failures while connected to hazardous equipment.

## 9. Explicit limitations and outstanding verification

1. **Deployment wiring:** Source alone does not prove which Linux units start Launcher, Updater, Apache, Core or WebBridge; nor ordering, `Restart=` policy, watchdog configuration or privileges.
2. **Source/version provenance:** The bundled source lacks a verified Git commit and installed binary identity. Verify before applying these observations to release 2.0.27 or another field version.
3. **READY activation trigger:** Confirm whether deployed services restart Launcher or otherwise select a newly prepared release, and precisely when.
4. **Graceful stop policy:** Launcher force-kills child process trees. Decide whether cooperative cleanup and output-state handling are required and, if so, verify implementation.
5. **Functional health policy:** A process-alive test can pass even when Core is unresponsive, hardware initialization was partially ineffective, or WebBridge has not connected. Confirm intended release acceptance criteria.
6. **Logger boundary:** Reconcile Core-owned logger code in this snapshot with the separately deployed logger architecture described in later project planning.
7. **Relay safe state:** The `Fail-Safe State` configuration was not observed in a fault-handling path in reviewed Core code; do not promise guaranteed transition on crashes or power loss.
8. **Reconnection tests:** Measure client recovery through actual Core/bridge restarts, including cache freshness and pending-command failure semantics.

These are **verification tasks**, not automatically confirmed product defects.

## 10. Source map

Paths in this section are relative to the **development-only** source root `project/sources/SigCore-Software-Source/`. They are not customer-device paths and should not be used as links to files in the customer distribution ZIP.

| Behavior | Reviewed code |
|---|---|
| Core entry, initialization and TCP accept | `Server/SigCoreServer/Program.cs`, `SigCoreServer.cs`, `ClientSession.cs` |
| Hardware configuration, I/O/PID/logger loops | `libs/SigCoreCommon/HardwareManager.cs` |
| Core restart/shutdown commands | `Server/SigCoreServer/CommandDispatcher.cs`, `OsManager.cs` |
| Managed reconnection, re-handshake and re-subscription | `libs/SigCoreCommon/SigCoreSystem.cs`, `SigCoreClient.cs` |
| WebBridge service startup, cache and teardown | `WebBridge/Program.cs`, `CoreState.cs` |
| Chart history and settings | `WebBridge/ChartRecorder.cs`, `Endpoints/ChartEndpoints.cs` |
| Web API reboot | `WebBridge/Endpoints/RestartEndpoints.cs` |
| Launcher program exit, child start/stop, probation and supervision | `SigCoreLauncher/Program.cs`, `Launcher.cs`, `ReleaseManager.cs` |
| Release-state enum, storage and locks | `libs/SigCoreCommon/Update/ReleaseState.cs`, `ReleaseStateStore.cs` |
| Update polling, signing and staged preparation | `SigCoreUpdater/Updater.cs`, `VersionInstaller.cs`, `ScuVerifier.cs`, `ManifestValidator.cs` |
| One-time update installation tasks | `SigCoreUpdater/InstallationTaskRunner.cs` |
| Optional event reporting | `SigCoreUpdater/UpdateEventReporter.cs` |

## 11. Related KB chapters

- [Software Architecture](../Architecture/Software-Architecture.md): system-level component ownership, interfaces, and persistence.
- [System Architecture](../Architecture/System-Architecture.md): hardware/software relationship.
- [Relay Outputs](../Hardware/Relay-Outputs.md) and [Analog Outputs](../Hardware/Analog-Outputs.md): physical output mapping and startup safety considerations.
- [Digital Inputs](../Hardware/Digital-Inputs.md) and [Analog Inputs](../Hardware/Analog-Inputs.md): sampling and configuration semantics.
- [AI Instructions](../AI-Instructions/Instructions.md): how consuming AI models should distinguish code observations from verified customer behavior.
