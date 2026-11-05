# Source Inventory

**Baseline received:** `sources(2).zip`, reviewed 2026-11-02. The extracted `Sources/` tree is maintained independently by the user; this register records its contents and authority, and should not package duplicate source material. User-supplied source paths are reproduced, not renamed.

| Source category | Available source | Authority and notes |
|---|---|---|
| Current customer documentation | `Sources/documentation/hardware-reference.html`, `control-panel-reference.html`, `api-reference.html` | **Product website reference pages are authoritative**. File contents in snapshot match `Sources/SigCore-Software-Source/Web/public_html/reference/` byte-for-byte. |
| Rev B schematic | `Sources/hardware/SCH_Schematic1_2026-08-15.pdf` | Six-sheet electrical design; first analog input review uses sheet 3. |
| BOM | `Sources/hardware/BOM_Main_PCB17_2026-08-15.xlsx` | Available; not exhaustively audited in this first drafting pass. |
| PCB | `Sources/hardware/Gerber_PCB17_2026-08-15/` | Four-layer and related fabrication files; not needed for the first architectural overview. |
| Overlay | `Sources/hardware/top-overlay.pdf` | Physical channel/terminal group identifiers; evaluation-unit marking. |
| Core and hardware software | `Sources/SigCore-Software-Source/libs/SigCoreCommon/`, `Server/SigCoreServer/` | Implementation evidence, **not** proof all code is deployed to a specific installed release. |
| WebBridge and on-device web | `Sources/SigCore-Software-Source/WebBridge/`, `UpdaterFiles/www/` | Runtime implementation evidence and website assets, including PHP proxy. |
| Control Panel | `Sources/SigCore-Software-Source/ControlPanel/` | C# and XAML included. |
| Launcher/Updater | `Sources/SigCore-Software-Source/SigCoreLauncher/`, `SigCoreUpdater/` | Implementation evidence; complete deployment/health checks not yet verified. |
| Images/screenshots | `Sources/images/`, `Sources/documentation/images/`, plus screenshots separately shared in conversation | Development references; select and approve images into future `KnowledgeBase/Images/`. |
| 3D CAD | Separately uploaded STEP files | Useful development sources, not included in initial customer ZIP. |
| Measured performance | No system characterization dataset supplied | Explicit knowledge boundary; no attempt to guess measured results. |

## Source authority and conflicts

- Product website customer reference pages are the authoritative descriptions of **documented** product features. Obsolete device website reference copies have been removed and must not be treated as current.
- Implementation code supplies evidence for **actual software mechanisms**, but filenames alone do not prove enabled capability, customer support status, or exact release scope.
- Schematic, component datasheet, calculated performance and measured performance are distinct categories.
- Where documented feature names and code disagree, record the discrepancy in `Verification-Register.md`, not a silently reconciled customer claim.
- Do not include credentials or private configuration from development sources in customer output. The source package may contain sensitive `Web/includes/secrets.php`; never reproduce its contents.
