---
title: ".NET API"
status: source-draft-review-required
hardware_revisions: "Rev B"
software_versions: "Supplied unversioned SigCoreCommon source snapshot; verify shipping release"
source_review_date: 2026-11-06
customer_release_approved: false
---

# .NET API

> **Engineering source draft, not a certified API contract.** The published API Reference is the primary customer-oriented reference; the supplied `SigCoreCommon` implementation is supporting evidence. See [Discovery and Connections](Discovery-and-Connections.md), [Software Architecture](../Architecture/Software-Architecture.md), and [HTTP API](HTTP-API-and-WebBridge.md). APIs and behaviors below belong to the reviewed source snapshot, not necessarily the later installed release.

## 1. Library, network, and ownership

- Namespace: `SigCoreCommon`, with configuration payload types in `SigCoreCommon.Payloads`. The concrete client façade is `SigCoreSystem`; device discovery is `SigCoreDiscovery`.
- Primary Core endpoint: TCP **7020** with a proprietary JSON-based message protocol. The managed library handles framing, handshake, subscriptions, request correlation, and reconnect. Application code should not invent a raw TCP framing format.
- Supported channels in Rev B: 8 digital inputs (`0..7`), 8 relays (`0..7`), 4 analog inputs (`0..3`), 4 analog outputs (`0..3`), 4 PID controllers (`0..3`). Physical `DI1`, `R1`, `AI1`, `AO1` and `PID1` correspond to program index `0`.
- Typical host is the current device hostname/IP; use the actual discovered reachable address or a known address. Device discovery reports mDNS advertisements but only verifies initial TCP reachability.
- Build requirements, runtime target, and distribution of the DLL plus its dependencies must be verified against the **customer-shipped example/application**; this draft does not claim a separately installable NuGet package.

## 2. Managed connection lifecycle

`new SigCoreSystem()` creates the client. `await StartAsync(string ip, int port = 7020, int timeoutMs = 5000)` starts an owned connection loop and awaits first successful handshake and subscription. The object continues to reconnect until `Stop()`/`Dispose()`. `ReconnectDelay` defaults to one second.

`ConnectionState` is one of `Disconnected`, `Connecting`, `Handshaking`, `Subscribing`, `Connected`, `Reconnecting`, or `Faulted`. `IsConnected` describes the underlying **TCP** transport; prefer `ConnectionState == SigCoreConnectionState.Connected` to indicate application-level readiness. A protocol rejection can put the managed connection in `Faulted` rather than indefinitely retrying. `ConnectionStateChanged` and `ConnectionWarning` provide change notifications.

There is also an explicit `ConnectAsync(string ip, int port = 7020, int timeoutMs = 5000)` returning `ConnectStatus` (`Connected`, `TcpFailed`, `ProtocolRejected`, `Timeout`) and a `Close()` method, but use the managed `StartAsync`/`Stop` pattern when automatic reconnection is required. Do not simultaneously treat the unmanaged and managed patterns as independent, concurrent connections on the same object.

**Minimal read-only example (illustrative; compile against the shipped library):**

```csharp
using System;
using System.Threading.Tasks;
using SigCoreCommon;

var unit = new SigCoreSystem();
try {
    await unit.StartAsync("192.0.2.10"); // Replace with the controller's actual address.
    if (unit.ConnectionState != SigCoreConnectionState.Connected)
        throw new InvalidOperationException("Core is not ready.");
    bool di1 = await unit.GetDInAsync(0);
    double ai1 = await unit.GetAnalogInputAsync(0);
    Console.WriteLine($"DI1={di1} AI1={ai1}");
} finally {
    unit.Stop();
    unit.Dispose();
}
```

`192.0.2.10` is a **documentation-only example address**, not a default device IP. The code does not toggle any outputs. Subscribe to connection-state events and handle interruptions in long-running applications. Do not invoke UI updates from device events without marshaling to the UI thread; event callbacks can run on background threads.

## 3. Value reads and writes

| Function | Reviewed method(s) | Result / caveat |
|---|---|---|
| Digital input | `GetDInAsync(uint channel)` | `Task<bool>`; debounced logical input state |
| Relay output | `GetRelayAsync(uint channel)`; `SetRelayAsync(uint channel, bool state)` | Logical state and command. Readback is Core software state, **not** independent contact feedback. Setter completion indicates transport send, not physical verification |
| Analog input | `GetAnalogInputAsync(uint channel)` | `Task<double>`; configured processed/scaled reading |
| Analog output | `GetAOutValueAsync(uint channel)`; `SetAOutValueAsync(uint channel, double value)` | Configured output value; setter is a command without physical acknowledgment |
| PID state | `GetPIDCurValAsync(uint channel)` | Named tuple `(setPoint, processVariable, output, tolerance, autoMode, deviation)` |
| Ping | `PingAsync(int timeoutMs = 1000)` | `Task<bool>`; protocol responsiveness, not wiring/IO verification |
| Status | `GetStatusAsync()` | `Task<JObject>` from Core; use as fresh status when reachable |
| OS/device | `RestartAsync()`; `Shutdown()` | Device-level actions with operational consequences; confirm before invoking |

`SetRelayAsync`, `SetAOutValueAsync` and most configuration/mode setters use a **send-only** path. A completed task proves the message was sent to the client transport, not that a relay contact moved or that analog voltage attained its target. A subsequent read is useful for confirming Core logical state, but external hardware feedback is needed to independently verify a physical load.

