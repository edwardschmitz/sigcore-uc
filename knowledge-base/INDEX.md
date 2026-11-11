# SigCore UC AI Knowledge Base: Table of Contents

> **INTERNAL DRAFT. NOT FOR CUSTOMER DISTRIBUTION.** All forty-five technical chapters now contain source-grounded first drafts requiring engineering review. **Draft coverage is complete; zero chapters are engineering-approved or AI-validated.**

This collection is designed to provide technical knowledge to a customer-selected AI service, **not** to serve as a conventional readable manual. For human-oriented instructions, use the separate SigCore UC support references.

Begin with [README.md](README.md) for usage and verification rules, [AI instructions](AI-Instructions/Instructions.md) for expected assistant response behavior, and [VERSION.md](VERSION.md) for release status.

## AI Instructions

- [Instructions for AI Assistants](AI-Instructions/Instructions.md): Guidance for answering questions, using technical facts, including actual images, citing sources, and handling uncertainty.

## Contents

### Architecture

- [Product Overview](Architecture/Product-Overview.md): **SOURCE DRAFT, REVIEW REQUIRED.** Rev B product purpose, I/O inventory, client options, engineering boundaries, representative applications and source authority.
- [System Architecture](Architecture/System-Architecture.md): **SOURCE DRAFT, REVIEW REQUIRED.** Hardware/software components and their relationships.
- [Software Architecture](Architecture/Software-Architecture.md): **SOURCE DRAFT, REVIEW REQUIRED.** Rev B runtime components, Core/client ownership, network interfaces, persistence, release management, process supervision and failure boundaries.
- [Revisions and Feature Availability](Architecture/Revisions-and-Feature-Availability.md): **SOURCE DRAFT, REVIEW REQUIRED.** Rev A/B/C hardware vs software versions, implementation evidence, shipping uncertainty, protocol compatibility and roadmap distinctions.

### Hardware

- [Hardware Overview](Hardware/Hardware-Overview.md): **SOURCE DRAFT, REVIEW REQUIRED.** Rev B physical composition, I/O and I²C device map, power/reference boundaries, PCB fabrication size, photographs and engineering cautions.
- [Power and Protection](Hardware/Power-and-Protection.md): **SOURCE DRAFT, REVIEW REQUIRED.** 5 V power distribution, Pi/logic rails, 2.500 V reference, schematic protection, as-built regulator uncertainty and power loss.
- [Connectors and Pinouts](Hardware/Connectors-and-Pinouts.md): **SOURCE DRAFT, REVIEW REQUIRED.** Evaluation-unit overlay and connector design labels, relay COM/NO/NC, reference returns, wiring limits and outstanding terminal orientation sign-off.
- [Analog Inputs](Hardware/Analog-Inputs.md): **SOURCE DRAFT, REVIEW REQUIRED.** Complete analog measurement chain, calibration and unresolved input-mode discrepancies.
- [Analog Outputs](Hardware/Analog-Outputs.md): **SOURCE DRAFT, REVIEW REQUIRED.** Voltage/PWM implementation, scaling discrepancy, output reset on configuration, and API behavior.
- [Digital Inputs](Hardware/Digital-Inputs.md): **SOURCE DRAFT, REVIEW REQUIRED.** Eight digital inputs, NPN conditioning, polarity, debounce, thresholds, wiring constraints, and software/API behavior.
- [Relay Outputs](Hardware/Relay-Outputs.md): **SOURCE DRAFT, REVIEW REQUIRED.** SPDT contact wiring, driver circuitry, logical/physical states, inversion, startup defaults, fail-safe implementation gap, and relay APIs.
- [Internal Interfaces](Hardware/Internal-Interfaces.md): **SOURCE DRAFT, REVIEW REQUIRED.** Pi GPIO/I2C electrical map, six peripheral addresses, Core hardware ownership, internal signal paths and noncustomer buses.
- [Mechanical and Assembly](Hardware/Mechanical-and-Assembly.md): **SOURCE DRAFT, REVIEW REQUIRED.** Rev B physical construction, Gerber ~230x150 mm outline, enclosure/service boundaries, assembly evidence and unknown installation dimensions.

