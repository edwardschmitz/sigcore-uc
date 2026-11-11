---
title: "Integration Patterns"
status: source-draft-review-required
hardware_revisions: "Rev B"
software_versions: "Supplied unversioned WebBridge/Core/SigCoreCommon snapshot; verify deployed release"
source_review_date: 2026-11-06
customer_release_approved: false
---

# Integration Patterns

> **Engineering integration guidance, not an assurance of safety certification or a guarantee of any particular deployed interface.** Describes patterns supported by source-reviewed primitives. For exact method names, route payloads and limitations, see [.NET API](DotNet-API.md), [HTTP API](HTTP-API-and-WebBridge.md), and [Discovery and Connections](Discovery-and-Connections.md).

## 1. Choose an integration boundary

| Integration goal | Appropriate boundary | Notes |
|---|---|---|
| Low-latency interactive controller application | Managed `.NET SigCoreSystem` over TCP 7020 | Uses request/reply and notifications; background reconnect/resubscription; multiple clients must coordinate writes |
| Lightweight device website integration | PHP `/api/*.php` over HTTP | Backed by loopback WebBridge; no assumption of HTTPS/authentication beyond installed server/network policy |
| Python read-only capture, dashboards | HTTP GET `/api/state.php` and chart-history | Cached state/recorder history; choose meaningful timestamps, freshness validation |
| Time-series record for later analysis | Configured SQL logging | Separate from web chart RAM; verify logger deployment/schema and database access policy |
| Monitoring health rather than I/O | Core `GetStatusAsync()` or PHP `/api/status.php` | Fresh Core status (requires connection); distinct from cached `/api/state.php` |
| Hardware fault detection | External safety chain plus dedicated feedback sensor | Not guaranteed by software output `accepted`/logical `ON` |

Core owns all I/O and PID execution. WebBridge is a **client**, not a second controller performing independently persistent PID control. A laptop or browser does not need to remain running for an already configured Core PID to execute; actual enable/auto state and safety requirements still matter. All access paths reach the same physical channels and must not be treated as isolated namespaces.

## 2. Read-only telemetry/dashboard

1. Discover or otherwise specify the correct device, then establish the chosen interface connection.
2. Acquire a complete snapshot; annotate with the **application's receive time** and connection state. A receive timestamp is not the acquisition timestamp for a cached measurement.
3. If Core disconnects, explicitly label the last values as stale and stop making control decisions based on them.
4. For change notifications (`SigCoreSystem`), subscribe and update only changed values, but refresh a complete state after reconnection.
5. For HTTP polling, choose an interval appropriate to operator needs and network load; do not assume hardware sampling frequency equals HTTP update frequency.
6. Preserve unit/scaling metadata when exporting values. An engineering-unit AI reading is not necessarily raw ADC voltage.

The device website's Chart Recorder records four AI channels periodically in WebBridge RAM, **1 s by default**. It resamples when returning history. Database logging has a different interval, scope and retention; do not label chart-history output a permanent audit trail. See [Logging and History](../Core-Software/Logging-and-History.md).

## 3. Closed-loop PID supervision

Use a validated AI channel as process variable, a PID controller in Core, and a compatible AO as output. Configure sensor range/calibration, output electrical mode/limits, PID routing, gains, setpoint, automatic enable and output destination consistently. Confirm that the actuator, transducer, and supplies are electrically compatible (for example, never assume a DAC output can directly energize a heater or motor). The full loop is described in [PID Control](../Core-Software/PID-Control.md) and Actuators and Loads (planned chapter).

A supervisory application may read PV, setpoint, output and mode through `.NET` or HTTP. Keep supervisory logic distinct from the Core loop: an application can show or adjust the loop without having to emulate its high-frequency PID calculation. Protect changes with authorized operator workflows and bounds that reflect the actual process. **Do not automate PID writes based only on the cached WebBridge connection flag.**

The reviewed source allows output routing configurations but has not demonstrated prevention of two PIDs driving the same AO. Treat a channel as **single-owner** unless the deployed release explicitly implements arbitration; verify controller priorities before enabling loops.

## 4. Discrete supervisory control (relay and DI)

A common pattern is a DI command/permit input and a relay-operated external device. DI input inversion and debounce create the **reported logical state**; relay inversion transforms commanded logical state to hardware drive; `RelayChanged` is not dry-contact feedback. Where a motor/pump/valve must be verified, wire a separate contact/auxiliary switch or other actual feedback into a DI and compare commanded and observed states.

- Identify the appropriate COM/NO/NC relay contact and load limits from the hardware reference.
- Check supply isolation and grounding before any wiring; the DI transistor stage is **not galvanically isolated** even if schematic labels suggest otherwise.
- Define what must happen when Core, WebBridge, the Pi or the external supply stops.
- Design independent emergency-stop and fail-safe circuitry where required. The reviewed source did not establish runtime application of the configured `failSafeState` field.
- Coordinate writes so two interfaces cannot simultaneously issue contradictory relay commands. Add explicit operator authority, timeouts, and expected-state readback in the supervisory application.

If the device is controlling hazardous machinery or material, do not rely on network connectivity and application logic as the sole safety barrier.

## 5. Capture, record and export

