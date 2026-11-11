---
title: "External System Integration"
status: source-draft-review-required
hardware_revisions: "Rev B"
software_versions: "Supplied unversioned source; verify current installed build and exposure policy"
source_review_date: 2026-11-06
customer_release_approved: false
---

# External System Integration

> **Engineering draft.** Integrations depend on the deployed version, network exposure and installed services. The presence of an endpoint in source code is not a guarantee of public accessibility or authorization. External systems should distinguish **observation, command, configuration, and durable history**.

## 1. Integration boundaries and intended uses

| Integration need | Interface available in reviewed source | What it provides | Avoid assuming |
|---|---|---|---|
| Managed C#/.NET process | `SigCoreCommon.SigCoreSystem` -> Core TCP **7020** | Discovery, requests, I/O writes, config, PID, event subscriptions, reconnect | Generic raw JSON-over-TCP framing or stable protocol details without library/reference |
| Browser/Python/HTTP client | Apache `/api/*.php` -> WebBridge `127.0.0.1:8080` -> Core | Cached state, current status, selected writes/configuration and chart history | An Internet-safe authenticated HTTPS API by default |
| Long-term analysis / analytics | Separately configured MySQL logging | Recorded state snapshots when enabled and working | Real-time control or lossless per-conversion AI archive |
| Direct access to field hardware | **Not an integration API** | Hardware is owned by Core and its I²C drivers | Independent external I²C/GPIO access without interfering with Core |
| Modbus / MQTT / OPC UA / fieldbus | **No built-in service established in reviewed source** | Potentially implemented via an external bridge | Automatic protocol compatibility or certified server profile |

Use **one integration boundary** for a given application and document version and security assumptions. Externally written bridges may translate between SigCore API values and an industry protocol, but they must implement their own semantics for units, quality, timestamps, authorization and write failure.

## 2. Network identity and discovery

`SigCoreDiscovery` advertises/discovers `_sigcore._tcp.local.` records and returns hostname, identifiers, address candidates, version and service port metadata. Actual API commands connect to Core's TCP **7020**. A successful mDNS query does not prove that the selected address is reachable or that Core finished initializing. Discovery can fail across VLANs while direct TCP may still work, depending on routing and firewall rules.

Suggested external client connection lifecycle:

```text
Known host/IP or mDNS discovery
 -> choose a routable address
 -> connect to expected service
 -> complete application handshake and subscription
 -> fetch fresh baseline state
 -> operate / monitor with explicit stale-data policy
 -> on disconnect: mark data stale, reconnect, reacquire baseline
```

A changed DHCP address can invalidate an old connection; do not hard-code the example `192.0.2.10` as a factory default. Preserve serial number as a stable *device identifier* when available, but verify it against the device after reconnection.

## 3. Read-only HTTP observation

For a website-enabled device, the browser-facing route is **`/api/state.php`**, which returns a **cached** WebBridge state. `/api/status.php` requests fresh Core status, while `/api/chart-history.php` accesses volatile, resampled AI history. The internal WebBridge paths `/api/state`, `/api/status`, and `/api/chart/history` normally listen on **loopback**; clients on the LAN use the PHP proxy instead of assuming port 8080 is public.

Example monitoring call, **read-only**:

```bash
curl --fail-with-body --max-time 5 'http://DEVICE-ADDRESS/api/state.php'
```

Check `connection.connected` before treating the cache as live, and carry a client receive timestamp clearly labeled as such. Connection readiness is not the same as actual actuator response. A returned `analogInputs` value is a *configured engineering reading*; a returned `relays` Boolean is **logical software state**, not contact position. See [Data Acquisition and Monitoring](Data-Acquisition-and-Monitoring.md).

## 4. Command requests and confirmation semantics

The source's public PHP proxy forwards controlled operations, including:

| Purpose | Browser-facing route | Payload example | Meaning of success |
|---|---|---|---|
| Relay logical state | `POST /api/control.php?operation=relay` | `{"channel":0,"state":false}` | Request accepted for Core transport; no physical contact feedback |
| Analog output | `POST /api/control.php?operation=analog-output` | `{"channel":0,"value":2.5}` | Requested configured AO value, not measured terminal volts |
| PID setpoint | `POST /api/control.php?operation=pid&channel=0&action=setpoint` | `{"value":10.0}` | Request accepted, not process response |

