# SigCore UC — ModbusBridge

**Implementation:** Modbus TCP **server**, connected to the existing SigCore Core API via `SigCoreCommon.SigCoreSystem` in exactly the same general pattern as `WebBridge/CoreState.cs`.

**Status:** source implementation provided; **not compiled or hardware-tested** in this delivery environment. `SigCoreCommon` is a project dependency in the repository but was not included in the WebBridge reference ZIP. Existing SigCore build/update deployment scripts were also not supplied, so they have not been modified.

## Architecture

```
Modbus TCP clients -> ModbusBridge (TCP 1502 by default)
                      |
                      +-- FC05 relay command --> core.SetRelayAsync()
                      +-- FC16 AO command ------> core.SetAOutValueAsync()
                      |
                      +-- subscribed DigitalInChanged --> DI feedback cache
                      +-- subscribed AnalogInChanged  --> AI feedback cache
                      +-- subscribed RelayChanged     --> relay feedback cache
                      +-- subscribed AnalogOutChanged --> AO feedback cache
                      |
                      +-- SigCoreCommon / Core (127.0.0.1:7020)
```

Core remains responsible for all control logic, relay inversion, analog calibration and output validation. The bridge **does not poll Core** for Modbus reads. Feedback updates originate only from Core subscriptions. A Modbus write forwards the command immediately; it **does not change the feedback cache**. Thus an accepted write is not a confirmation of physical output state.

## Address map

All offsets below are **zero-based on the Modbus wire**. The 0xxxx/1xxxx/3xxxx/4xxxx styles are *reference number notations* used by some client UIs; the network messages contain offsets only.

| Modbus area | Offset(s) | SigCore channel | Behavior |
| --- | --- | --- | --- |
| Coils | 0–7 | Relay 1–8 commands | Write only; forward to Core |
| Discrete inputs | 0–7 | DIN 1–8 | Read-only feedback from Core |
| Discrete inputs | 8–15 | Relay 1–8 | Read-only logical-state feedback from Core |
| Holding registers | 0–1, 2–3, 4–5, 6–7 | AOUT 1–4 commands | Write-only 32-bit floats, forward to Core |
| Input registers | 0–1, 2–3, 4–5, 6–7 | AIN 1–4 | Read-only float feedback from Core |
| Input registers | 8–9, 10–11, 12–13, 14–15 | AOUT 1–4 | Read-only float feedback from Core |

Analog encoding is IEEE-754 binary32 (float), **high 16-bit word first** and **big-endian byte order** in each word. Example: `1.0` = `0x3F80 0x0000`. Core `double` subscription values are converted to `float` for Modbus (precision can be lost). Invalid or non-finite analog values are not exposed as valid feedback.

Supported protocol functions: FC02 (read discrete feedback), FC04 (read analog feedback), FC05 (write relay), FC16 (write one complete AO float). FC01 and FC03 intentionally return illegal-function exceptions: the command locations do **not** serve as feedback. FC06 and FC15 are likewise not implemented. The bridge does not need an extra internal read/write command API; Modbus protocol parsing translates operations directly.

## Core integration

`Io/SigCoreCoreIo.cs` references the following verified names from the supplied WebBridge source:

- `SigCoreSystem`, `StartAsync("127.0.0.1", 7020)`, `Stop`, `Dispose`, `ConnectionState` and `ConnectionStateChanged`.
- `DigitalInChanged`, `AnalogInChanged`, `RelayChanged`, `AnalogOutChanged`.
- `SetRelayAsync(uint channel, bool value)` and `SetAOutValueAsync(uint channel, double value)`.

Internal channels are zero-based, matching the source's `RequiredChannel` checking (`0..7` relays/DIN, `0..3` AIN/AOUT).

Upon disconnect, every cached feedback validity flag is cleared. A read before a channel has provided a fresh subscription notification (including after reconnect) receives Modbus exception `04` (server failure); stale data is never represented as current. During normal startup, some channels may be unavailable until Core has published their initial state. This behavior intentionally avoids fabricating an initial value.

A successful output write means the Core API call completed without error. It does not mean the subscribed output state has already changed. The subscription controls feedback.

## Build and development test

Place the `software/ModbusBridge/` directory in the SigCore repository alongside `software/WebBridge/` and `software/libs/SigCoreCommon/`. The project uses `.NET 8` and references the **existing** `SigCoreCommon.csproj` directly (no independent Core protocol implementation).

The project follows WebBridge's `linux-arm64`, self-contained, single-file publish conventions and routes outputs into the centralized **`build/modbusbridge/`** directory.

From `software/ModbusBridge/` in a complete checkout:

```
dotnet publish ModbusBridge.csproj -c Release
```

By default the service listens on **127.0.0.1:1502** (development only), has unit ID 1, and attempts the Core connection at **127.0.0.1:7020**. Configuration file: `config/modbusbridge.json`. This is intentionally not exposed on the LAN until explicitly configured and tested.

For **simulated I/O only**:

```
dotnet run -- --config config/modbusbridge.simulated.json
python3 -m unittest discover -s tests -v
```

Run the second command in a different terminal, from `software/ModbusBridge/`. Simulation echoes commands into synthetic output feedback, mimicking notifications; it does not contact Core or drive hardware. The socket integration tests **write outputs** and must never be run against an actual controller.

Without .NET, `python3 -m unittest discover -s tests -p test_packet_layout.py -v` runs only offline framing/encoding checks.

## Operating and safety notes

- Modbus TCP does **not** provide authentication or encryption. Restrict the TCP listener to a trusted interface/subnet; never expose the port to the public internet.
- The service accepts multiple Modbus TCP clients. All clients share Core's logical I/O state; ModbusBridge does not enforce command ownership.
- The I/O values are Core's engineering measurements and logical relay states. Relay inversion and physical output driving are handled exclusively by Core.
- If a Core write hangs longer than a Modbus client tolerates, the client may time out even though Core processes the command later. A protocol timeout cannot undo an already-issued command. Confirm final output state through feedback.
- `deployment/modbusbridge.service.template` is deliberately not installed. Integration with the SigCore Launcher, Updater, and deployment scripts requires the current build/release sources.

## Remaining verification

1. Compile with a full SigCore source checkout, including `software/libs/SigCoreCommon/`.
2. Test initial subscription delivery for all four groups, particularly relay/AO channels that may send only changes. Verify state after reconnect.
3. Verify real Core command completion/error semantics and output feedback under simultaneous Control Panel/WebBridge/Modbus operations.
4. Run the socket integration tests **only in simulated mode**; run controlled physical tests separately.
5. Integrate publishing, installation and supervision into the **existing** SigCore build/update workflow after inspecting those files.
6. Extend coverage with the agreed LabVIEW thin VI wrappers after device-level Modbus validation.
