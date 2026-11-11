# SigCore UC AI Knowledge Base

**Status: INTERNAL DEVELOPMENT DRAFT. Forty-five source-grounded chapters are drafted; none has passed engineering review or blind AI validation. Not ready for customer distribution.**

## Purpose

This is a collection of technical source documents intended to be ingested by a customer's chosen AI model. It prioritizes complete, precise, version-specific product knowledge over prose optimized for direct human reading. Customers should use the existing human-oriented SigCore UC documentation when seeking a manual or tutorial.

The customer supplies the AI service. SigCore supplies files, not a hosted chatbot or AI inference service.

## Navigation

- [AI-Instructions/Instructions.md](AI-Instructions/Instructions.md) defines the intended behavior of an AI assistant answering questions with this knowledge base.
- [INDEX.md](INDEX.md) is the master table of contents with direct links to every subject file.
- [VERSION.md](VERSION.md) tracks the knowledge base's release status and applicability.
- `AI-Instructions/` contains AI-facing response guidance.
- `Architecture/`, `Hardware/`, `Core-Software/`, `User-Interfaces/`, `Programming/`, `Operations/`, `Applied-Engineering/`, and `Reference/` contain modular knowledge documents.
- `Images/` contains the shared image library, with an [image inventory](Images/IMAGE-INVENTORY.md).
- `project/` contains **development-only** project management and verification records. Do not include `project/` in a customer release.

- The eight drafted [Core Software chapters](INDEX.md#core-software) cover lifecycle, configuration/state, operating modes/safety, acquisition timing, calibration, PID, logging/history and release updates/recovery.
- [User Interfaces](INDEX.md#user-interfaces) covers the Windows Control Panel, on-device website and status/diagnostics.
- [Programming](INDEX.md#programming) covers discovery and connections, native .NET API, device website HTTP API, Python integration paths, and multi-client integration patterns.
- [Operations](INDEX.md#operations) covers safe commissioning, networking, configuration backup/restore, maintenance and symptom-oriented troubleshooting.
- [Architecture](INDEX.md#architecture) now includes product overview, revision/feature mapping and detailed system/software architecture.
- [Hardware](INDEX.md#hardware) includes I/O subsystems, power, connector identity, internal bus map and mechanics (installation pinouts and electrical limits still require sign-off).
- [Reference](INDEX.md#reference) consolidates verified/source-based electrical characteristics, protocol/data fields, events/errors, standard terminology and documented limitations.
- [Applied Engineering](INDEX.md#applied-engineering) covers compatible sensor/actuator interfaces, monitoring, PID/supervisory control, external integration and worked application examples.

## Authoring rules

1. **Never invent a specification.** Derive technical claims from revision-specific schematics, source code, tests or verified records.
2. **Separate confirmed product behavior from engineering guidance and hypothetical designs.** Label planned features explicitly.
3. **Treat image data as supporting evidence.** Record electrical connections, labels, values and important relationships in text even when there is a diagram.
4. **Use one source of truth per specification.** Cross-reference related files instead of maintaining contradictory definitions.
5. **Record applicability.** Specify the hardware revision and software release where a behavior applies.
6. **Use relative image paths.** Images are kept in `Images/` and referenced from documents via `../Images/<group>/<name>`. The consuming AI is instructed to display the actual images inline when relevant; users choose a capable AI platform.
7. **Keep the project modular.** Do not flatten separate Markdown sources during editing.

## Readiness rule for an AI

All forty-five planned technical chapters now have first drafts; there are no remaining placeholders in the master index. `Architecture/System-Architecture.md`, `Architecture/Software-Architecture.md`, `Core-Software/Processes-and-Lifecycle.md`, `Core-Software/Data-Model-and-Configuration.md`, `Core-Software/Operating-Modes-and-Safety.md`, `Core-Software/Measurement-and-Control-Timing.md`, `Core-Software/Calibration-and-Scaling.md`, `Core-Software/PID-Control.md`, `Core-Software/Logging-and-History.md`, `Core-Software/Updates-and-Recovery.md`, `User-Interfaces/Control-Panel.md`, `User-Interfaces/Device-Website.md`, `User-Interfaces/Status-and-Diagnostics.md`, `Programming/Discovery-and-Connections.md`, `Programming/DotNet-API.md`, `Programming/HTTP-API-and-WebBridge.md`, `Programming/Python-Integration.md`, `Programming/Integration-Patterns.md`, `Operations/Installation-and-Commissioning.md`, `Operations/Networking-and-Access.md`, `Operations/Configuration-and-Restore.md`, `Operations/Maintenance-and-Service.md`, `Operations/Troubleshooting.md`, `Hardware/Hardware-Overview.md`, `Hardware/Analog-Inputs.md`, `Hardware/Analog-Outputs.md`, `Hardware/Digital-Inputs.md`, `Hardware/Relay-Outputs.md`, `Applied-Engineering/Sensors-and-Signal-Conditioning.md`, `Applied-Engineering/Actuators-and-Loads.md`, `Applied-Engineering/Data-Acquisition-and-Monitoring.md`, `Applied-Engineering/Control-Applications.md`, `Applied-Engineering/External-System-Integration.md`, `Applied-Engineering/Worked-Designs.md`, `Architecture/Product-Overview.md`, `Architecture/Revisions-and-Feature-Availability.md`, `Hardware/Power-and-Protection.md`, `Hardware/Connectors-and-Pinouts.md`, `Hardware/Internal-Interfaces.md`, `Hardware/Mechanical-and-Assembly.md`, `Reference/Electrical-Specifications.md`, `Reference/Protocol-and-Data-Reference.md`, `Reference/Events-and-Errors.md`, `Reference/Glossary.md`, `Reference/Known-Limitations.md` now have `source-draft-review-required` status: they contain evidence-linked technical information and explicitly marked conflicts, but are **not approved for customer use**. A model must not infer product capabilities from placeholders or silently resolve contradictions in drafts.

## Release intent

The planned customer artifact is a ZIP of the completed Markdown collection and approved images, plus `README.md`, `INDEX.md` and `VERSION.md`. **This scaffold ZIP is not the customer release.**
