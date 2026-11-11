---
title: "Known Limitations"
status: source-draft-review-required
hardware_revisions: "Rev B source context; Rev C roadmap explicitly excluded"
software_versions: "Supplied unversioned source snapshot; actual installed release and fixes must be verified"
source_review_date: 2026-11-06
customer_release_approved: false
---

# Known Limitations

> **Engineering verification register summarized for AI answers.** "Observed in supplied source" does not mean "still present in the current shipping release." Conversely, a planned or discussed fix is not evidence of successful deployment. This is not a defect disclosure for a specific publicly released version; it is a source-snapshot and documentation assessment. **When asked whether a limitation remains, first establish the exact installed software release and hardware revision.** Do not repeat sensitive implementation details or credentials in customer answers.

## 1. How to interpret status

| Status | Meaning | Appropriate AI wording |
|---|---|---|
| **Published limitation** | Customer-facing reference explicitly defines a restriction | "The reference specifies ..." (state revision) |
| **Observed implementation behavior** | Code in the supplied snapshot demonstrates behavior | "In the reviewed source snapshot ..." (do not assert current shipping behavior) |
| **Unverified specification** | Evidence insufficient to assign a safe electrical/functional guarantee | "The available reference does not establish ..." |
| **Implementation/reference conflict** | Reviewed code and published documentation disagree | Describe both, cite the conflict, **do not guess which is correct** |
| **Planned or in-progress fix** | Engineering work identified but not release-verified | "Fix status requires verification on the installed version" |
| **Unsupported/roadmap** | Proposal or source artifact does not establish support | "Not documented as a supported feature for this revision" |

The verified source of truth for a customer installation should be the approved documentation and release notes **matching that actual revision**, supplemented by a reproducible hardware/software test where appropriate. This document tracks draft evidence, not a superseding support promise.

## 2. Electrical and wiring specification gaps

| ID | Limitation or unresolved question | Why it matters | Where to look |
|---|---|---|---|
| HW-01 | **Digital input maximum voltage is not established** in supplied current product reference | Cannot promise that 5 V, 12 V or 24 V field signaling is nondamaging merely from OFF/ON recognition thresholds | [Electrical Specifications](Electrical-Specifications.md), [Digital Inputs](../Hardware/Digital-Inputs.md) |
| HW-02 | Digital inputs are **not galvanically isolated** in reviewed schematic, notwithstanding an "isolated" label in source drawing | Ground/reference and external circuitry must be designed accordingly | [Digital Inputs](../Hardware/Digital-Inputs.md) |
| HW-03 | 5 V **supply input tolerance, current rating, connector wire/torque and environmental limits** lack approved system numbers | No general installation power or thermal rating can be quoted | [Power and Protection](../Hardware/Power-and-Protection.md) |
| HW-04 | Exact **terminal/pin-1 orientation and complete field wiring map** not signed off against assembled Rev B | Incorrect wiring can damage equipment or fail to operate | [Connectors and Pinouts](../Hardware/Connectors-and-Pinouts.md) |
| HW-05 | AO **terminal output current, impedance, precision, loading, settle time and drift** not established as product ratings | 0–5 V/PWM is a signal interface, not a general-purpose load power output | [Analog Outputs](../Hardware/Analog-Outputs.md) |
| HW-06 | Relay published **120 VAC / 10 A** does not specify universal DC, inductive/motor or inrush ratings; overall certifications unverified | Can't extrapolate contact approval to arbitrary loads | [Relay Outputs](../Hardware/Relay-Outputs.md) |
| HW-07 | Boost/regulator as-built paths differ or may differ from schematic; **board size** Gerber ~230 × 150 mm vs older 250 × 150 mm note | Circuit and mechanical claims need actual-unit verification | [Power and Protection](../Hardware/Power-and-Protection.md), [Mechanical and Assembly](../Hardware/Mechanical-and-Assembly.md) |
| HW-08 | Published **4–20 mA** and low-voltage input classes do not map cleanly to every internal ADC range name/current raw-unit behavior | Calibration and sensor interface instructions need release-specific testing | [Analog Inputs](../Hardware/Analog-Inputs.md) |

