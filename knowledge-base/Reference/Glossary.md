---
title: "Glossary"
status: source-draft-review-required
hardware_revisions: "Rev B context; planned Rev C terms marked explicitly"
software_versions: "Terminology drawn from supplied code and reference snapshots"
source_review_date: 2026-11-06
customer_release_approved: false
---

# Glossary

> **Canonical terminology for the AI KB.** Use user-facing channel names when describing hardware, and zero-based indices when showing an API request. Do not redefine a name differently in separate chapters. A source-code symbol is not necessarily a guaranteed public interface.

## 1. Product, physical hardware and electronics

| Term | Meaning in this knowledge base |
|---|---|
| **SigCore UC** | Raspberry Pi-based configurable controller with analog/digital I/O, relay outputs, PID functions, a local website and external control APIs |
| **Rev A / Rev B / Rev C** | Hardware **revisions**, distinct from software release numbers; Rev C is planned and its proposals are not automatic Rev B capabilities |
| **Raspberry Pi / Pi 4B** | Embedded Linux computer executing the controller processes |
| **Main board / carrier board** | Controller's Rev B I/O PCB and peripheral support electronics |
| **AI1–AI4** | Four **analog input** field channels; API `channel=0..3` |
| **AO1–AO4** | Four **analog output** field channels; API `channel=0..3` |
| **DI1–DI8** | Eight **digital input** field channels; API `channel=0..7` |
| **R1–R8** | Eight **relay output** field channels; API `channel=0..7` |
| **PID1–PID4** | Four software PID loops, **not four additional electrical connections** |
| **GND / return** | Device electrical reference net; not automatically protective earth or an isolated channel return |
| **SPDT** | Single pole, double throw: a relay contact with one common and two switched throws |
| **COM / NO / NC** | Relay **common**, **normally open**, **normally closed** relative to a **de-energized coil** |
| **Dry contact / voltage-free contact** | A switch contact that does not itself source a field voltage |
| **Relay coil** | Internal electromechanical actuator moving a contact set; coil energization differs from logical state if inversion is enabled |
| **Input conditioning** | Electrical circuitry adapting/protecting an input signal before conversion or logic readout |
| **Galvanic isolation** | Absence of a conductive electrical path between circuits; the Rev B digital-input NPN stages do **not** establish this |
| **I²C** | Internal Pi peripheral bus used for relay/DI GPIO expanders, analog mux control, ADC, DAC and PWM |
| **GPIO** | General-purpose digital input/output signal, often on I²C expansion devices |
| **MCP23008** | Eight-bit I²C I/O expander; in Rev B used for relay drive (`0x20`) and DI readback (`0x21`) |
| **MCP23017** | Sixteen-bit I²C GPIO expander used for analog input switch selection (`0x22`) |
| **TMUX1209** | Analog input multiplexer used to route selected AI measurement paths |
| **ADS1115 / ADC** | Analog-to-digital converter; ADS1115 is the multiplexed, programmable-gain input ADC (`0x48`) |
| **MCP4728 / DAC** | Digital-to-analog converter; four-channel DAC for voltage output (`0x60`) |
| **PCA9685 / PWM** | Internal pulse-width modulation generator (`0x40`) also used for analog-output path selection |
| **REF3025** | Nominal 2.500 V precision-reference device in the Rev B analog design |
| **PGA** | Programmable gain amplifier/range setting of the ADC; not a guaranteed terminal voltage limit |
| **PWM duty cycle** | Proportion of each PWM cycle in the active state; **not** a continuous analog voltage by itself |
| **4–20 mA loop** | Current-encoded sensor interface; external transmitter supply and verified input interface must be considered |
| **SPS** | ADC converter samples per second, not automatically SigCore's effective per-channel AI reporting rate |
| **mDNS / DNS-SD** | Multicast DNS and service discovery used to locate SigCore on a local link |
| **Evaluation-unit overlay** | Labeled physical design artwork; not an approved pin-1 wiring diagram |

## 2. Application services and network interfaces

| Term | Meaning / ownership |
|---|---|
| **Core** | Principal device process that owns I/O, sampling, PID behavior, configuration and TCP control protocol |
| **Control Panel / CP** | Windows desktop client for monitoring/configuring SigCore UC through the managed API |
| **Device website** | Apache-hosted pages on the controller for browser-based monitoring/configuration/control |
| **PHP bridge/proxy** | Website PHP scripts that forward supported `/api/*.php` requests to private WebBridge routes |
| **WebBridge** | Local .NET HTTP service, typically bound at `127.0.0.1:8080`, with a managed Core connection and cached state |
| **`/api/state` cache** | WebBridge's in-memory latest state; may remain populated while Core is disconnected |
| **`/api/status`** | WebBridge route that requests fresh Core status; different from cached state |
| **TCP 7020** | Rev B Core protocol control connection, not website HTTP and not Modbus by implication |
| **.NET / SigCoreSystem** | Managed client library and high-level API for discovery, request/response, configuration and writes |
| **Updater** | Process that evaluates, downloads, authenticates, validates and prepares software releases |
| **Launcher** | Process selecting an eligible release, starting/supervising children, and enforcing probation/fallback |
| **Version Selector** | Proposed separate Rev C/3.0.0 component; **not** a proven Rev B deployed process |
| **Chart Recorder** | WebBridge-owned, in-RAM periodic analog history and browser plot; not persistent SQL data storage |
| **MySQL logger** | Separate database logging function/destination; actual deployed process topology is version-sensitive |
| **Device identifier / DSN** | Device serial number identity used in management/update and logging records; not an IP address |

