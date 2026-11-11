---
title: "Device Website"
status: source-draft-review-required
hardware_revisions: "Rev B device runtime context"
software_versions: "Supplied unversioned UpdaterFiles/www + WebBridge snapshot; verify against installed release"
source_review_date: 2026-11-06
customer_release_approved: false
---

# Device Website

> **Source-grounded draft; engineering review required.** This is the *website running on the SigCore UC device*, not the public `sigcore-uc.com` product site and not the Windows Control Panel. Browser access and successful page rendering do **not** independently verify Core or any field I/O. The supplied PHP/JavaScript and WebBridge sources define the behavior described below; deployed Apache/systemd settings have not been checked.

## 1. Architecture and access path

```text
Operator browser
    |
    | HTTP website port (normal device site: 80; deployment verification required)
    v
Apache: HTML, CSS, JavaScript and PHP under device website
    |
    | /api/*.php -> local HTTP proxy (_bridge.php)
    v
WebBridge (ASP.NET Core) 127.0.0.1:8080
    |
    | SigCoreCommon managed connection to Core
    v
Core TCP 127.0.0.1:7020 -> hardware I/O and control
```

`UpdaterFiles/www/` is the authoritative device-site source in the supplied snapshot. The current site links to the **public product Support references**; old locally duplicated reference pages are not the maintained source of documentation. The PHP helper `_bridge.php` forwards supported GET, POST and PUT requests to WebBridge at `127.0.0.1:8080`; the browser never needs direct access to that loopback port. Network exposure and authentication are **deployment considerations**: nothing about the loopback bind implies the outward-facing Apache routes are authenticated or protected by HTTPS. See [Software Architecture](../Architecture/Software-Architecture.md).

## 2. Navigation and main screens

The shared site navigation is defined in `UpdaterFiles/www/js/site-nav.js` in this order:

| Menu label | Page | Purpose |
|---|---|---|
| Dashboard | `index.html` | Device identification, operating status, health and network summary |
| Control | `control.html` | Live relay, DI, AI and AO states; relay and AO manual commands |
| Chart Recorder | `chart-recorder.html` | Four-channel AI trend display, historical navigation, recorder settings |
| Properties | `configuration.html` | Read/edit properties for relay, DI, AI, AO and PID channels |
| Logs | `logviewer.php` | SQL event/log display, with local text-log fallback |
| Device | `device.html` | System/network/logger settings, backup/restore, restart and update-status display |
| Support | `support.html` | Links to maintained customer support references |
| Contact | `contact.html` | Product support/contact information |

The common navigation and branding render without an active Core connection. The shared header makes a **single** `/api/status.php` request on page load to fill device name and serial when available; otherwise it deliberately leaves the serial blank rather than generating a fictitious identifier.

## 3. Dashboard

The Dashboard requests device status (`/api/status.php`) and connection state (`/api/connection.php`) independently, normally refreshing approximately every **5 seconds** while the tab is visible. It displays selected system identity, software/hardware versions, uptime, temperature, CPU load, memory/disk information and Ethernet details when returned. If Core is unavailable, a website page may still display, but it shows degraded/unavailable status rather than a successful device-health check. The UI suppresses background refresh while the page is hidden.

Distinguish the different values: **HTTP response** means the web layer answered; **Core connected** means WebBridge completed its Core connection; **Ready** is an application-reported status; and **equipment healthy** would require independent diagnostics beyond these indicators. See [Status and Diagnostics](Status-and-Diagnostics.md).

## 4. Control page

`js/control.js` creates eight relay controls, eight read-only DI indicators, four read-only AI readings and four AO controls. It loads names/configurations without polling every channel continually, then polls `/api/state.php` with an adaptive interval (roughly **450 ms** in normal operation, briefly **150 ms** after a relay interaction). State comes from WebBridge's subscription cache.

- A relay click posts to `/api/control.php?operation=relay`; the rendered ON/OFF state updates from the later subscribed state response, not simply from the click.
- The N/O/N/C visual indicators are computed from the **software logical relay state** and the `invertedLogic` configuration. They are *not* separate contact sensors. Do not tell the user the relay contacts have physically been proven to move based on these indicators.
- An AO value is entered in the pop-up editor and sent through `/api/control.php?operation=analog-output`; the control is disabled when Core is disconnected. Correct output range/mode and PID ownership must be checked separately.
- DI values and AI values are observations, not control buttons. Display formatting follows the retrieved configuration (precision and units).

The reviewed control page computes relay contact indicators from its cached inversion property. Because changing relay inversion without a physical update is a known source-snapshot bug, any indication derived this way can disagree with the actual coil until the Core correction is verified in the deployed version. See [Relay Outputs](../Hardware/Relay-Outputs.md). The immediate relay-inversion fix is a separate development task, **not** assumed installed by this KB draft.

## 5. Properties page

`js/configuration.js` creates channel buttons for `relay` (8), `di` (8), `ai` (4), `ao` (4), `pid` (4), then obtains the selected configuration using `/api/config.php?kind=<kind>&channel=<zero-based index>`. It builds a modal editor from known fields, supports calibration fields and enum selectors, and submits edited values using HTTP `PUT` to the same PHP path. The browser limits simultaneous name lookups to two concurrent requests. These configuration data are managed by Core, not by the browser alone.

