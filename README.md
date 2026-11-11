# Partition Studio

Experimental Windows disk management, recovery launcher, health inspection, and backup/cloning utility. Independent project; not affiliated with MiniTool, Microsoft, CGSecurity, or SignPath.

**Status: source publication draft, 0.3.1-preview.1. Not signed, not approved by SignPath, and not production-ready.** Windows Smart App Control blocked execution of the local unsigned 0.3 build. This source candidate does not resolve that block by itself. The project is seeking a legitimate signing route without changing Windows security policies.

## Features and limits

- Real disk/partition inventory and graphical storage map.
- Queued create/resize/format/delete operations with review and confirmations. System disks and special partitions are protected in the integrated partition manager.
- NTFS verification/repair, Windows-managed optimization/defragmentation/TRIM, and read-only sector scanning with an error report.
- Windows health/reliability counters and raw ATA SMART attributes, when the hardware, driver and permissions expose them. Unsupported fields remain unavailable; no invented health percentages.
- Windows system-volume backup through wbadmin/VSS, where installed and supported.
- Full offline disk images, restore and sector-by-sector cloning with SHA-256 verification. Clone/restore require **exactly equal capacity and logical sector size**. They do not expand or repair a GPT for a larger destination. The running Windows disk cannot be a RAW clone/restore target.
- Optional launch of official TestDisk/PhotoRec tools. They are not bundled in this source candidate; see [THIRD-PARTY.md](THIRD-PARTY.md).

Physical sector relocation is controlled by device firmware. CHKDSK bad-cluster marking is not physical remapping. Disk images include the entire disk, including unused space, and are not encrypted by this application.

## Experimental disk writes

**Do not use this preview on irreplaceable data.** RAW copy/restore, physical-disk scanning, wbadmin/VSS, optimization and CHKDSK /r have not been validated on disposable physical disks during initial development. File-only/mocked tests and successful compilation do not replace that validation. No cloned operating system has been boot-tested. Interrupted clone/restore operations can leave their target incomplete; no rollback is implemented.

The local 0.3 build passed model, file-copy/hash, simulated sector-error and UI tests in an earlier build. Windows App Control prevented final executable re-tests after subsequent adjustments. The added error-limit test is pending execution in an authorized test environment. The draft CI workflow has **not yet run on GitHub**. No automated test intentionally opens a physical disk, invokes wbadmin, or repairs/optimizes a real volume.

## Build and test

Requirements: Windows 10/11 x64, .NET SDK 8.0.424, PowerShell 7 for scripts, and Windows PowerShell 5.1/Storage for the app's backend. Sensor access and disk operations need elevation; test switches do not.

```powershell
./scripts/Build.ps1
./scripts/Test.ps1
./scripts/Package.ps1
```

The unsigned executable is generated in `artifacts/app`. `Test.ps1` uses test switches with synthetic data and workspace files; storage guard scripts replace storage commands with mocks. The unsigned ZIP is packaged only after those tests pass. On a device that blocks the binary, stop and record the failure; do not change App Control or use another loader to evade the restriction.

GitHub Actions builds on `windows-2022`, pins action commits, uses a read-only token, executes file-only tests and uploads an explicitly unsigned candidate. There is no signing secret, signing request, public release publishing or deployment in that workflow. A trusted CI build is a traceable build, not a claim of bit-for-bit reproducibility across different SDK/runtime environments.

## Portable use and removal

Extract a release to its own folder and open `PartitionStudio.exe` only when Windows accepts it. Review every operation's selected disk and destination. Optional recovery tools have their own confirmations and are not controlled by the app's partition queue. See [docs/OPERATIONS.md](docs/OPERATIONS.md).

The application has no installer/service or autostart entry. Close it and delete its extracted folder to remove it. Preserve backups, recovered data, manifests and reports you still need. Removing the application does not undo any disk changes, restore deleted partitions, or remove backups managed by Windows.

## Code signing policy

**No signing provider has accepted this project. No signed release is currently available.** Proposed maintainership, review and release approval: [frmaazevedo-spec](https://github.com/frmaazevedo-spec), subject to confirmation when the repository is created. See [SIGNING.md](SIGNING.md), [PRIVACY.md](PRIVACY.md) and [docs/SIGNPATH-APPLICATION-DRAFT.md](docs/SIGNPATH-APPLICATION-DRAFT.md).

The MIT license applies to original project code; bundled system runtimes and optional upstream recovery utilities retain their own licenses. See [LICENSE.txt](LICENSE.txt) and [THIRD-PARTY.md](THIRD-PARTY.md).

