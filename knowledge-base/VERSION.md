# Knowledge Base Version and Applicability

| Field | Value |
|---|---|
| Package | SigCore UC AI Knowledge Base development draft |
| Knowledge base revision | 0.1.15-source-draft |
| Release status | Internal planning / **not for customers** |
| Hardware revisions covered | Rev B architectural, mechanical outline and I/O circuit evidence; all planned technical subjects drafted from published Rev B references and an unversioned source snapshot; revision-specific approval still pending |
| Software versions covered | Provided software source snapshot (exact commit and release unknown) |
| Verification status | Forty-five technical chapters source-drafted; AI response instructions drafted; zero engineering-approved or AI-tested |
| Image assets | Nine Control Panel screenshots, two source photographs, four Rev B schematic excerpts, a top-overlay illustration and a device Chart Recorder screenshot included in `Images/`; further assets remain in development sources |

## Release version policy (proposed)

- Record a knowledge base release revision and publication date.
- State exactly which hardware revisions and software versions are supported.
- Mark every known mismatch, omission or unsupported configuration.
- Change history should distinguish technical corrections from new features.
- Only publish technical documents and images after their evidence is reviewed.

## Draft change log

- 0.1.2: Added dedicated `AI-Instructions/Instructions.md` and linked it from the README and master index.

- 0.1.3: Drafted source-grounded `Hardware/Analog-Outputs.md`; identified scaling-direction discrepancy and config-save zeroing; added two actual configuration screenshots and image inventory.

- 0.1.4: Added source-grounded `Hardware/Digital-Inputs.md` with the digital-input configuration image and Rev B transistor-stage schematic excerpt. Flagged the schematic's misleading isolation wording, incomplete maximum voltage rating, debouncing input-validation gaps, and field-wiring assumptions for engineering review.

- 0.1.5: Added `Hardware/Relay-Outputs.md` with actual configuration screenshot and eight-relay driver schematic excerpt. Documented physical contact behavior, logical-state/inversion mapping, published 120 VAC / 10 A rating boundaries, Core startup defaults, fail-safe setting not applied in reviewed source, and state-readback/physical-actuation distinctions.

- 0.1.6: Added `Hardware/Hardware-Overview.md` mapping the Rev B controller, 24 field I/O channels, Pi/I²C devices, power domains and physical assembly. Added top-view/enclosure-open source photographs. Identified nominal 230 × 150 mm Gerber outline versus earlier 250 × 150 mm project figure and unverified as-built power-regulator population.

- 0.1.7: Added source-grounded `Architecture/Software-Architecture.md`, documenting Core/WebBridge/website/Control Panel/Launcher/Updater responsibilities, data/command flow, configuration and chart storage, and update/recovery behavior. Marked installed service topology, logger process boundary, activation trigger, and exact source release provenance for verification.

- 0.1.8: Added source-grounded `Core-Software/Processes-and-Lifecycle.md`, covering Core initialization and internal loops, managed client reconnect, WebBridge/chart lifetime, Updater startup and tasks, READY/GOOD/FAILED lifecycle, Launcher probation and supervision, forced child termination, OS reboot endpoints and persistence. Logged deployment/service, graceful shutdown, health criteria and activation-timing verification items.

- 0.1.9: Added six interrelated Core Software chapters: configuration/state; operating modes/safety; measurement/control timing; calibration/scaling; PID control; logging/history. Included system/PID settings screenshots. Identified hard-coded SQL credentials in reviewed source, sensitive fields in backup, inactive mode-change reset flag, ineffective PID PV-clamp setter, sequential/batch analog averaging mismatch with newer work, AO scaling conflict, and chart RAM retention semantics. All remain engineering drafts.

- 0.1.10: Drafted five connected chapters: Updates and Recovery, Control Panel, Device Website, Status and Diagnostics, and Discovery and Connections. Added actual Control Panel main window, device-picker and network-settings screenshots. Identified a Control Panel mDNS-empty/manual-IP usability defect, TCP-only discovery readiness, WebBridge cache freshness boundaries, and incomplete proof of deployed update activation/telemetry and external website access protections. All remain engineering drafts.

- 0.1.11: Added all four remaining Programming chapters (.NET API, HTTP API and WebBridge, Python Integration, Integration Patterns). Kept prior Discovery and Connections chapter. Documented public PHP/loopback WebBridge separation, zero-based I/O indexing, send-only writes, chart history timestamp units and cached-state freshness. Confirmed no Python source/example in supplied snapshot; separately completed Python and C# examples still require installation-package verification. Logged risk of sensitive network/logger passwords being returned through administrative settings API. All chapters remain engineering review drafts.

- 0.1.12: Drafted all five Operations chapters: Installation and Commissioning, Networking and Access, Configuration and Restore, Maintenance and Service, and Troubleshooting. Cross-referenced existing device screenshots and subsystem chapters. Preserved source-versus-shipping-version distinctions, safe I/O commissioning, browser/Core process boundaries, configuration secrets and restore limitations, and evidence-based diagnostics. Added operations review findings; no new or invented installation ratings. All chapters remain engineering drafts.

- 0.1.13: Added all six Applied Engineering chapters: Sensors and Signal Conditioning, Actuators and Loads, Data Acquisition and Monitoring, Control Applications, External System Integration, and Worked Designs. Added an actual Chart Recorder screenshot and cross-linked existing I/O and PID images. Worked examples cover conditioned sensor measurements, a read-only monitor, manual AO-to-AI loopback, thermal PID with an external actuator stage, and an illustrative supervisory pump/valve state machine. The chapters explicitly distinguish software state from physical feedback, current-version uncertainty, unresolved analog-output scaling, sensor-current interpretation and safety limitations. All remain engineering review drafts.

- 0.1.14: Added Product Overview, Revisions and Feature Availability, Power and Protection, Connectors and Pinouts, Internal Interfaces and Mechanical and Assembly. Included the original evaluation-unit overlay and source-sheet excerpts for power/I2C and analog reference/regulators. Distinguished manufacturing Gerber ~230 × 150 mm board outline from earlier planning size; avoided unverified pin-number maps, connector torque, supply-current ratings, as-built regulator population and planned Rev C features. Forty of forty-five technical chapters drafted; all await engineering review.

- 0.1.15: Added all five Reference chapters: Electrical Specifications, Protocol and Data Reference, Events and Errors, Glossary, and Known Limitations. Cross-checked interfaces against SigCoreProtocol.cs and WebBridge/PHP routes, linked supporting source images, resolved previously provisional chapter references, and preserved every unresolved rating/version question for engineering reconciliation. **All 45 planned technical chapters now have first drafts; zero are engineering-approved or AI-tested.**