**Field semantics and validation:** The UI can offer values that are not meaningful for all Core revisions; the Core boundary must enforce allowed ranges. In particular, older code allowed invalid analog averaging values; the later requirement is **1–100** samples. Treat a successful visual edit as a request to persist configuration, not proof that hardware behavior or safe operation has been validated. See [Data Model and Configuration](../Core-Software/Data-Model-and-Configuration.md) and the specific I/O chapters.

## 6. Chart Recorder

The Chart Recorder displays AI1–AI4 using the locally bundled **uPlot** library and the **WebBridge-owned** in-RAM circular history. It does not require a new Core wire protocol. Its main operations include selecting a time window; dragging/panning earlier or later; zooming; choosing individual channel traces/colors; selecting automatic or manual Y scale; and setting recorder interval and memory limit.

**Default behavior confirmed in supplied WebBridge source:** recording interval **1 second**, maximum memory allocation **1024 MiB**, default display window **3600 seconds**, four enabled traces. Valid `recordIntervalSec` is **0.1 to 60 seconds**; valid `maxMemoryMiB` is **16 to 1024**. History sampling is periodic only when WebBridge is connected to Core. The saved settings are kept in `/opt/sigcore/webbridge-chart.json` on Linux, not browser localStorage; samples are **RAM-only**, lost on WebBridge restart, and changing the recording interval or capacity intentionally clears history.

**Live viewport rule:** **Go Live** places NOW at **90%** of the window, leaving 10% to the right; the window stays stationary until the current time passes its right edge, when it advances by a 10%-of-window increment. The browser's live timer uses the configured recorder interval. When viewing historical data, interaction exits live mode. The history API takes UTC Unix-millisecond `start`, `length` and `samples` and returns evenly positioned values for display. Recorded buckets use the **last** sample in the bucket; empty buckets are interpolated only if bracketing measurements exist, otherwise become JSON `null`. This is not a lossless export of every raw reading or peak. See [Logging and History](../Core-Software/Logging-and-History.md).

## 7. Logs, Device and Support pages

**Logs:** `logviewer.php` uses MySQL when configured/accessible, with an automatic fallback to the local text log. It supports filter fields and bounded record counts. The local text-log endpoint reads a tail of up to 2 MiB from `/tmp/sigcore-log/log.txt`; that is a diagnostics window, not a permanent archive. Detailed behavior: [Status and Diagnostics](Status-and-Diagnostics.md).

**Device:** `js/device.js` reads and edits system, network and logger settings via `/api/device-settings.php?kind=...`. It provides configuration backup and restore and a restart request with UI confirmation. The restore UI states that global and network settings are preserved, with a device restart after restore. **Important:** source review shows a backup can contain the stored logger password. Treat downloaded backups as credentials-bearing files; do not share them in support requests or public AI chats without redaction. See [Data Model and Configuration](../Core-Software/Data-Model-and-Configuration.md).

**Support:** The site links to maintained **public product website** reference documents; duplicating and maintaining separate device-local reference copies is not the intended source hierarchy.

## 8. Troubleshooting by layer

| Symptom | Likely layer to check |
|---|---|
| Website entirely unreachable | Device IP/DNS, network, Apache/service/port/firewall |
| Website loads but controls say disconnected | WebBridge-to-Core session, Core TCP 7020, application processes |
| Dashboard status fails | PHP proxy and `/api/status`, WebBridge or Core status request |
| Relay click succeeded but load did not switch | Logical/inverted state, driver/contact behavior and external wiring; not merely HTTP success |
| Chart shows no new readings | Core connection, sampling task, AI state cache; a constant signal may legitimately be constant |
| Chart lost older history | WebBridge restart or recorder reset on interval/capacity change, RAM retention limit |
| Log Viewer shows text instead of SQL | MySQL connectivity/configuration or logger issue; read fallback notice |
| Device unreachable after network edit | New IP/SSID/DHCP, hostname, active interface and local network reachability |

## 9. Engineering verification required

- Verify installed Apache routes, TLS/authentication posture and access restrictions before publicly exposing control/configuration endpoints.
- Reconcile browser-side field limits with Core validation, particularly AI averaging and precision.
- Verify the relay inversion fix against both physical output and browser N/O/N/C indicators in the shipping version.
- Confirm restart/restore semantics on deployed units and that configuration backup handling protects embedded secrets.
- Confirm actual chart retention and memory overhead on the target Raspberry Pi.
- Validate that the public Support links match the published references and no obsolete device-local copies are deployed.

## Evidence inventory

`UpdaterFiles/www/js/site-nav.js`, `dashboard.js`, `control.js`, `configuration.js`, `chart-recorder.js`, `device.js`; `UpdaterFiles/www/api/*.php`; `UpdaterFiles/www/logviewer.php`; `WebBridge/Program.cs`; `WebBridge/Endpoints/*.cs`; `WebBridge/CoreState.cs`; `WebBridge/ChartRecorder.cs`. Paths are relative to the supplied development source snapshot.