### Core Software

- [Processes and Lifecycle](Core-Software/Processes-and-Lifecycle.md): **SOURCE DRAFT, REVIEW REQUIRED.** Core and WebBridge startup, client reconnection, Updater/Launcher probation, shutdown, and recovery.
- [Data Model and Configuration](Core-Software/Data-Model-and-Configuration.md): **SOURCE DRAFT, REVIEW REQUIRED.** Core config file/schema, runtime vs persisted values, WebBridge cache, backup and restore boundaries.
- [Operating Modes and Safety](Core-Software/Operating-Modes-and-Safety.md): **SOURCE DRAFT, REVIEW REQUIRED.** AO modes, PID automatic/enable flags, relay logic and explicitly unverified safety behavior.
- [Measurement and Control Timing](Core-Software/Measurement-and-Control-Timing.md): **SOURCE DRAFT, REVIEW REQUIRED.** Sequential analog sampling, ADS1115 delays, DI polling, PID cadence, logger and recorder rates.
- [Calibration and Scaling](Core-Software/Calibration-and-Scaling.md): **SOURCE DRAFT, REVIEW REQUIRED.** AI calibration functions, multipoint endpoint extrapolation, polynomial ordering and AO scaling conflict.
- [PID Control](Core-Software/PID-Control.md): **SOURCE DRAFT, REVIEW REQUIRED.** Four PID controllers, routing, P/I/D algorithm, clamping, ramps and unresolved mode transitions.
- [Logging and History](Core-Software/Logging-and-History.md): **SOURCE DRAFT, REVIEW REQUIRED.** Core SQL snapshots, event logs, WebBridge RAM chart recorder, history resampling and security findings.
- [Updates and Recovery](Core-Software/Updates-and-Recovery.md): **SOURCE DRAFT, REVIEW REQUIRED.** Updater package validation, release dispositions, Launcher probation/fallback, telemetry and OS tasks.

### User Interfaces

- [Control Panel](User-Interfaces/Control-Panel.md): **SOURCE DRAFT, REVIEW REQUIRED.** Windows desktop operation, discovery, live I/O, configuration dialogs, reconnection and device actions.
- [Device Website](User-Interfaces/Device-Website.md): **SOURCE DRAFT, REVIEW REQUIRED.** Device-site Apache/PHP/WebBridge chain, dashboard, control, chart recorder, properties, logs and settings.
- [Status and Diagnostics](User-Interfaces/Status-and-Diagnostics.md): **SOURCE DRAFT, REVIEW REQUIRED.** Core status, WebBridge cached state, logger diagnostics, error semantics and troubleshooting.

### Programming

- [Discovery and Connections](Programming/Discovery-and-Connections.md): **SOURCE DRAFT, REVIEW REQUIRED.** mDNS, port/address selection, SigCoreSystem managed connections, manual IP limitations and diagnostics.
- [DotNet API](Programming/DotNet-API.md): **SOURCE DRAFT, REVIEW REQUIRED.** SigCoreCommon managed connection, requests, writes, subscriptions, configuration and PID client semantics.
- [HTTP API and WebBridge](Programming/HTTP-API-and-WebBridge.md): **SOURCE DRAFT, REVIEW REQUIRED.** Device website PHP routes to private WebBridge, JSON schemas, channel writes, settings and chart history.
- [Python Integration](Programming/Python-Integration.md): **SOURCE DRAFT, REVIEW REQUIRED.** Verified HTTP integration from Python, pending example-distribution verification and native Core protocol limitations.
- [Integration Patterns](Programming/Integration-Patterns.md): **SOURCE DRAFT, REVIEW REQUIRED.** Telemetry, PID supervision, discrete I/O, logging, multi-client ownership, retries and safe external design.

### Operations