## 3. Configuration and control semantics in the supplied source

| ID | Source-snapshot finding | Customer impact / treatment |
|---|---|---|
| SW-01 | Changing relay **Inverted Logic** updates stored config without immediately reapplying currently driven GPIO | Previously reported as an immediate-fix bug; do **not** assert it remains unfixed without checking the release. For an installed unit, test both ON and OFF while changing inversion under safe conditions. [Relay Outputs](../Hardware/Relay-Outputs.md) |
| SW-02 | `failSafeState` property is stored; **fault-time relay application is not demonstrated** | Do not promise configured physical fault behavior; use external independent safety design. [Operating Modes and Safety](../Core-Software/Operating-Modes-and-Safety.md) |
| SW-03 | AO configuration-save path **may zero the output** even for a display change | Avoid unattended config writes during an active process until verified fixed. [Analog Outputs](../Hardware/Analog-Outputs.md) |
| SW-04 | AO scaling direction differs between Control Panel reference and Core (`Display = m × V + b` versus `V = m × Display + b`) | Do not silently substitute a formula; reproduce a current build calculation at known points. [Calibration and Scaling](../Core-Software/Calibration-and-Scaling.md) |
| SW-05 | Prior source loop implementation performs sequential/channel averaging differently from more recent moving-average development, including a zero-count defect in older builds | Version-sensitive: recent engineering work reportedly changed guardrails and sampling. **Test installed version**; do not claim zero remains accepted. [Measurement and Control Timing](../Core-Software/Measurement-and-Control-Timing.md) |
| SW-06 | PID process-variable clamp setter appears ineffective; sharing an AO between multiple PIDs may allow conflicting writers | Separate computed state from actual output control and test PID bounds/owner assignment. [PID Control](../Core-Software/PID-Control.md) |
| SW-07 | Some stored settings, including startup restoration or PID reset-on-mode-change, lack a verified runtime execution path in snapshot | UI option/persisted JSON is not proof a behavior occurs. [Data Model and Configuration](../Core-Software/Data-Model-and-Configuration.md) |
| SW-08 | Source server can acknowledge a command **send without readback**; relay and AO APIs return logical/software values | Independent contact/voltage sensors needed for physical confirmation. [Protocol and Data Reference](Protocol-and-Data-Reference.md) |
| SW-09 | Source does not establish enforceable exclusive **multi-client output ownership** | External integrations must coordinate writing, especially during PID automatic mode. [Integration Patterns](../Programming/Integration-Patterns.md) |
| SW-10 | Snapshot includes process termination and 30-second probation that checks **process survival**, not functional I/O health | Power-loss/reboot/failure responses must be validated for the actual machine. [Processes and Lifecycle](../Core-Software/Processes-and-Lifecycle.md) |

**Critical distinction:** relay inverter and analog averaging issues have been the subject of immediate maintenance work. This KB source snapshot is not pinned to the latest release. Never present a previously discovered issue as newly confirmed on the deployed device without a version-specific retest.

## 4. Communications, website and persistence boundaries

