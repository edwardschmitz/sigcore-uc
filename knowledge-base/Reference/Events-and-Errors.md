---
title: "Events and Errors"
status: source-draft-review-required
hardware_revisions: "Rev B system context"
software_versions: "Supplied unversioned Core/WebBridge/Launcher/Updater snapshot"
source_review_date: 2026-11-06
customer_release_approved: false
---

# Events and Errors

> **Event and error interpretation reference, not a complete stable error-code registry.** A Core TCP `Error` message, an HTTP status, a structured application log entry, and release telemetry describe **different things**. None alone establishes the physical condition of connected equipment. The implementation-derived names below must be checked against the installed build before being treated as an external compatibility contract.

## 1. Identify the message source before interpreting it

| Mechanism | Where seen | Source / meaning | What it does **not** prove |
|---|---|---|---|
| Core protocol `Error` | TCP **7020**, `Command=8` | Failure handling in `ClientSession`/`SigCoreMessage.CreateError` | No universal HTTP mapping; no physical I/O feedback |
| Core/managed alerts | TCP subscribed session | DI, AI, relay, AO and PID state change notifications | Not a persistent, lossless event history |
| WebBridge HTTP response | `/api/*.php` through local bridge | Request validation, Core connectivity, command-send results | `accepted:true` does not confirm a relay moved or a voltage appeared |
| Structured application log | Component text/system log | Category, event code, message; often exception context | Does not guarantee data reached a SQL logger |
| Database logger snapshots | Configured SQL destination | Periodic captured state, if running/configured | Not same data as WebBridge Chart Recorder; no complete history guarantee |
| Updater/Launcher dispositions | Installed `releases.json` | `STAGING`, `READY`, `GOOD`, `FAILED` | `GOOD` is process-survival probation, **not** a health/IO safety certification |
| Update telemetry | Device-initiated update service | Update progress / health events per implementation | Never interpret as measurements, I/O states or complete operational logs |

For where each mechanism runs, see [Software Architecture](../Architecture/Software-Architecture.md), [Logging and History](../Core-Software/Logging-and-History.md) and [Updates and Recovery](../Core-Software/Updates-and-Recovery.md).

## 2. Core TCP error envelope and messages

The server's `ClientSession.RunAsync` wraps dispatcher calls in exception handling. When a handler throws, it constructs:

```json
{"MsgId":123,"Command":8,"Payload":{},"Status":"error","Message":"Cmd:GetRelay ..."}
```

This is **illustrative**: the tail of `Message` is constructed from the exception text and is not a stable machine-readable error code. `MsgId` is the failing request's ID. `Command=8` corresponds to `MsgType.Error` in the provided protocol enum; `Status` is set to the literal string `error`; `Payload` is empty in `CreateError`. Do not parse a particular substring of `Message` as a guaranteed public API contract. Log exception detail may reveal device internals; avoid propagating unredacted log text to public interfaces.

**Transport failure is distinct from protocol error.** A TCP reset, timeout, watchdog disconnect or network interruption can occur with **no Core `Error` reply**. In particular, Core handlers for `SetRelay`, `SetAOut`, some other setters, and `Subscribe` return `null` rather than a success message; lack of a response on these paths does not itself imply failure. Conversely, a client-side send completing does not prove Core applied the output. For integrations, re-establish connectivity and query current state before a risky retry. See [Protocol and Data Reference](Protocol-and-Data-Reference.md).

## 3. Core state notifications

| `MsgType` / numeric value | Payload form in supplied code | Interpretation |
|---|---|---|
| `DInChangeAlert` / **38** | `{"values":[0,1,...]}` with eight entries | Processed DI snapshot; values encoded as integer **0 or 1** |
| `AInChangeAlert` / **39** | `{"values":[number,...]}` with four entries | Configured AI measurement values from the Core pipeline |
| `RelayChangeAlert` / **40** | `{"channel":N,"value":true}` | Updated logical relay state; no contact sensing |
| `AOutChangeAlert` / **41** | `{"channel":N,"value":number,"auto":boolean}` | Output software value/automatic flag, no measured terminal feedback |
| `PIDCurValChangeAlert` / **42** | PID value object plus `channel` | PID controller update; inspect full version-matched schema |

`Subscribe` is numeric **37**. The server registers subscribers and removes them when a session closes. Alerts may be absent during disconnection or a process restart and should not be treated as guaranteed once-only delivery. A reconnecting client should fetch an authoritative current state and rebuild subscriptions. Logical/physical distinctions are documented in [Relay Outputs](../Hardware/Relay-Outputs.md), [Analog Outputs](../Hardware/Analog-Outputs.md) and [Status and Diagnostics](../User-Interfaces/Status-and-Diagnostics.md).

## 4. Device website HTTP outcomes

| HTTP status / field | Common implementation meaning | Recommended handling |
|---|---|---|
| **200** with state JSON | Request succeeded at HTTP layer; read response semantics | Check `connection.connected`; compare response to expected time/source |
| `accepted:true`, `confirmed:false` | Bridge passed a write request to managed Core client | Treat as **sent**, not physically verified; obtain feedback before safety-critical continuation |
| **400** | Missing or wrong JSON type, invalid channel, malformed request | Correct request; do not blindly retry same invalid body |
| **404** | Route, kind or particular resource not found | Check route and version, not network state alone |
| **502** | Core operation failed or gateway/upstream request timed out | Query connectivity and current state; write may have reached Core before timeout |
| **503** | Core disconnected/unavailable | Reconnect, wait for Core readiness, distinguish stale WebBridge cache |
| **500** | Endpoint/service-specific error, including OS restart permission failure | Inspect the corresponding component log and OS service rights |
| JSON `{"error":"..."}` | Human-readable error summary | Log a redacted summary and use HTTP status; do not assume fixed text grammar |

