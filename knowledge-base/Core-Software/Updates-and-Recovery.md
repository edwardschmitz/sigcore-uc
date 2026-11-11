---
title: "Updates and Recovery"
status: source-draft-review-required
hardware_revisions: "Rev B context"
software_versions: "Supplied unversioned source snapshot; exact release/commit unknown"
source_review_date: 2026-11-06
customer_release_approved: false
---

# Updates and Recovery

> **Engineering source draft, not customer-approved.** This is the behavior implemented in the supplied sources, not a guarantee about a specific installed build. Do not conflate *downloading*, *preparing*, *activating*, *passing probation*, and *application health*. These are different states. This chapter covers the Rev B update framework; the proposed separate Version Selector is **Rev C/3.0.0 work**, not an implemented Rev B capability.

## 1. Responsibility boundaries

| Responsibility | Component | Source evidence |
|---|---|---|
| Check manifest and decide whether a compatible update is available | `SigCoreUpdater` | `SigCoreUpdater/Updater.cs`, `UpdateDecisionEngine.cs` |
| Validate signed manifest and update package | `SigCoreUpdater` | `ManifestValidator.cs`, `ScuVerifier.cs`, `Updater.cs` |
| Extract and validate new release | `SigCoreUpdater` | `VersionInstaller.cs` |
| Maintain installed release dispositions | Shared release state store | `libs/SigCoreCommon/Update/` models, `Updater.cs`, `SigCoreLauncher/Launcher.cs` |
| Select, activate and supervise running processes | `SigCoreLauncher` | `SigCoreLauncher/Launcher.cs`, `ReleaseManager.cs` |
| Execute one-time OS/package tasks from **active** release | `SigCoreUpdater` on its own startup | `SigCoreUpdater/InstallationTaskRunner.cs` |
| Device status readout of update configuration | Core status protocol | `Server/SigCoreServer/CommandDispatcher.cs` |

See [Software Architecture](../Architecture/Software-Architecture.md) and [Processes and Lifecycle](Processes-and-Lifecycle.md) for the process model, including uncertainties about installed systemd units.

## 2. Automatic preparation sequence

1. Updater loads the active release's `config.json`, device feed configuration, and `/opt/sigcore/releases.json`.
2. On startup, it first processes any pending installation tasks belonging to the **currently active** release. Failed tasks are logged and do not block the update loop.
3. Updater fetches manifest JSON and its detached signature into `/tmp/sigcore`. The manifest must validate against `/opt/sigcore/keys/sigcore_public.pem`.
4. It verifies the manifest's hardware revision matches the current release and selects the manifest entry named by `latestVersion`.
5. It compares installed and offered versions. **Automatic cross-major-version upgrades are rejected**; they require a deliberate manual system upgrade. The source compares major version numbers. Do not assume automatic Rev B-to-Rev C migration.
6. If the target is already recorded in a non-`STAGING` disposition, Updater exits this cycle without reinstalling it.
7. Updater marks the new version `STAGING` and removes an incomplete staging/final directory for that version.
8. It downloads the `.scu` package and signature, validates both the expected SHA-256 hash and signature, and then optionally reports `DOWNLOADED` to the configured telemetry service.
9. `VersionInstaller.Prepare()` extracts to `/opt/sigcore/versions/.staging-<version>`, checks `config.json` version/revision, validates the presence of the declared executable paths and marks them executable on Linux.
10. It renames the fully prepared staging directory into `/opt/sigcore/versions/<version>` and marks the release `READY`.

The download-check cadence is **every six minutes requested between cycles** (`Updater.CheckInterval`). Network/download duration and task execution add time; this is **not** a six-minute activation guarantee. Failed preparation logs an error while the active release remains in place.

## 3. Installed release states and selection

| Disposition | Meaning from source | Selection implications |
|---|---|---|
| `STAGING` | Preparation started or incomplete | Not eligible for Launcher selection |
| `READY` | Package verified, extracted and validated; not yet probated | Eligible for selection when Launcher selects a release; requires probation |
| `GOOD` | Launcher completed its probation for that release | Eligible for selection and fallback |
| `FAILED` | Selected candidate failed activation/probation | Not eligible for normal highest-eligible selection |
| `REGRESSED` | Planned/reported in broader project design | **Not demonstrated as an active choice in supplied Launcher code**; verify before describing as available |

Launcher chooses the **highest** version with disposition `READY` or `GOOD` on its startup (`ReleaseManager.SelectHighestEligible`). It switches `/opt/sigcore/current` by replacing the symbolic link with an atomic filesystem rename, loads the selected release's `config.json`, then launches configured executables. Updater's transition to `READY` does **not**, by itself, prove that Launcher restarts or activates it immediately. The missing activation trigger must be verified against installed services and deployed release scripts.

## 4. Probation and fallback