- [Installation and Commissioning](Operations/Installation-and-Commissioning.md): **SOURCE DRAFT, REVIEW REQUIRED.** Power-up, Windows setup, first connection, safe I/O commissioning, acceptance records and installation boundaries.
- [Networking and Access](Operations/Networking-and-Access.md): **SOURCE DRAFT, REVIEW REQUIRED.** DHCP/static addressing, mDNS versus Core reachability, Apache/PHP/WebBridge paths and deployment access boundaries.
- [Configuration and Restore](Operations/Configuration-and-Restore.md): **SOURCE DRAFT, REVIEW REQUIRED.** Persistent JSON configuration, backup/restore scope, secret handling, deferred restart, factory reset and validation.
- [Maintenance and Service](Operations/Maintenance-and-Service.md): **SOURCE DRAFT, REVIEW REQUIRED.** Service records, signed updates, restart/power boundaries, logging, field-service limitations and escalation.
- [Troubleshooting](Operations/Troubleshooting.md): **SOURCE DRAFT, REVIEW REQUIRED.** Symptom-based diagnosis for discovery, connection, website, I/O, PID, logger, restore and updates.

### Applied Engineering

- [Sensors and Signal Conditioning](Applied-Engineering/Sensors-and-Signal-Conditioning.md): **SOURCE DRAFT, REVIEW REQUIRED.** Compatible sensor output forms, DI/AI conditioning, voltage/current examples, calibration and fault handling.
- [Actuators and Loads](Applied-Engineering/Actuators-and-Loads.md): **SOURCE DRAFT, REVIEW REQUIRED.** Relay load interfaces, AO voltage/PWM drivers, external protection, contact versus logical state and feedback.
- [Data Acquisition and Monitoring](Applied-Engineering/Data-Acquisition-and-Monitoring.md): **SOURCE DRAFT, REVIEW REQUIRED.** Acquisition rates, live/cached/history/SQL paths, timestamps, data quality, chart resampling and read-only monitoring.
- [Control Applications](Applied-Engineering/Control-Applications.md): **SOURCE DRAFT, REVIEW REQUIRED.** Core PID control, sequence supervision, writer ownership, fault responses and external safety boundaries.
- [External System Integration](Applied-Engineering/External-System-Integration.md): **SOURCE DRAFT, REVIEW REQUIRED.** .NET, browser PHP/WebBridge and database boundaries, discovery, adapter design, authorization and command semantics.
- [Worked Designs](Applied-Engineering/Worked-Designs.md): **SOURCE DRAFT, REVIEW REQUIRED.** Illustrative AO-to-AI, thermal PID, pump/valve sequencing and durable monitoring designs with commissioning checks.

### Reference

- [Electrical Specifications](Reference/Electrical-Specifications.md): **SOURCE DRAFT, REVIEW REQUIRED.** Rev B electrical ratings and explicit gaps; power, digital and analog I/O, contacts, signal references and source classification.
- [Protocol and Data Reference](Reference/Protocol-and-Data-Reference.md): **SOURCE DRAFT, REVIEW REQUIRED.** TCP JSON envelope, version-scoped command IDs, PHP/WebBridge route schemas, data units and acknowledgment semantics.
- [Events and Errors](Reference/Events-and-Errors.md): **SOURCE DRAFT, REVIEW REQUIRED.** Core alerts, TCP and HTTP errors, structured log events, update dispositions and diagnostic interpretation.
- [Glossary](Reference/Glossary.md): **SOURCE DRAFT, REVIEW REQUIRED.** Consistent terminology for hardware, Core, APIs, control state, versions, history and AI answers.
- [Known Limitations](Reference/Known-Limitations.md): **SOURCE DRAFT, REVIEW REQUIRED.** Published limits, source-snapshot defects, unresolved ratings, deployment gaps and software-version caveats.

## Visual assets

- [Image inventory](Images/IMAGE-INVENTORY.md).
- Nine Control Panel screenshots, two actual controller photographs, four Rev B schematic excerpts, one top-overlay illustration, and one device Chart Recorder screenshot are available and linked from technical chapters. Further images will be selected as chapters are authored.

## Project-only materials (exclude from released customer package)

- Project plan (not yet written)
- [Source inventory](project/Source-Inventory.md)
- [Verification register](project/Verification-Register.md)
- AI validation plan (not yet written)
