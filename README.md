# SigCore UC – Universal Controller

**Status:** In Development  
**Version:** v0.1 (prototype phase)

SigCore UC is a modular, high-precision universal I/O controller designed for engineers, developers, and integrators working in automation, control systems, and experimentation.

It supports a broad range of analog, digital, and current-loop signals, and is built to be both powerful and transparent—from PCB to Python.

---

## Key Features

- **Analog Inputs**
  - Millivolt to 30V input range
  - 4–20 mA loop support
  - Configurable filtering and calibration
- **Digital Inputs**
  - Logic level support from 3V to 30V
  - Built-in protection and detection
- **Relay Outputs**
  - High-current mechanical and solid-state options
  - Default-state configuration and software override
- **Analog Outputs**
  - Voltage and 4–20 mA current-loop output modes
- **Communication**
  - Modbus RTU over RS-485
  - USB/Serial interface
  - Optional OPC UA and Web API integration
- **Extensibility**
  - Python and C# libraries
  - MMF-based driver architecture
  - Modular firmware upgrade path

---

## Folder Structure

hardware/           # Schematics, layout files, overlay graphics
software/
├── drivers/        # Device drivers (Python, C#)
├── control-panel/  # Main application UI
├── libs/           # Shared libraries
firmware/           # Embedded firmware (if applicable)
tools/              # Diagnostic and utility tools
docs/               # User guides, wiring diagrams, spec sheets
assets/             # Images, branding, front panel renders
test/               # Validation scripts, loopbacks, automated tests

---

## License

[MIT License](LICENSE) — open for inspection, modification, and contribution.

---

## About This Repo

SigCore UC is the successor to the [D88A42 Prototype](https://github.com/edwardschmitz/D88A42-Prototype).  
It brings forward lessons learned and raises the ceiling for custom I/O control systems.
