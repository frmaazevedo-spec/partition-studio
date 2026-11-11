# Operations and validation limits

## Inventory and partition operations

The main window reads disk identity, size, state, partition layout and volumes. Queue a create/resize/format/delete or repair operation and review the destination before applying. Only one partition operation per disk may be queued. Identity and expected layout are checked again before those operations. Formatting and deletion erase data. There is no transactional rollback, and an OS-level failure can leave a partially changed disk.

The integrated partition manager refuses system/boot disks, hidden/special partitions, offline/read-only disks, unsupported partition types and identified encrypted volumes. Unavailable encryption-provider information can prevent an operation. Creation uses contiguous unallocated space and does not format automatically. Refresh and format the new partition separately when appropriate. Resize and filesystem repair require supported NTFS volumes. It does not move/merge partitions or migrate the running OS.

## Health

Health/reliability counters are requested manually. Temperature, hours, cycles, error counts and wear may be unavailable through a particular controller/USB adapter. Raw ATA attributes are manufacturer-dependent; an ATA temperature attribute's raw integer is not automatically a Celsius value. There is no NVMe-specific SMART decoder or background health alert service. A Windows `Healthy` state is not a guarantee against failures.

## Optimization and sectors

Optimization invokes Windows `Optimize-Volume` without forcing the same method on every device. Windows selects appropriate optimization according to the volume/device support. CHKDSK /r is a separate, modifying operation for a selected NTFS data volume; it does not force a dismount or schedule a restart and can fail while the volume is in use. Its bad-cluster marking is logical, not a promise of physical sector remapping.

The read-only sector scan reads large blocks, retries failing blocks at logical-sector granularity and records offsets/errors. A report distinguishes a complete scan from cancellation or an error-limit stop. The limit is 256 consecutive errors or 10,000 total errors. Errors can have causes other than media damage. Scanning a failing drive can stress it; prioritize data recovery when failure is suspected. The scanner cannot force firmware relocation.

## Running Windows backup

The VSS route uses `wbadmin start backup`, includes the current Windows volume and critical volumes and requests a copy backup. The selected NTFS destination must be on another physical disk. The folder chooser selects a volume: Windows manages its `WindowsImageBackup` folder at the root. Previous backups and free space are managed by Windows. Availability depends on Windows features/configuration. Detailed output is returned at completion; this interface does not cancel wbadmin. Restore of WindowsImageBackup is performed through Windows recovery tools, not this application's RAW image restore.

## Full images, clone and restore

RAW images include all sectors, allocated and unallocated space, all partition tables and any installed OS. Sources must be secondary offline disks for a consistent copy; the running Windows disk is protected. A secondary attached OS disk can be imaged without booting its installed OS. No boot environment is bundled.

The image destination must have enough space on another physical disk. Files are not compressed or encrypted. A verified `.psimg` image and its `.psimg.json` manifest must be kept together. `.partial-*` files indicate incomplete backups and are not valid verified images. This is not a forensic acquisition engine: it has no bad-sector tolerance map or automatic resume.

Clone/restore targets must be secondary, offline, writable devices with **exactly equal size and logical sector size**. Even a larger disk is rejected. There is no GPT relocation, partition expansion or filesystem conversion in the copy. A clone source is opened read-only. Native handles are checked for device number/length and disk identity/state is re-read before copying. Image restore checks its source SHA-256 before writing. All complete copies are re-read for verification.

Destination erasure requires typing `APAGAR DISCO N`. Interrupted writes leave the target incomplete; cancellation is not rollback. Disks remain offline after a copy, and clones have duplicate partition/disk identifiers. Do not put both online without considering identifier conflicts. Byte equality does not guarantee OS boot, firmware/driver support, licensing or Secure Boot compatibility.

## Optional recovery tools

TestDisk and PhotoRec run separately when an official upstream package has been installed by the operator. Confirm the disk again inside the selected tool. The application's protections do not control writes within TestDisk. Recover files to a different physical disk. File carving may lose names and folders. This application does not repair the internal structure of a corrupt document or video.

## Validation before recommending release

Initial file-only tests cover transfer hashes, truncation, cancellation, metadata validation, simulated sector errors and device-state/geometry guards. Mocked PowerShell tests exercise refusals before mutation. They intentionally do not call the physical copying APIs, wbadmin, CHKDSK or Optimize-Volume against actual devices.

The following remain pending: final candidate tests on an authorized runner, real disposable-disk copy/restore and bad-sector-scan validation, VSS backup/recovery validation, optimization and CHKDSK integration checks, hot-unplug/error behavior, encryption cases and an OS boot test. Record disk sizes, logical/physical sector geometry, before/after layout, data hashes and exact error/cancellation outcomes using synthetic data. Do not collect an operator's real disk identifiers or private contents in public CI artifacts.

Windows Smart App Control blocked local final executable re-tests. Respect that boundary and sign through an approved provider; do not relabel an unsigned file, change a loader or change system trust to make that blocked file run.