The currently reviewed relay inversion bug is described in [Relay Outputs](../Hardware/Relay-Outputs.md). Do not assume this defect still exists after the separately planned fix is installed; verify the release.

## 4. Configuration methods

Each getter returns a typed `SigCoreCommon.Payloads` object backed by JSON; setters generally send a complete configuration payload, not a patch. Read existing values, update intended fields, validate, send, and reread. Do not assume atomic compare-and-swap or that another client cannot update the same channel concurrently.

| Component | Getter | Setter |
|---|---|---|
| DI | `GetDInConfigAsync(uint)` | `SetDInConfigAsync(DInConfigPayload)` |
| Relay | `GetRelayConfigAsync(uint)` | `SetRelayConfigAsync(uint, RelayConfigPayload)` |
| AI | `GetAInConfigAsync(uint)` | `SetAInConfigAsync(AInConfigPayload)` |
| AO | `GetAOutConfigAsync(uint)` | `SetAOutConfigAsync(AOutConfigPayload)` |
| PID | `GetPIDConfigAsync(uint)` | `SetPIDConfigAsync(PidConfigPayload)` |
| Global/system | `GetGlobalConfigAsync()` | `SetGlobalConfigAsync(SystemConfigPayload)` |
| Network | `GetNetworkConfigAsync()` | `SetNetworkConfigAsync(NetworkSettingsPayload)` |
| Logger | `GetLoggingConfigAsync()` | `SetLoggingConfigAsync(LoggerConfigPayload)` |

Payload types and fields are implemented under `libs/SigCoreCommon/Payloads/`. Examples: relay payload has `Channel`, `Name`, `DefaultState`, `FailSafeState`, `InvertedLogic`; DI has naming/debounce/inversion; AO includes mode, scaling, and display preferences; PID contains gains, routing, output/PV limits and mode. See individual Hardware/Core chapters for actual semantics, limits and implementation gaps. **Never infer that a stored configuration field produces the advertised fault or safety behavior without runtime evidence.**

**Example of preserving an existing relay configuration before editing it:**

```csharp
var relay = await unit.GetRelayConfigAsync(0);
relay.Name = "Pump Ready";
await unit.SetRelayConfigAsync(0, relay);
var reread = await unit.GetRelayConfigAsync(0);
if (reread.Name != "Pump Ready")
    throw new InvalidOperationException("Configuration was not confirmed.");
```

Run only where configuration changes are authorized and safe. Relay polarity changes may alter real outputs. The `SetRelayConfigAsync` call overwrites the payload's channel from its explicit `channel` argument.

## 5. PID control

`SetPIDSPAsync`, `SetPIDOutputAsync`, `SetPIDTolAsync`, `SetPIDAutoAsync`, `SetPIDRampTimeAsync`, `SetPIDRampTargetAsync`, `SetPIDRampAsync`, and `ResetPIDAsync` accept a zero-based PID channel (`uint`); setters except `ResetPIDAsync` also take a value (`double` or `bool`). PID configuration uses `GetPIDConfigAsync`/`SetPIDConfigAsync`. Setpoint, process value and output are not interchangeable; a PV may be routed from an AI, while PID output may command an AO. Configuration and operating modes require safety review for the controlled process. See [PID Control](../Core-Software/PID-Control.md).

## 6. Subscriptions and events

The library exposes `DigitalInChanged` (`bool[]`), `AnalogInChanged` (`double[]`), `RelayChanged` (`uint,bool`), `AnalogOutChanged` (`uint,double,bool`), and detailed `PIDSPChanged`, `PIDPVChanged`, `PIDOutputChanged`, `PIDTolChanged`, `PIDDevChanged`, `PIDAutoChanged`, `PIDRampTimeChanged`, `PIDRampTargetChanged`, `PIDRampChanged`, plus `LoggerStatusChanged`. `SubscribeAsync` registers server alerts; managed `StartAsync` already includes subscription and re-subscription on reconnect.

An alert means Core reported changed software state. A client can also poll request/response methods for a fresh value. Neither strategy is independent proof of physical I/O conditions. On reconnect, acquire a fresh snapshot rather than assuming every intermediate event was buffered.

## 7. Diagnostics, error handling and boundaries

- Request/response getters can throw on disconnect, timeout, malformed payload or server error. Catch appropriately and decide whether to retry only after checking connection state; do not automatically repeat state-changing writes unless idempotence is established.
- Keep logging credentials and network PSKs out of application diagnostics, source control, and distributed examples.
- Do not rely on `IsCommander` alone as proof of device-side authorization; check protocol/server enforcement for the release under test.
- If a value matters for equipment safety, use independent measurement/interlocks; the API is not a certified safety system.
- Beware multiple clients commanding one output or multiple PIDs routed to one AO; single-writer ownership is an application design responsibility.

## Evidence and verification

Primary implementation: `libs/SigCoreCommon/SigCoreSystem.cs`, `SigCoreClient.cs`, `SigCoreDiscovery.cs`, `Payloads/*.cs`, `SigCoreProtocol.cs`. Published human reference: `project/sources/documentation/api-reference.html` (product website reference snapshot). Confirm compiled customer DLL and published Python/C# examples **actually ship with the installation**; no source-level example project is proved by the supplied snapshot. This chapter is a developer contract **draft** until API signatures and behavior are checked against the installed release.
