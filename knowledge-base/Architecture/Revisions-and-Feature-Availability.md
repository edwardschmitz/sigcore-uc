---
title: "Revisions and Feature Availability"
status: source-draft-review-required
hardware_revisions: "Rev A / Rev B / proposed Rev C, only as individually qualified"
software_versions: "Rev B supplied unversioned development snapshot; forward-looking Rev C/3.0.0 plans clearly marked"
source_review_date: 2026-11-06
customer_release_approved: false
---

# Revisions and Feature Availability

> **Engineering draft.** Three different identifiers must not be conflated: *hardware revision*, *software version*, and *knowledge-base revision*. Roadmap features are not available simply because they appear in project notes. This source snapshot has no verified Git commit or binary checksum tying it to a shipping release.

## 1. Revision and version identities

| Identity | Purpose | Evidence and limits |
|---|---|---|
| **Hardware Rev A** | Earlier controller design | Referenced in field migration notes, but no complete Rev A electrical package is in the supplied sources; do not carry Rev B connector/power ratings backward. |
| **Hardware Rev B** | Current documented I/O-board design | August 15, 2026 Rev B schematic, Gerber outline, public Hardware Reference, supplied device photos. |
| **Hardware Rev C** | Future board/design work | Candidate list only. No production hardware, approved schematics or released firmware provided. |
| **Software 2.x** | Rev B release family in project workflow | Source bundle is unversioned. Development notes mention 2.0.26 and 2.0.27, but supplied files were not tied to an immutable commit/build. |
| **Software 3.0.0** | Proposed future version-selection architecture | Planned Rev C work; not a current Rev B feature. |
| **Knowledge Base 0.1.x** | Documentation draft revision | Measures document drafting progress; does not mean a particular device software has been tested. |

**Example:** A Rev A unit running a migration package or updated software is not thereby Rev B *hardware*. Likewise, release 2.0.27 does not imply Rev C features. Treat mechanical revisions and software capabilities independently.

## 2. What is supported by supplied Rev B evidence

| Capability | Source-grounded Rev B status | Where to look |
|---|---|---|
| Eight relay outputs, eight DI, four AI, four AO | **Documented in published hardware guide** | [Hardware Overview](../Hardware/Hardware-Overview.md) |
| Device-resident website and Windows Control Panel | **Documented; source implementation available** | [Device Website](../User-Interfaces/Device-Website.md), [Control Panel](../User-Interfaces/Control-Panel.md) |
| Native Core TCP server port 7020 and .NET client | **Implemented in reviewed source** | [Software Architecture](Software-Architecture.md), [.NET API](../Programming/DotNet-API.md) |
| PHP-to-loopback WebBridge HTTP paths | **Implemented in reviewed source**; deployment exposure requires verification | [HTTP API](../Programming/HTTP-API-and-WebBridge.md) |
| Chart Recorder, 1-second default RAM sampling and windowed history | **Implemented in reviewed WebBridge source**; retention not permanent | [Logging and History](../Core-Software/Logging-and-History.md) |
| Launcher/Updater signed package and probation workflow | **Implemented in reviewed source**; deployed activation trigger unverified | [Updates and Recovery](../Core-Software/Updates-and-Recovery.md) |
| PID loops and AI calibration | **Implemented in reviewed source**, with identified version-sensitive behaviors | [PID Control](../Core-Software/PID-Control.md), [Calibration and Scaling](../Core-Software/Calibration-and-Scaling.md) |
| Completed Python and C# example projects | **Reported completed separately**, but not present as source files in supplied snapshot | [Python Integration](../Programming/Python-Integration.md) |
| Independently deployed MySQL logger | **Discussed for current project**, but snapshot instantiates logger in Core | [Logging and History](../Core-Software/Logging-and-History.md) |
| Automatic remotely initiated customer-device updates | **Not established as a feature**; customer-device-initiated update process is described | [Updates and Recovery](../Core-Software/Updates-and-Recovery.md) |

The feature table is *evidence scope*, not a guarantee of availability on a customer's installed build. Confirm installed software version, hardware revision and any installed migration before giving version-specific instructions.

## 3. Planned features: not Rev B promises

The following items were identified as **future work** in the engineering project context, not verified capabilities of the reviewed Rev B release:

- **Rev C hardware:** RP2040-based DI/PWM development, changes to Power/Ready indicators, a front-facing display provision, larger mounting bosses and additional mechanical/connectivity improvements.
- **Version Selection Architecture 3.0.0:** Independent release-selection logic rather than the reviewed current Launcher selection behavior.
- **Control Panel update-available notifications:** Planned enhancement; no reliable current operational claim from this source snapshot.
- **Expanded customer scripting, process screens and external-device integration:** Roadmap descriptions do not establish a shipped user scripting host or arbitrary industrial protocol server.

A future feature can be discussed as a plan, but must never be used as a required step in a present-tense wiring, configuration or coding procedure. Avoid inventing planned release dates.

## 4. Compatibility boundaries

- **Rev B protocol constraint:** The project maintenance policy is to avoid changes to existing Rev B device/Control Panel communications protocol. Protocol-breaking changes belong in a future hardware/software revision. This is an engineering policy and should be verified against actual release artifacts, not inferred from source filenames.
- **Configuration file:** Core reads `/opt/sigcore/properties.json` in the supplied source; preserving a filename does not guarantee cross-version schema equivalence. Verify restoration/migration rules before copying configuration between hardware or major software revisions.
- **Release major number:** The supplied Updater rejects unattended cross-major-version upgrade paths. Do not assume a Rev A-to-Rev B or 2.x-to-3.x unattended transition.
- **Physical compatibility:** Terminal positions, harnesses, mounting, power and internal bus pin usage must be checked against the specific hardware revision. Rev C ideas are not approved pinout evidence for Rev B.
- **UI/example compatibility:** A client sample or screenshot may target a development build. Confirm the current API's payloads and menu labels when preparing a customer-facing example.

## 5. Responding to version-specific questions

1. Identify the user's **hardware revision**, **installed software version**, and whether they mean a planned feature or presently deployed capability.
2. Use the matching chapter and evidence date. If only an unversioned source snapshot is available, state the limitation instead of claiming an installed unit will behave identically.
3. For a bug discussed in a past source review, do not claim it is unfixed unless the present release was tested. Conversely, do not claim a fix is deployed merely because it has been requested.
4. Cite the source/revision and any necessary engineering verification finding.

**Known version-sensitive examples:** relay inversion application on configuration changes; analog-input averaging redesign and range checks; analog-output scaling and save behavior; MySQL logger deployment topology; updated launcher shutdown policy; chart recorder behavior. The reader should prefer release-verified behavior when it becomes available.

## 6. Evidence and unresolved mapping

- Customer-facing references: `project/sources/documentation/{hardware-reference,control-panel-reference,api-reference}.html`.
- Hardware: `project/sources/hardware/SCH_Schematic1_2026-08-15.pdf`, `Gerber_PCB17_2026-08-15/`, `top-overlay.pdf`.
- Source: `project/sources/SigCore-Software-Source/` (exact commit/build unavailable).
- Project tracking: `project/Verification-Register.md`, KB `VERSION.md`.

**Required release audit:** Obtain the deployed build's Git commit/tag, component versions, installed architecture and any Rev B board revisions. Then update per-chapter applicability instead of replacing all historical source findings with unqualified "current" language.
