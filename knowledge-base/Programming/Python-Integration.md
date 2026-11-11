---
title: "Python Integration"
status: source-draft-review-required
hardware_revisions: "Rev B"
software_versions: "Supplied unversioned snapshot; verify distributed Python/C# examples against installed release"
source_review_date: 2026-11-06
customer_release_approved: false
---

# Python Integration

> **Engineering source draft.** Python and C# API examples were completed separately in the development workflow, but **their distribution with the device/software installation has not yet been verified**. The supplied source tree contains no Python example or native Python client implementation. This chapter therefore documents verified interface paths and a standard-library HTTP **read-only** example, without inventing a supported Python SDK or guessing at Core TCP message framing. See [HTTP API and WebBridge](HTTP-API-and-WebBridge.md) and [.NET API](DotNet-API.md).

## 1. Which Python connection path is appropriate?

| Path | Where Python runs | Status in supplied source | Use |
|---|---|---|---|
| HTTP over the device website | Authorized client on a network that can access device Apache/PHP | PHP proxy endpoints and WebBridge implementation are present | Simple polling of `/api/state.php`, fresh status, chart history; guarded command/config writes |
| Native Core TCP protocol on **7020** | External Python host | Core custom protocol and .NET client implementation exist; **no Python implementation verified in this snapshot** | May be used by separately developed, version-matched Python example; do not improvise JSON framing/handshake |
| Python calling SigCoreCommon .NET assembly | External host | **Not verified as supported** | Possible integration technique in general, but do not prescribe as a SigCore-supported method without tests |
| MySQL logger database access | Any appropriately authorized database client | Logger configuration/database schema is separate from real-time Core control | Historical data and reporting; **not** command or live-status interface |

Do not confuse WebBridge's `127.0.0.1:8080` loopback server with a remote network service. It is accessed through the device website's PHP route on the Apache-hosted address. No Python package name, pip command, remote authentication method, or on-device shell integration should be invented from this snapshot.

## 2. Channel names and data semantics

- All API indices begin at **0**. `AI1`/`AO1`/`R1`/`DI1` are channel `0` in requests and zero-based arrays. Four AI and AO channels and eight DI/relay channels are present.
- `/api/state.php` returns WebBridge's **cached** values and Core connection state. If `connection.connected` is false, treat all last-known measurements as **stale**.
- Relay booleans report **software logical states**. They are not an independent coil/contact position sensor.
- `/api/status.php` uses a direct Core status request (when connected) and is different from cached `/api/state.php`.
- Chart history uses **milliseconds** in query parameters, but `data[0]` timestamps are **Unix seconds**, and missing data appears as JSON `null`.
- Write acknowledgments with `accepted:true, confirmed:false` are transport/command acceptance, not a measurement that output hardware reached the requested condition.

## 3. Standard-library Python: read-only state and error handling

This example uses only Python's `urllib` and `json`, and reads from a trusted installation where the device website is reachable. It makes **no physical output changes**.

```python
import json
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen

DEVICE = "http://192.0.2.10"  # Documentation placeholder: substitute your device URL.

def get_json(path: str, timeout: float = 5.0) -> dict:
    request = Request(DEVICE + path, headers={"Accept": "application/json"})
    try:
        with urlopen(request, timeout=timeout) as response:
            if response.status != 200:
                raise RuntimeError(f"Unexpected HTTP status: {response.status}")
            return json.load(response)
    except HTTPError as error:
        raise RuntimeError(f"SigCore HTTP error: {error.code}") from error
    except URLError as error:
        raise RuntimeError("SigCore website is unreachable") from error

snapshot = get_json("/api/state.php")
if not snapshot.get("connection", {}).get("connected", False):
    raise RuntimeError("Core is disconnected; cached values may be stale")

ai = snapshot.get("analogInputs", [])
if len(ai) < 4:
    raise RuntimeError("Incomplete analog input snapshot")

print("AI1:", ai[0], "AI2:", ai[1], "AI3:", ai[2], "AI4:", ai[3])
```

**Important limitations:** This verifies only the shape shown by the reviewed source. HTTP availability, network policy and permissions depend on the installed device. The illustrative `192.0.2.10` address is **not** a device default. No read timestamp accompanies each cached channel, and `connection.connected` can change between reading the cache and acting on it. Poll at an appropriate rate; do not assume the browser/chart recording interval equals hardware ADC conversions per second.

## 4. Optional read-only history request