The reviewed `EndpointHelpers.ExecuteRead/ExecuteWrite` implements 400 for `ArgumentException`, 503 for `ConnectionState != Connected`, 502 for unexpected Core operation errors. Other endpoint families have their own codes, validation and error body forms. The PHP proxy can add a separate failure mode for loopback connection or timeout, and public PHP route availability differs from private WebBridge route availability. See [HTTP API and WebBridge](../Programming/HTTP-API-and-WebBridge.md).

**Examples of precise validation:** `channel` must be an integer within `0..7` or `0..3`, according to the kind; relay `state` must be JSON boolean; AO `value` must be a finite JSON number. Configuration PUT is generally a complete per-channel record, not a partial PATCH. When a write times out, determine actual logical/physical state before retrying rather than issue repeated commands that may change the machine.

## 5. Structured application logs: observed event names

In the reviewed code, structured calls use category/code/message patterns such as the following. These are **observed implementation identifiers, not a frozen complete catalog**. Do not build hard-coded client logic around them without a version-specific contract.

| Source | Category / event code (as written) | Typical meaning |
|---|---|---|
| `HardwareManager.cs` | `Sampling / AnalogInputLoopFailure` | Analog acquisition loop failed; the loop may re-enter and fail again |
| `HardwareManager.cs` | `Sampling / DigitalInputLoopFailure` | DI sampling/processing task failed |
| `HardwareManager.cs` | `Sampling / PidLoopFailure` | PID loop execution failed |
| `HardwareManager.cs` | `Sampling / LoopStopFailure` | Loop stop/dispose path failed |
| `HardwareManager.cs` | `Configuration / MissingAnalogInputConfig`, `MissingDigitalInputConfig`, `MissingAnalogOutputConfig`, `MissingRelayConfig`, `MissingPidConfig` | Missing expected configuration branch or entry |
| `ClientSession.cs` | `Protocol / DispatchFailed` | Dispatcher threw for a received command; error reply attempted |
| `ClientSession.cs` | `Session / SessionFailed`, `SendFailed`, `Disconnected` | Network session lifecycle/failure |
| `SigCoreClient.cs` | `Connection / Connected`, `ConnectFailure` | Client-side connect outcome; not I/O validation |
| `CommandDispatcher.cs` | `Subscription / Subscribed`, `SubscribeRepeated`, `SubscriberRemoved` | Subscription registered, redundant request, or cleanup |
| `EndpointHelpers.cs` | `API / ReadFailure`, `WriteFailure` | WebBridge operation exception |
| `EndpointHelpers.cs` | `Configuration / WriteValidationFailure` | Client-submitted write data failed validation |

Search logs by **timestamp, component, severity, category and event code**, then correlate with connection state and the specific I/O channel. Absence of a recent error is not proof of accurate measurements. Components may log at different levels or to separate OS/process destinations; log path, rotation and collection depend on installation. Do not publish SQL credentials, Wi-Fi PSKs or full configuration dumps in diagnostic transcripts.

## 6. Update, installation and recovery states

| Name | Evidence-backed meaning | Condition that remains unverified |
|---|---|---|
| `STAGING` | Update preparation begun but not complete | Not safe for active selection |
| `DOWNLOADED` | Telemetry event after downloaded/validated package handling in updater | Not proof of activation |
| `READY` | Verified package and executable structure prepared on disk | Not proof Launcher has selected or started it |
| `GOOD` | Selected `READY` release survived **30-second** Launcher process-exit checks | Not proof field hardware is healthy or I/O functions were exercised |
| `FAILED` | Candidate failed startup or probation | Does not itself restore an arbitrary external machine's state |
| `REGRESSED` | Appears in broader release design/telemetry discussions | Actual deployed manual-regression selection must be verified |

A `GOOD`/`FAILED` event is **release management**, not the same as logging an application exception. Updater and Launcher can record distinct transitions at different times. See [Updates and Recovery](../Core-Software/Updates-and-Recovery.md).

## 7. Diagnostic decision paths

1. **Cannot discover device:** check link, IP/DHCP, mDNS visibility, then TCP 7020. mDNS advertisement does not prove Core ready.
2. **Connected but no fresh measurements:** distinguish cached WebBridge `/api/state` from fresh `/api/status` and check sampling logs, channel configuration and sensor voltage.
3. **Command accepted but actuator did not operate:** verify UI state, inversion/mode, actual output voltage or relay contact, independent load supply and circuit; do not equate software readback with physical confirmation.
4. **History absent:** check Chart Recorder RAM lifecycle/restart and recording interval; distinguish SQL logger state/retention.
5. **Update appears ready but old version running:** `READY` is not selection; inspect active symlink/Launcher and release state rather than immediately forcing an update.
6. **Network/config restore response accepted but device inaccessible:** restart or OS network apply may continue after HTTP response; recover with the approved service procedure.

See [Troubleshooting](../Operations/Troubleshooting.md). Outstanding version-dependent defects belong in [Known Limitations](Known-Limitations.md); do not present them as permanent product behavior after a fix is confirmed.

## 8. Source traceability

`libs/SigCoreCommon/SigCoreProtocol.cs`, `SigCoreClient.cs`, `HardwareManager.cs`, `Log.cs`; `Server/SigCoreServer/ClientSession.cs`, `CommandDispatcher.cs`; `WebBridge/Endpoints/EndpointHelpers.cs`, `StatusEndpoints.cs`, `BackupRestoreEndpoints.cs`, `RestartEndpoints.cs`; `SigCoreUpdater/Updater.cs`, `SigCoreLauncher/Launcher.cs`. The precise event list and semantics must be rechecked against the deployed build.
