---
title: "Discovery and Connections"
status: source-draft-review-required
hardware_revisions: "Rev B controller (device network context)"
software_versions: "Supplied unversioned SigCoreCommon and ControlPanel snapshot; verify with release under test"
source_review_date: 2026-11-06
customer_release_approved: false
---

# Discovery and Connections

> **Source-grounded engineering draft.** This chapter distinguishes automatic *discovery*, TCP *reachability*, protocol *handshake/subscription*, and application *readiness*. They are not interchangeable. See [Software Architecture](../Architecture/Software-Architecture.md) and [Status and Diagnostics](../User-Interfaces/Status-and-Diagnostics.md).

## 1. Network endpoints

| Endpoint | Transport | Purpose | Evidence |
|---|---|---|---|
| `_sigcore._tcp.local.` | mDNS/DNS-SD discovery | Advertises/discovers SigCore on the local link | `libs/SigCoreCommon/SigCoreDiscovery.cs` |
| **7020/TCP** | Custom SigCore protocol | Core control, configuration, subscriptions and status | `Server/SigCoreServer/SigCoreServer.cs`, `SigCoreSystem.cs` |
| **80/TCP** | HTTP through Apache | Human-facing device website and its PHP API | Device site content and planned deployment; installed Apache listen port requires confirmation |
| `127.0.0.1:8080` | Private HTTP | WebBridge API consumed by local PHP proxy | `WebBridge/Program.cs`, `UpdaterFiles/www/api/_bridge.php` |
| MySQL server: configured TCP port | SQL | Optional logger destination; **not** the Core API | Logger settings and PHP Log Viewer |

The source binds Core's listener to `IPAddress.Any` on TCP 7020. This does not automatically establish firewall exposure or safe Internet accessibility. WebBridge explicitly binds to **loopback** `127.0.0.1:8080`; clients on the LAN ordinarily reach its functions through the Apache/PHP website instead. Network segments and firewalls can block discovery even when direct TCP access works. Do not tell customers to expose 7020 directly to the Internet.

## 2. `SigCoreDiscovery` API and selection behavior

In the supplied `SigCoreCommon` library, `SigCoreDiscovery.DiscoverAsync()` resolves `_sigcore._tcp.local.` through Zeroconf and builds a list of `SigCoreDevice` objects. For each advertisement, it collects advertised addresses, extracts a service-port value and optional `version` TXT property, orders IPv4 addresses ahead of IPv4 link-local addresses (`169.254.*` last), and tests **TCP 7020** connection availability with a **1000 ms** timeout per candidate address. Only devices with a reachable selected IPv4 address are returned.

`SigCoreDevice` reports:

| Field | Meaning |
|---|---|
| `Name`, `Id` | Advertised hostname/display identifier |
| `Address` | Chosen reachable IPv4 address, nullable in type but set for successful discovered results |
| `AdvertisedAddresses` | All address strings supplied by Zeroconf, including addresses not selected |
| `Version` | Optional advertised TXT value; not authoritative proof of installed or active release without Core status |
| `ServicePort` | Advertised DNS-SD port, which need not equal the control port |
| `ControlPort` | **7020** in this library |
| `Ready` | True when a TCP connection test succeeds; **not** a handshake or hardware-health check |
| `RawHost` | Underlying Zeroconf host object, for application use |

**Important distinction:** This discovery implementation does *not* probe the controller's protocol handshake before setting `Ready = true`; it only tests that a TCP socket opens. The metadata shown by a browser, scanner, or Control Panel can also be stale compared with a fresh Core status request.

## 3. Windows Control Panel discovery and manual address

The Control Panel opens a device picker populated from `SigCoreDiscovery` and permits manually typed IPv4 addresses while the dialog is visible. Its **Scan Subnet** control uses the Windows ARP table (`arp -a`) to find address candidates, probes them and adds any device candidates to the picker. The dialog saves the selected/typed IP and Auto Connect preference to user preferences.

**Reviewed-source usability defect:** `MainWindowVM.DiscoverSigCore_ClickAsync()` returns immediately if the mDNS resolver finds **zero** eligible devices, **before** constructing `SelectSigCoreDlg`. This makes the picker’s manual IP and Scan Subnet controls unavailable in that case. The UI therefore must not be described as guaranteeing manual IP recovery when mDNS fails; verify that the fix is in the deployed Control Panel before giving such instructions. In addition, a manually typed address is initially constructed as `Ready=true` without a prior Core protocol test, so the subsequent managed connection is what proves protocol compatibility.

