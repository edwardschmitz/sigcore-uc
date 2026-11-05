# Verification Register

All documents start unverified. Mark **Verified** only after checking implementation or test evidence and recording applicable versions.

| Chapter | Draft | Evidence linked | Reviewed | AI tested | Notes |
|---|---|---|---|---|---|
| [`Architecture/Product-Overview.md`](../Architecture/Product-Overview.md) | No | No | No | No | Placeholder |
| [`Architecture/System-Architecture.md`](../Architecture/System-Architecture.md) | **Yes** | **Yes** | No | No | First source-based draft; release/version and installation details pending |
| [`Architecture/Revisions-and-Feature-Availability.md`](../Architecture/Revisions-and-Feature-Availability.md) | No | No | No | No | Placeholder |
| [`Hardware/Hardware-Overview.md`](../Hardware/Hardware-Overview.md) | No | No | No | No | Placeholder |
| [`Hardware/Power-and-Protection.md`](../Hardware/Power-and-Protection.md) | No | No | No | No | Placeholder |
| [`Hardware/Connectors-and-Pinouts.md`](../Hardware/Connectors-and-Pinouts.md) | No | No | No | No | Placeholder |
| [`Hardware/Analog-Inputs.md`](../Hardware/Analog-Inputs.md) | **Yes** | **Yes** | No | No | Mismatched range nomenclature and current-unit interpretation; review required |
| [`Hardware/Analog-Outputs.md`](../Hardware/Analog-Outputs.md) | No | No | No | No | Placeholder |
| [`Hardware/Digital-Inputs.md`](../Hardware/Digital-Inputs.md) | No | No | No | No | Placeholder |
| [`Hardware/Relay-Outputs.md`](../Hardware/Relay-Outputs.md) | No | No | No | No | Placeholder |
| [`Hardware/Internal-Interfaces.md`](../Hardware/Internal-Interfaces.md) | No | No | No | No | Placeholder |
| [`Hardware/Mechanical-and-Assembly.md`](../Hardware/Mechanical-and-Assembly.md) | No | No | No | No | Placeholder |
| [`Core-Software/Processes-and-Lifecycle.md`](../Core-Software/Processes-and-Lifecycle.md) | No | No | No | No | Placeholder |
| [`Core-Software/Data-Model-and-Configuration.md`](../Core-Software/Data-Model-and-Configuration.md) | No | No | No | No | Placeholder |
| [`Core-Software/Operating-Modes-and-Safety.md`](../Core-Software/Operating-Modes-and-Safety.md) | No | No | No | No | Placeholder |
| [`Core-Software/Measurement-and-Control-Timing.md`](../Core-Software/Measurement-and-Control-Timing.md) | No | No | No | No | Placeholder |
| [`Core-Software/Calibration-and-Scaling.md`](../Core-Software/Calibration-and-Scaling.md) | No | No | No | No | Placeholder |
| [`Core-Software/PID-Control.md`](../Core-Software/PID-Control.md) | No | No | No | No | Placeholder |
| [`Core-Software/Logging-and-History.md`](../Core-Software/Logging-and-History.md) | No | No | No | No | Placeholder |
| [`Core-Software/Updates-and-Recovery.md`](../Core-Software/Updates-and-Recovery.md) | No | No | No | No | Placeholder |
| [`User-Interfaces/Control-Panel.md`](../User-Interfaces/Control-Panel.md) | No | No | No | No | Placeholder |
| [`User-Interfaces/Device-Website.md`](../User-Interfaces/Device-Website.md) | No | No | No | No | Placeholder |
| [`User-Interfaces/Status-and-Diagnostics.md`](../User-Interfaces/Status-and-Diagnostics.md) | No | No | No | No | Placeholder |
| [`Programming/Discovery-and-Connections.md`](../Programming/Discovery-and-Connections.md) | No | No | No | No | Placeholder |
| [`Programming/DotNet-API.md`](../Programming/DotNet-API.md) | No | No | No | No | Placeholder |
| [`Programming/HTTP-API-and-WebBridge.md`](../Programming/HTTP-API-and-WebBridge.md) | No | No | No | No | Placeholder |
| [`Programming/Python-Integration.md`](../Programming/Python-Integration.md) | No | No | No | No | Placeholder |
| [`Programming/Integration-Patterns.md`](../Programming/Integration-Patterns.md) | No | No | No | No | Placeholder |
| [`Operations/Installation-and-Commissioning.md`](../Operations/Installation-and-Commissioning.md) | No | No | No | No | Placeholder |
| [`Operations/Networking-and-Access.md`](../Operations/Networking-and-Access.md) | No | No | No | No | Placeholder |
| [`Operations/Configuration-and-Restore.md`](../Operations/Configuration-and-Restore.md) | No | No | No | No | Placeholder |
| [`Operations/Maintenance-and-Service.md`](../Operations/Maintenance-and-Service.md) | No | No | No | No | Placeholder |
| [`Operations/Troubleshooting.md`](../Operations/Troubleshooting.md) | No | No | No | No | Placeholder |
| [`Applied-Engineering/Sensors-and-Signal-Conditioning.md`](../Applied-Engineering/Sensors-and-Signal-Conditioning.md) | No | No | No | No | Placeholder |
| [`Applied-Engineering/Actuators-and-Loads.md`](../Applied-Engineering/Actuators-and-Loads.md) | No | No | No | No | Placeholder |
| [`Applied-Engineering/Data-Acquisition-and-Monitoring.md`](../Applied-Engineering/Data-Acquisition-and-Monitoring.md) | No | No | No | No | Placeholder |
| [`Applied-Engineering/Control-Applications.md`](../Applied-Engineering/Control-Applications.md) | No | No | No | No | Placeholder |
| [`Applied-Engineering/External-System-Integration.md`](../Applied-Engineering/External-System-Integration.md) | No | No | No | No | Placeholder |
| [`Applied-Engineering/Worked-Designs.md`](../Applied-Engineering/Worked-Designs.md) | No | No | No | No | Placeholder |
| [`Reference/Electrical-Specifications.md`](../Reference/Electrical-Specifications.md) | No | No | No | No | Placeholder |
| [`Reference/Protocol-and-Data-Reference.md`](../Reference/Protocol-and-Data-Reference.md) | No | No | No | No | Placeholder |
| [`Reference/Events-and-Errors.md`](../Reference/Events-and-Errors.md) | No | No | No | No | Placeholder |
| [`Reference/Glossary.md`](../Reference/Glossary.md) | No | No | No | No | Placeholder |
| [`Reference/Known-Limitations.md`](../Reference/Known-Limitations.md) | No | No | No | No | Placeholder |