For a `READY` release, Launcher requests a **30-second** probation period. Each second it checks that every configured child process has **not exited**. Successful survival results in disposition `GOOD`; exit or activation error results in `FAILED`, followed by selection and activation of the highest previously `GOOD` release. Fallback itself gets a two-second startup/process-survival check. Launcher then supervises running children about every two seconds and attempts to restart an individual child that exits.

**Limitations:** Process survival does *not* prove Core's TCP listener responds, hardware is functional, PID is healthy, WebBridge can connect to Core, the HTTP UI works, or connected equipment is in a safe state. When Launcher stops children it uses `Kill(entireProcessTree: true)`, not a graceful command/acknowledgment handshake. Do not promise fail-safe output transfer or uninterrupted process control during release changes. See [Operating Modes and Safety](Operating-Modes-and-Safety.md).

If a failed candidate has no previously `GOOD` release, successful fallback is not established. A device stuck in a crash/restart loop needs service-level diagnostics; merely reading a `GOOD` disposition is not a functional acceptance test.

## 5. Installation tasks

An active release may include `installation-tasks.json` with unique task IDs, commands and optional verification checks. Updater runs those tasks **once per its own startup** against the active release and records each completed ID in `/opt/sigcore/completed-tasks.json` using an atomic replacement. A failed task remains incomplete and is retried at a later Updater startup, not on every six-minute update poll.

Example from the supplied `UpdaterFiles/installation-tasks.json`: check for PHP `pdo_mysql`; otherwise invoke `apt-get install -y php-mysql`. This is an example of an **operating-system dependency task**, not evidence that the website is guaranteed to be functional after any particular boot. Execution privileges, package availability, network access and installed service configuration must be verified in deployment.

## 6. Telemetry and customer data boundary

The reviewed `SigCoreUpdater/UpdateEventReporter.cs` has an **optional** HTTPS `DOWNLOADED` report after successful package validation. It reads serial number from `properties.json`, hardware revision, target software version and UTC occurrence time. It reads the destination URL and bearer-style token from `/opt/sigcore/update-telemetry.json`. Missing/invalid telemetry configuration, server rejection or network failure **does not prevent update preparation**.

The reviewed update reporter does **not** send measurement readings, I/O states, process configurations or logs. This narrow statement refers to **this source method**; it does not establish the complete deployed telemetry/event policy. Project plans discuss additional `READY`, `GOOD`, `FAILED`, and `REGRESSED` reporting; those reporters are **not shown in the supplied Updater/Launcher snapshot**. The customer-facing privacy and automatic-update policies on the public product website should be checked against the deployed build before final approval.

## 7. Troubleshooting decisions

| Observation | What it does establish | What to check next |
|---|---|---|
| Update feed unreachable | Updater may not discover a new release | Network/DNS, manifest URL, Updater log, time/certificate configuration |
| Manifest rejected | Signature invalid or manifest unusable for revision | Feed revision, signature/public key, manifest format, device revision |
| Package verification failed | Hash or signature validation rejected package | Download integrity, manifest hash, signature; do **not** bypass verification |
| `READY` remains present | Package prepared but not necessarily active | Current symlink, Launcher startup/selection trigger, deployed services |
| New release becomes `FAILED` | Launcher detected activation/probation failure | Which process exited, `config.json`, dependencies, latest `GOOD` fallback |
| `GOOD` but website broken | Processes survived probation | Core connection, PHP/Apache/WebBridge health and logs |
| Device reachable but update not installed | Connectivity does not prove activation | `/opt/sigcore/current`, release disposition and Launcher lifecycle |

Never advise an operator to delete release state, change the `current` symlink manually or disable signature validation as a routine troubleshooting step. Capture device serial, hardware revision, installed version, target version, relevant logs and reported disposition before invasive recovery.

## 8. Engineering verification required

- Identify the exact source commit for the released Rev B build and its installed systemd units; verify who restarts Launcher to select a `READY` package.
- Confirm how `READY`, `GOOD`, `FAILED` and `REGRESSED` are reported in **deployed** telemetry, beyond `DOWNLOADED` shown in this snapshot.
- Confirm how the currently running Updater is related to the selected release and whether its configuration is refreshed after a release transition.
- Test fallback when a candidate crashes, refuses to start, passes process probation but is functionally broken, and when there is no viable `GOOD` release.
- Confirm physical output and PID behavior during abrupt process termination and automatic restart.
- Verify installer tasks, service ownership, permissions and persistence on a real unit.

## Evidence inventory

`SigCoreUpdater/Updater.cs`; `UpdateDecisionEngine.cs`; `ManifestDownloader.cs`; `ManifestValidator.cs`; `ScuVerifier.cs`; `VersionInstaller.cs`; `InstallationTaskRunner.cs`; `UpdateEventReporter.cs`; `SigCoreLauncher/Launcher.cs`; `SigCoreLauncher/ReleaseManager.cs`; `UpdaterFiles/installation-tasks.json`; `Server/SigCoreServer/CommandDispatcher.cs`. All paths are relative to `project/sources/SigCore-Software-Source/` in the **development** archive, not paths included in the released customer KB.