## 4. Starting and maintaining a managed `.NET` connection

`SigCoreSystem` is the higher-level client interface for the controller's Core protocol. `StartAsync(host, port = 7020)` owns a managed connection loop; after connection it performs protocol handshake and subscriptions for state changes. Its connection-state enum includes:

`Disconnected` -> `Connecting` -> `Handshaking` -> `Subscribing` -> `Connected`.

If a recoverable transport failure occurs, it enters `Reconnecting`, closes the failed connection and attempts another connection, handshake and subscription. `ReconnectDelay` defaults to **one second** in this class. A protocol rejection is treated differently: a `Faulted` state stops retrying until the application chooses a new course of action. `Stop()` cancels the managed loop and fails outstanding requests.

**Use connection state, not discovery state, to decide whether live commands can be issued.** An open TCP socket proves only transport reachability; `Connected` in `SigCoreSystem` means transport plus required protocol setup. Even then it does not prove that external I/O circuitry or controlled loads are physically healthy.

## 5. Minimal C# usage pattern (illustrative)

The published source supplies `SigCoreCommon.SigCoreDiscovery` and `SigCoreSystem`. The following outline is for understanding call sequence; consult the shipped .NET example and API reference for exact project/package setup and error handling.

```csharp
using SigCoreCommon;
using System.Linq;

var devices = await new SigCoreDiscovery().DiscoverAsync();
var target = devices.FirstOrDefault(d => d.Ready && d.Address != null);
if (target == null) {
    // Discovery is not a substitute for a documented manual-address path.
    return;
}

var system = new SigCoreSystem();
await system.StartAsync(target.Address!, target.ControlPort);
// Wait for system.ConnectionState == SigCoreConnectionState.Connected
// before relying on subscriptions or issuing device commands.
// Always call system.Stop() during orderly application shutdown.
```

Successful completion of the initial `StartAsync` call means the first protocol handshake and subscription completed at that moment. It does **not** guarantee the connection remains live; monitor connection state and notifications for later loss and recovery. Treat the snippet as conceptual until compiled against the customer-shipped assembly version.

## 6. Diagnostic sequence

1. Determine the device's serial number and expected hostname/IP from a trusted local source (label, known DHCP lease or current Core status).
2. Confirm the chosen network can reach the device; mDNS advertisements may be filtered between VLANs/subnets.
3. Try mDNS discovery first on the local link; if no results, distinguish **no advertisement**, **no eligible IPv4 address**, and **TCP 7020 not reachable**.
4. If an address is known, attempt a proper managed Core connection, subject to the current Control Panel's zero-results dialog limitation.
5. Inspect `ConnectionState`. `Faulted` may reflect protocol rejection or incompatible versions; `Reconnecting` usually indicates transient connectivity failure; neither guarantees hardware failure.
6. Request fresh Core status after connection to confirm serial, hardware revision and software version.
7. For web-only problems, check port 80/Apache/PHP separately from Core 7020 and WebBridge loopback 8080. The latter is intentionally not a LAN API endpoint.

## 7. Limitations and engineering verification

- Discovery is **IPv4-only** at address selection, even if an advertisement contains IPv6; device hostname resolution in other contexts may use IPv6, but this discovery path does not select it.
- A timed-out mDNS/TCP scan does not prove absence of a device; multicast filters, network profile or temporarily unavailable TCP listener can explain it.
- Port scanning and ARP discovery must remain scoped to networks the operator is authorized to manage.
- Fix/verify Control Panel's zero-discovery picker path, and separately test a literal IPv4 manual connection.
- Identify and publish the shipped source-version contract and the external network/authentication policy for each deployment.
- Verify that automatic reconnection re-establishes all expected event subscriptions after Core restart and OS network transition.

## Evidence inventory

`libs/SigCoreCommon/SigCoreDiscovery.cs`; `libs/SigCoreCommon/SigCoreSystem.cs`; `ControlPanel/Dialogs/SelectSigCoreDlg.xaml.cs`; `ControlPanel/MainWindowVM.cs`; `Server/SigCoreServer/SigCoreServer.cs`; `WebBridge/Program.cs`; `UpdaterFiles/www/api/_bridge.php`. Source paths are relative to the development snapshot.
