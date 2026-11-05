# SigCore UC AI Knowledge Base: Table of Contents

> **INTERNAL DRAFT. NOT FOR CUSTOMER DISTRIBUTION.** System Architecture and Analog Inputs now contain source-grounded first drafts requiring engineering review. All remaining subject chapters are placeholders and must not be used as evidence of product capabilities.

This collection is designed to provide technical knowledge to a customer-selected AI service, **not** to serve as a conventional readable manual. For human-oriented instructions, use the separate SigCore UC support references.

Begin with [README.md](README.md) for usage and verification rules, and [VERSION.md](VERSION.md) for release status.

## Contents

### Architecture

- [Product Overview](Architecture/Product-Overview.md): Product purpose, product boundaries, interfaces, and terminology.
- [System Architecture](Architecture/System-Architecture.md): **SOURCE DRAFT, REVIEW REQUIRED.** Hardware/software components and their relationships.
- [Revisions and Feature Availability](Architecture/Revisions-and-Feature-Availability.md): Hardware revisions, software versions, compatibility, and current-versus-planned features.

### Hardware

- [Hardware Overview](Hardware/Hardware-Overview.md): Board architecture, physical design, and signal/power paths.
- [Power and Protection](Hardware/Power-and-Protection.md): Supply requirements, rails, regulators, and protective elements.
- [Connectors and Pinouts](Hardware/Connectors-and-Pinouts.md): Connector identification and pin-level electrical reference.
- [Analog Inputs](Hardware/Analog-Inputs.md): **SOURCE DRAFT, REVIEW REQUIRED.** Complete analog measurement chain, calibration and unresolved input-mode discrepancies.
- [Analog Outputs](Hardware/Analog-Outputs.md): Analog and PWM output electrical behavior and usage.
- [Digital Inputs](Hardware/Digital-Inputs.md): Digital input sensing, thresholds, timing, and wiring.
- [Relay Outputs](Hardware/Relay-Outputs.md): Relay drive circuits, contact ratings and application constraints.
- [Internal Interfaces](Hardware/Internal-Interfaces.md): Raspberry Pi connections, on-board buses, and internal components.
- [Mechanical and Assembly](Hardware/Mechanical-and-Assembly.md): Board geometry, enclosure, mounting, and service access.

### Core Software

- [Processes and Lifecycle](Core-Software/Processes-and-Lifecycle.md): Core, supporting processes, startup, shutdown, and supervision.
- [Data Model and Configuration](Core-Software/Data-Model-and-Configuration.md): Internal state, persistence, configuration, and property semantics.
- [Operating Modes and Safety](Core-Software/Operating-Modes-and-Safety.md): Manual, automatic and override modes, transitions, and safety.
- [Measurement and Control Timing](Core-Software/Measurement-and-Control-Timing.md): Sampling, update timing, signal processing, and event sequencing.
- [Calibration and Scaling](Core-Software/Calibration-and-Scaling.md): Calibration algorithms, scaling, conversions and extrapolation.
- [PID Control](Core-Software/PID-Control.md): PID controller implementation and operational behavior.
- [Logging and History](Core-Software/Logging-and-History.md): Measurements, logs, history retrieval, and recorder behavior.
- [Updates and Recovery](Core-Software/Updates-and-Recovery.md): Updater, Launcher, version tracking, recovery and telemetry.

### User Interfaces

- [Control Panel](User-Interfaces/Control-Panel.md): Desktop Control Panel screens, actions, fields and behavior.
- [Device Website](User-Interfaces/Device-Website.md): On-device web interface and operating procedures.
- [Status and Diagnostics](User-Interfaces/Status-and-Diagnostics.md): Status reporting across interfaces and interpretation.

### Programming

- [Discovery and Connections](Programming/Discovery-and-Connections.md): Discovery, addresses, ports, connectivity, and reconnection.
- [DotNet API](Programming/DotNet-API.md): Verified public .NET API classes and contract.
- [HTTP API and WebBridge](Programming/HTTP-API-and-WebBridge.md): On-device HTTP bridge endpoints and request/response semantics.
- [Python Integration](Programming/Python-Integration.md): Supported Python access patterns and code examples.
- [Integration Patterns](Programming/Integration-Patterns.md): External applications and automation integration patterns.

### Operations

- [Installation and Commissioning](Operations/Installation-and-Commissioning.md): Installation, first boot, validation, and acceptance.
- [Networking and Access](Operations/Networking-and-Access.md): IP addressing, mDNS, services, ports and network exposure.
- [Configuration and Restore](Operations/Configuration-and-Restore.md): Configuration management, identity, backups and recovery.
- [Maintenance and Service](Operations/Maintenance-and-Service.md): Updates, periodic checks, hardware service and field diagnosis.
- [Troubleshooting](Operations/Troubleshooting.md): Symptom-oriented diagnosis and corrective actions.

### Applied Engineering

- [Sensors and Signal Conditioning](Applied-Engineering/Sensors-and-Signal-Conditioning.md): Integrating common sensors with accepted input forms.
- [Actuators and Loads](Applied-Engineering/Actuators-and-Loads.md): Output interfacing with relays, analog loads and actuators.
- [Data Acquisition and Monitoring](Applied-Engineering/Data-Acquisition-and-Monitoring.md): Measurement collection, persistence, trends and analysis.
- [Control Applications](Applied-Engineering/Control-Applications.md): Designing practical feedback and supervisory control applications.
- [External System Integration](Applied-Engineering/External-System-Integration.md): Connecting SigCore to other devices and systems.
- [Worked Designs](Applied-Engineering/Worked-Designs.md): End-to-end worked engineering applications.

### Reference

- [Electrical Specifications](Reference/Electrical-Specifications.md): Consolidated verified electrical data with revision scope.
- [Protocol and Data Reference](Reference/Protocol-and-Data-Reference.md): Concise wire protocol, API schemas, and definitions.
- [Events and Errors](Reference/Events-and-Errors.md): Canonical messages, statuses, states, and error conditions.
- [Glossary](Reference/Glossary.md): Common terminology, abbreviations, and naming standards.
- [Known Limitations](Reference/Known-Limitations.md): Verified constraints, unsupported configurations, and known issues.

## Visual assets

- [Image inventory and requested asset list](Images/IMAGE-INVENTORY.md).
- Images currently **not included**: inventory entries specify assets to capture, export, verify and add.

## Project-only materials (exclude from released customer package)

- [Project plan](Project/Project-Plan.md)
- [Source inventory](Project/Source-Inventory.md)
- [Verification register](Project/Verification-Register.md)
- [AI validation plan](Project/AI-Validation-Plan.md)