| ID | Limitation | Consequence / verification |
|---|---|---|
| NET-01 | mDNS may be blocked by network boundaries; manual IP connection UI in reviewed Windows source had a no-discovery-path issue | Treat mDNS reachability, TCP 7020 connection, and manual address entry as separate tests. Current fix status unknown. [Discovery and Connections](../Programming/Discovery-and-Connections.md) |
| NET-02 | Device website's `/api/state` is a **cached WebBridge view** | The UI may display stale values after Core disconnects; use `connection.connected` and fresh status. [Status and Diagnostics](../User-Interfaces/Status-and-Diagnostics.md) |
| NET-03 | HTTP `accepted:true, confirmed:false` only indicates the write request was passed on | A socket-level acknowledgment is not physical actuator confirmation. [HTTP API and WebBridge](../Programming/HTTP-API-and-WebBridge.md) |
| NET-04 | Deployed HTTP/authorization/remote exposure security is **not established** by source routes | Do not claim public Internet availability or confidentiality without deployment-specific review. [Networking and Access](../Operations/Networking-and-Access.md) |
| NET-05 | Admin device-settings API in supplied snapshot **may return network/database secrets**; backup may include logger secrets | Restrict administrative access; redact configuration and logs; verify patched behavior before deployment. [Configuration and Restore](../Operations/Configuration-and-Restore.md) |
| NET-06 | MySQL logging code snapshot contains **hard-coded connection credentials** instead of consistently honoring configuration | High-priority source/deployment security reconciliation; never reproduce secrets in KB or answers. [Logging and History](../Core-Software/Logging-and-History.md) |
| DATA-01 | Chart Recorder history is held in **WebBridge RAM** and is lost on restart; periodic samples are resampled for chart reads | Not durable or lossless export; use another approved storage path for long-term evidence. [Data Acquisition and Monitoring](../Applied-Engineering/Data-Acquisition-and-Monitoring.md) |
| DATA-02 | Chart-history request `start`/`length` uses **milliseconds**, but sample timestamps inside `data[0]` use **seconds** | Integration must handle mixed units exactly. [Protocol and Data Reference](Protocol-and-Data-Reference.md) |
| DATA-03 | Configuration restore can return accepted before Launcher restart completes; a network change can interrupt connectivity | Confirm post-restart identity, state and process health; do not equate HTTP response with recovery. [Configuration and Restore](../Operations/Configuration-and-Restore.md) |
| DATA-04 | Source and project differ in **logger process topology** | Verify actual release and installed systemd services before describing logging as inside or outside Core. [Software Architecture](../Architecture/Software-Architecture.md) |
| UPDATE-01 | Updater can prepare release `READY` without showing the **exact trigger** for Launcher to restart/select it | Do not promise an immediate activation. [Updates and Recovery](../Core-Software/Updates-and-Recovery.md) |
| UPDATE-02 | Launcher `GOOD` is based on child-process survival and fallback policy; **not full operational qualification** | Independent device/IO acceptance and external safety checks remain necessary. [Events and Errors](Events-and-Errors.md) |

## 5. Unsupported or unproven capabilities

- **Direct 0–10 V analog input** is not a published supported terminal capability just because the source contains a `Voltage10V` enum; use approved external conditioning.
- **User-facing Modbus TCP** support is **not established** from Modbus-named classes alone. Do not generate a customer register map or promise interoperability without a tested/shipped contract.
- **Customer access to internal I²C peripherals** is not a documented supported integration interface; use the normal API.
- **Independent relay-contact and AO-terminal feedback** is not built into the demonstrated API readback. Add external sensing when required.
- **Rev C features**, such as an RP2040, local e-paper display, revised enclosure and a separate version selector, are plans, not current Rev B capabilities.
- **An unlimited, permanent high-resolution raw chart history** is not provided by the process-RAM recorder. API history responses are resampled and restart-volatile.
- **Cross-platform image behavior** is the consumer's AI-platform choice; the KB includes links to actual images and strongly instructs the AI to show them. It is not necessary for KB development to validate every AI platform.

## 6. How an AI should answer limitations questions

1. Identify the **Rev A/B/C hardware**, installed software version and whether a claim comes from public documentation, source code or a verified test.
2. State the exact supported statement first; if a numerical rating is missing, say so plainly and do not invent a "typical" product rating.
3. For a defect, say whether it was observed only in an **unversioned source snapshot**, and disclose that the current fix status is unknown unless verified by an applicable release.
4. Prefer the original subsystem chapter for detailed conditions, wiring and diagrams, rather than duplicating speculative explanations here.
5. When a response concerns wiring or an energized load, distinguish software command from physical feedback and avoid issuing unsafe numeric/terminal advice without approved ratings.
6. Do not reveal sensitive configuration data or unredacted credentials from project source files.

## Evidence and review status

This index consolidates `project/Verification-Register.md` findings KB-001 through KB-089 and the linked technical chapters. The project register is **development-only**, not part of the intended customer package. The supplied source commit and the deployed unit image have not yet been fully matched, and all entries require review before customer distribution. No resolved/fixed status should be inferred merely because a developer reported work in another thread.