## 3. Software state and configuration

| Term | Meaning / critical distinction |
|---|---|
| **Logical relay state** | `true/false` stored and reported by Core; **not verified relay-contact position** |
| **Coil energization** | Physical voltage/current driving a relay coil; affected by inversion and actual hardware function |
| **Inverted Logic** | Configuration that reverses the relation of logical state to output coil command or DI interpretation |
| **Default State** | Relay logical value initialized at Core startup; not a certified fault-safe state |
| **Fail-Safe State** | Stored property for a desired fault condition; its fault-time application is **not established** in supplied source |
| **Manual mode / Automatic mode** | AO/PID operating modes; automatic behavior depends on configured PID and output routing |
| **Setpoint (SP)** | Desired process value for a PID loop |
| **Process variable (PV)** | Measured or computed input compared with setpoint |
| **Manipulated/output variable (MV)** | PID output command applied to a configured output destination |
| **P / I / D** | Proportional, integral and derivative components of a control algorithm |
| **Calibration** | Mapping a measured electrical signal into desired output/engineering units |
| **Piecewise linear** | Calibration through user-specified input/output point pairs; edge extrapolation must be version-checked |
| **Polynomial calibration** | Calibration by coefficients, ordered `a0 + a1*x + a2*x² + ...` in reviewed code |
| **Scaling** | Mapping between raw, voltage or engineering/display values; AO formula has a documented source/reference conflict |
| **Averaging samples** | AI filtering configuration; prior invalid zero count and batch-sampling behavior are version-sensitive |
| **Debounce** | Input-state settling requirement that reduces short contact bounce/interference |
| **Persisted configuration** | Written settings in device JSON; do not confuse with process-only cache or physical state |
| **Factory Reset** | Configuration reset; not necessarily SD-card reimaging, OS reinstall or restoration of database history |
| **Backup / Restore** | Configuration export/replacement workflows; restore acceptance can precede an actual restart |
| **`accepted:true` / `confirmed:false`** | HTTP command was forwarded/accepted for send; independent hardware actuation was **not confirmed** |
| **`STAGING`** | Update installation in progress/incomplete |
| **`READY`** | Update prepared and eligible for Launcher choice; not yet verified active |
| **`GOOD`** | Launcher process-survival probation passed; not comprehensive I/O health evidence |
| **`FAILED`** | Update candidate failed the applicable activation/probation path |
| **`REGRESSED`** | Manual regression/event concept; deployment status must be verified |
| **Telemetry** | Update service sends release/device metadata/events, distinct from private measurements, configuration, states and logs |
| **Engineering source draft** | Chapter written from available evidence but not signed off or AI-tested for customer release |

## 4. Conventions and frequent ambiguities

- **One-based versus zero-based:** `AI1` is `channel=0`, `R8` is `channel=7`. Do not use board net names like `CIN401` as field terminal numbers.
- **Voltage versus current versus engineering units:** a processed AI reading may be scaled and formatted, so it is not necessarily an ADC input voltage or a current in mA.
- **"On" versus "energized":** a logical relay `true` can command an **unenergized** coil when inverted; software provides no independent contact sensing.
- **"Ready" versus "running":** `READY` describes a prepared software release; `GOOD` describes probation status; neither alone proves live I/O control health.
- **"Connected" versus "fresh":** Core socket connection, cached bridge values and an actual recent sensor measurement are distinct facts.
- **"History" versus "logging":** WebBridge history is RAM/volatile/resampled; database logging is another process/destination and needs configured retention.
- **"Capability in code" versus "shipped product":** unused enum items, a class named `ModbusClient`, or a planned Rev C change are not proof of a customer-supported feature.

## Related chapters

[Product Overview](../Architecture/Product-Overview.md), [Hardware Overview](../Hardware/Hardware-Overview.md), [Software Architecture](../Architecture/Software-Architecture.md), [Protocol and Data Reference](Protocol-and-Data-Reference.md), [Events and Errors](Events-and-Errors.md), [Known Limitations](Known-Limitations.md).
