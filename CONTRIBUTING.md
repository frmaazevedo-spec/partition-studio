# Contributing

Keep changes reviewable and describe their intended disk-operation behavior. Never use real disks for automated tests. Run the file-only/model tests and mocked PowerShell guard tests in an authorized Windows test environment; record policy blocks rather than changing the environment's security controls.

Changes to device access, identity checks, offline constraints, destination confirmation, copy length, integrity verification, encryption handling or process invocation require explicit review. Use disposable VHD/test disks and recorded before/after results for later integration testing. Do not treat a successful compile or a simulated sector-error test as physical-disk validation.

The project does not yet have a complete physical-disk validation matrix or signed release. Contributions must not claim these milestones have been achieved. Keep secrets, personal disk inventories, recovery images and signing certificates outside the repository. License original contributions under the project's MIT license.