```python
import time
from urllib.parse import urlencode

window_ms = 60_000
now_ms = int(time.time() * 1000)
query = urlencode({
    "start": now_ms - window_ms,
    "length": window_ms,
    "samples": 120,
})
history = get_json("/api/chart-history.php?" + query, timeout=12.0)
series = history["data"]
if len(series) != 5:
    raise RuntimeError("Unexpected chart series count")
# series[0] = timestamps in Unix seconds; series[1] = AI1, ... [4] = AI4.
# Any missing measurement is represented by None in decoded JSON.
points = list(zip(series[0], series[1]))
print("AI1 samples:", len(points))
```

Returned data are **resampled display history**, not lossless raw acquisition values. Samples may represent latest-in-bucket readings or interpolated values; missing boundaries produce nulls. Chart history resides only in WebBridge process RAM and is discarded on restart. This is not equivalent to the separate SQL logger.

## 5. How to approach writes safely

Python can send `POST`/`PUT` JSON through the documented device PHP endpoints, but software writes can operate real machinery. Do **not** provide an automatic loop that toggles a relay without explicit equipment context. Before using a write: identify the channel/wiring, ensure safe system operating mode, confirm no competing controller, validate bounded input values, check current state, send once, and read back Core state. Use a separate physical feedback sensor when confirmation of a contact or actuator matters.

In the reviewed HTTP proxy, a relay write is `POST /api/control.php?operation=relay` with a JSON body of `{"channel":0,"state":false}`. The response explicitly includes `"confirmed":false`. An AO write uses `operation=analog-output` and `{"channel":0,"value":2.5}`; control range and engineering-unit interpretation depend on its actual output configuration. PID writes use `operation=pid&channel=0&action=...`. The **existing source snapshot** does not establish authenticated API access over the public Internet. See [HTTP API](HTTP-API-and-WebBridge.md) for the complete route map and security boundary.

For configuration, the per-channel `PUT /api/config.php` expects a **complete set of allowed fields**, whereas `PUT /api/device-settings.php` supports a partial update. Read, edit, send, and read back; never submit an empty or stale configuration because that can reset behavior or cause output changes. The source review separately found that saving AO configuration can reset its output to zero; check the shipping version before automating configuration edits.

## 6. Python when the native TCP 7020 interface is required

The Core TCP protocol is not simply newline-delimited JSON or HTTP. It has a documented-by-source message/framing, connection setup, subscription and asynchronous response/event model implemented in C# (`SigCoreClient`, `SigCoreProtocol`, `SigCoreSystem`). The user-developed Python demonstration is intended to supply the working implementation. **Do not synthesize transport bytes, commands, checksums or serialization from a high-level summary.** Use the actual, version-matched sample once verified to ship, and respect the same connection lifecycle and readback limitations as `.NET`.

A Python caller also needs to distinguish unsolicited state-change notifications from correlated responses. Reconnecting should restore subscription and trigger a fresh state load; it must not unconditionally replay pending write commands. Prefer stable read-only capture until the application owner has decided how to handle ambiguous delivery.

## 7. Logging, tests and deployment checklist

1. Confirm installation includes the completed Python examples **and** any required support files; file location and commands are not established by the supplied snapshot.
2. Verify the example against the precise device software release and port chosen; test a clean boot, delayed device startup, disconnect/reconnect and a no-device case.
3. Check zero-based indexing and physical wiring before writes. Confirm that invalid channel indices are rejected.
4. Check JSON values for `None`/nonfinite data and time units for chart queries.
5. Avoid embedding WLAN passwords, database passwords or device-specific secrets in samples, logs and source control.
6. Treat a successful API read as software state, not a guarantee of field wiring, measurement accuracy or interlock safety.

## Evidence and open verification

`WebBridge/Endpoints/StatusEndpoints.cs`, `RelayEndpoints.cs`, `AnalogOutputEndpoints.cs`, `PIDEndpoints.cs`, `ConfigurationEndpoints.cs`, `ChartEndpoints.cs`, `ChartRecorder.cs`; `UpdaterFiles/www/api/*.php`; `libs/SigCoreCommon/SigCoreClient.cs`, `SigCoreSystem.cs`, `SigCoreProtocol.cs`. Source snapshot contains **no `.py` implementation**: verify completed Python and C# example deliverables and installation packaging separately. Any claimed supported Python SDK/API beyond these routes requires additional release evidence.