## Evidence entry format

- Claim / parameter / behavior
- Applicable hardware revision and software version
- Evidence path (schematic sheet, source location, test case or measurement)
- Verification result and reviewer/date
- Outstanding ambiguity or conflicting source


## First-source-pass findings (2026-11-02)

Evidence paths are relative to `Project/Sources/` unless otherwise stated. `SigCore-Software-Source/` refers to extracted files in that subfolder of the development source package. This register is INTERNAL and must not be bundled into the customer distribution.

| ID | Topic | Evidence / observation | Disposition | Priority |
|---|---|---|---|---|
| KB-001 | Current-mode reported units | Schematic sheet 3 has nominal 200 Ω current-path resistors; `SigCore-Software-Source/libs/SigCoreCommon/A_IN.cs` uses ADC voltage-domain values with no obvious conversion to mA; product Control Panel Reference gives mA calibration assumptions. | **Unresolved.** Trace/verify deployed behavior before publishing current-mode calibration instructions. | High |
| KB-002 | Analog range names and customer support | Product Hardware Reference advertises 0–5 V, 0–500 mV, 4–20 mA; Control Panel Reference names `Current20mA` and `Current4To20mA`; source `A_IN.Range` instead has `Current` and multiple voltage PGA entries. | **Unresolved.** Confirm product-supported choices, labels and electrical limits. | High |
| KB-003 | Legacy `Voltage10V` identifier | `ControlPanel/Dialogs/AInConfigDlgVM.cs` deliberately labels legacy `Voltage10V` value as `2.500V Reference` for Rev B; Hardware Reference prohibits direct 10 V connection. | **Explained in code.** Do not advertise 10 V as supported. Verify deployed UI matches snapshot. | High |
| KB-004 | Current-loop wiring / field terminal reference | Rev B schematic sheet 3, top overlay; full user-safe polarity/path and supply assumptions not yet independently checked. | **Unresolved.** No authoritative wiring figure published in this batch. | High |
| KB-005 | Source snapshot provenance | Received `sources(2).zip`; source contains components and customer reference snapshots but no proved complete Git commit/release identification. | **Unresolved.** Establish installed Core, Control Panel, WebBridge and hardware revision scope. | Medium |
| KB-006 | Measured analog system performance | No end-to-end metrology, noise, drift or scan-timing test records supplied. | **Not characterized.** Document manufacturer specifications/code-derived calculations as such; no testing program required for KB drafting. | Non-blocking, explicit limitation |
| KB-007 | Input validation boundary | `AInConfigDlg.xaml.cs` validates/sorts MultiPoint; lower-level payload parses pairs leniently and `Ads1115Driver.ReadChannel` throws for `avgSamples < 1`. | **Unresolved.** Verify deployed Web/API validation and document potential malformed inputs. | Medium |
| KB-008 | Service installation and network exposure | Code establishes Core TCP 7020 and WebBridge localhost HTTP 8080; no complete live systemd/Apache/network exposure inventory reviewed. | **Unresolved.** Do not claim secured/exposed-by-default configuration from architecture alone. | Medium |

### Review policy

`Draft: Yes / Evidence linked: Yes` means a document was assembled from located evidence. It **does not** mean the behavior has been independently tested on a unit, reviewed by the product engineer or approved for customer release. Preserve identified contradictions instead of choosing an unverified preferred interpretation.