**These are state-changing commands and must not be used on a live process without authorization, validated safe bounds and an explicit command owner.** The WebBridge API typically reports `accepted:true, confirmed:false`, which is not an actuator acknowledgment. Retrying blindly after a network timeout can repeat a command that already reached Core. Determine command and observed state before reissue.

Per-channel configuration PUTs require **complete configurations**, not sparse patches, while selected system/network/logger settings use different partial-update semantics. A config save can interrupt an AO signal or change network reachability. Never deploy a generic "write any JSON key" bridge without type-specific validation and readback. See [HTTP API and WebBridge](../Programming/HTTP-API-and-WebBridge.md).

## 5. Security and boundary placement

The supplied source includes network/device settings routes that can expose **stored Wi-Fi and database credentials**. An older logger file contains hard-coded database credentials. These are review findings, not content to reproduce; no API or example should output those secrets. The website proxy's existence does not imply it enforces strong authentication, encryption, command authorization or rate limiting.

For an external integration:

1. Place SigCore and the bridge in an **approved network trust zone** with appropriate routing and firewall controls.
2. Keep WebBridge's private loopback listener private unless a reviewed deployment explicitly changes it.
3. Limit write access at the application/proxy layer; log commands and failures without credentials or sensitive config dumps.
4. Apply TLS/authentication at the appropriate external gateway if access beyond the trusted zone is required; do not claim the supplied on-device HTTP site already provides them.
5. Never treat **email registration, a hidden URL or a stable redirect** as authentication. The proposed public demo expressly permits visitors to operate nonhazardous demonstration I/O, so isolate it from internal systems and use safe dummy loads.
6. Verify the actual shipped security fixes before claiming any exposed configuration or network endpoint is hardened.

This is deployment guidance, not an assertion that a particular VPN, firewall, identity provider or cloud service is supplied by SigCore.

## 6. External supervisory software pattern

A robust bridge maintains independent **read** and **write** concerns:

```text
SigCore Core -> managed .NET client / PHP-WebBridge proxy -> translator
     -> external application data model / dashboard / SCADA

Authorized supervisory command -> validator + command-owner policy
     -> SigCore write API -> Core software state -> independent feedback check
```

Map units and limits deliberately. Example: a building system may expose a pressure transmitter as `kPa` in one place and `psi` in another. Convert once at a documented boundary, and never assume a 0–5 V AO value equals process engineering units when scaling is configured. The same numbered channel may have different display names; use the underlying channel/serial identity for data joins.

If building an MQTT/OPC UA/Modbus adapter, define topic/register/node names, quality/stale flags, conversion, timestamps, writable subset, reconnect behavior and failure modes **inside that external adapter**. Do not imply those protocols are native SigCore services merely because an external bridge can translate them.

## 7. Multi-device integration and uptime

For several units, distinguish serial number, friendly name, hostname and current IP. Apply explicit device-to-process mapping rather than taking the first discovered unit. There is no demonstrated cross-device atomic transaction: a sequence commanding two controllers can partly succeed. Use a supervisory state machine with per-step confirmation and safe recovery, not simultaneous API calls that are assumed to finish together.

For readiness and monitoring keep at least these states separate: **discovered**, **TCP-connected**, **protocol-ready**, **Core status healthy**, **actual external equipment confirmed**. Reacquire baseline values on reconnection. For time-series alignment store observed UTC time, actual source timestamps only when available, and indicate chart interpolation or SQL snapshot intervals.

## 8. Traceability and verification

Implementation evidence: `libs/SigCoreCommon/SigCoreSystem.cs`, `SigCoreDiscovery.cs`, `SigCoreClient.cs`; `WebBridge/Program.cs`, `CoreState.cs`, `Endpoints/*.cs`; `UpdaterFiles/www/api/*.php`; `HardwareManager.cs` and `MYSQL_LOGGER.cs`. Cross-reference [.NET API](../Programming/DotNet-API.md), [Python Integration](../Programming/Python-Integration.md), [Discovery and Connections](../Programming/Discovery-and-Connections.md), [Networking and Access](../Operations/Networking-and-Access.md), [Integration Patterns](../Programming/Integration-Patterns.md).

**Release review:** actual deployed endpoints/auth policy; credential-leak fixes; device ID stability; logger process ownership; vendor-supported versus external-adapter protocols; example distribution; physical output command/feedback semantics. No customer-facing compatibility or secure-Internet-access claims follow from source existence alone.