- **Live read:** `GetAnalogInputAsync` (native .NET) for a fresh request/response; `AnalogInChanged` for live alerts; `/api/state.php` for bridge cache.
- **Recent chart:** `/api/chart-history.php` provides four resampled AI tracks. Its `start` and `length` are Unix milliseconds, data timestamp array is Unix seconds, and missing values are JSON `null`.
- **Long-term history:** SQL logger may persist configured snapshots. Verify current logger process, tables, permissions and credentials against the installed revision. A local WebBridge reboot clears its RAM chart but does not necessarily clear SQL data.
- **Exports used for analysis:** include device serial, software version, hardware revision, channel number, configuration/scaling, source timestamp provenance, units, and any resampling method, as available. Do not misrepresent interpolated chart points as raw ADC conversions.

For data acquisition of changing analog signals, account for multiplexing and sampling/averaging, which are documented in [Measurement and Control Timing](../Core-Software/Measurement-and-Control-Timing.md) and [Analog Inputs](../Hardware/Analog-Inputs.md). The older reviewed AI acquisition loop is not guaranteed to match the more recently changed moving-average implementation.

## 6. Device communications, retries and orchestration

A robust integration should distinguish five states: **discoverable**, **TCP reachable**, **protocol connected**, **Core reporting healthy**, and **external process physically responsive**. These cannot be collapsed into a single `online` bit. For `.NET`, use `SigCoreSystem.ConnectionState`; after a reconnect fetch current state rather than replaying buffered writes. For HTTP, check the HTTP status and `connection` content, and recognize that a proxy `502` may come from WebBridge being temporarily unavailable rather than the Core rejecting the request.

Treat commands as possibly delivered if a write times out: always retrieve state and decide whether retrying is safe. Writes to relays and AO values may be idempotent at the logical-command level, but subsequent changes by other actors mean repeated sends can still be disruptive; configuration saves can change or reset outputs. Use bounded request timeouts, avoid unlimited retry storms, and log outcomes **without secrets**. If an application changes network settings, explicitly plan for the connection to drop and the device address to change.

## 7. Multiple clients and authority

The Windows Control Panel, browser/device website, a .NET program and a separate Python application may all connect to the same Core. Their writes target the same channels, not replicated per-client state. SigCoreCommon exposes `IsCommander`; actual server-side privilege enforcement and arbitration need release-specific verification before being treated as a security boundary. Application integration should implement one explicit operator/controller owner per writable channel or process, especially when browser and API users coexist. This is a system integration rule, not a claim that Core implements a distributed lock.

Recommended behavior: read-only clients freely observe, while one authorized controller owns each output or PID parameter. If a second controller must take over, define handoff procedures and permitted fallback states. Always decide what happens when the owner crashes, because `accepted=true` cannot substitute for a physical fail-safe design.

## 8. Illustrative application designs

**Monitoring-only instrument panel:** Four AIs -> Core calibration -> WebBridge cached state or .NET events -> UI; optionally request one-hour chart history. Do not change field outputs.

**Thermal-loop demonstrator:** Thermocouple transmitter (approved conditioning into a configured AI) -> Core PID -> compatible AO/PWM interface -> suitably rated external power stage/heater. Account for independent overtemperature cutout; any 12 V heater must be supplied by an appropriate external stage, not DAC circuitry. Check PID routing/output and safe startup. This is a conceptual system design, not a validated appliance schematic.

**Supervised vacuum sequencing:** DI status of valves/switches, relay commands to externally appropriate interposing drivers, AI telemetry and heater/chiller PID. Build interlocks as an explicit state machine backed by actual sensor feedback; do not assume software modes alone enforce a safe sequence. The separate vacuum-sequencer runtime in the source snapshot is application-specific and must not be implied to be a standard supported product feature.

**Public interactive demo:** A marketing website can redirect to an isolated demo unit, but internet-accessible write paths have a different risk profile. The user's planned demo expressly permits visitors to operate example I/O; registration is lead capture, not an access-control boundary. Treat segmentation, exposure review, rate limiting, secrets protection, and safe demo loads as deployment requirements, not as guaranteed features of the supplied source.

## 9. Documentation and verification rules

- Do not create a nonexistent `sigcore` Python package import or claim direct unauthenticated API access is officially supported.
- Keep direct `.NET` calls, internal loopback WebBridge endpoints and public PHP proxies distinct.
- Refer to native source paths and this KB for verified field semantics. Do not conflate physical, logical, commanded and confirmed states.
- Treat `Rev C`, version-selection architecture updates and later plans as **planned**, not present features in Rev B unless verified in an installed build.
- In examples, avoid real device credentials and use reserved documentation IP addresses. Any operator-directed command with real output side effects must be labeled clearly.

## Evidence and verification

`libs/SigCoreCommon/SigCoreSystem.cs`, `SigCoreDiscovery.cs`, `SigCoreClient.cs`; `WebBridge/Program.cs`, `CoreState.cs`, `ChartRecorder.cs`, `Endpoints/*.cs`; `UpdaterFiles/www/api/*.php`; `project/sources/documentation/api-reference.html`. Confirm customer installation distribution of completed C#/Python examples, current authentication policy, deployed logger topology and release compatibility before making customer-facing guarantees.
