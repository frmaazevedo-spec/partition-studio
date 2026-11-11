# SignPath Foundation application — preparation draft

**Not submitted. No signing account/project/policy has been created.** Do not submit this draft as a claim that the project already has a public release, verified build history or established user reputation.

## Proposed project details

| Field | Draft value / status |
| --- | --- |
| Project | Partition Studio |
| Category | Experimental Windows disk management and backup utility |
| Original-source license | MIT; platform/runtime dependencies retain their own licenses |
| Proposed repository owner | `frmaazevedo-spec` — authenticated GitHub account identified during preparation |
| Proposed repository name | `partition-studio`; repository does not exist as a result of this preparation |
| Repository/homepage URL | Pending creation and verification; no URL should be represented as live yet |
| Public download/release URL | Pending successful CI and explicit publication of an unsigned preview |
| Contact email | Must be provided privately by the owner in the provider's application; not collected or published here |
| Legal identity / signer | No identity invented or asserted from the GitHub login |
| Maintainer location/type | Portugal, individual, as stated by the owner |
| Source/build | C# / Windows Forms / .NET 8; original embedded PowerShell Storage scripts |
| Requested signing scope | First-party `PartitionStudio.exe` from a reviewed CI artifact; optional upstream recovery tools excluded |
| Current signing status | Unsigned; Windows Smart App Control blocked local execution of the 0.3 binary |
| Public reputation evidence | None established for this newly prepared project; do not invent stars, users, references, audits or maintenance history |

## Proposed application text (only after the missing facts are completed)

Partition Studio is a Windows utility for inspecting disks and partitions and performing explicitly requested maintenance, recovery-launcher and backup operations. It is an independent project and is not affiliated with MiniTool. Its original source is provided under the MIT license.

The project is new and experimental. It has no established reputation or public release history at this preparation stage. We understand that your Foundation requires verifiable project reputation and may decline an application that does not meet this standard. We are not requesting an exception or a way around your signing constraints.

The proposed first-party artifact is PartitionStudio.exe, built from a reviewed commit through a Windows GitHub Actions workflow. The workflow uses pinned actions, a read-only repository token and file-only tests, plus mocked storage command tests. It does not request a signature, publish a release or execute physical disk operations. The initial workflow still needs its first verified public run. Optional CGSecurity tools are acquired separately from their upstream publisher and will not be signed with this project's identity.

The application has no implemented telemetry or automatic diagnostic upload. Disk inventories, images, clone contents and recovery output remain under the operator's control. Potentially destructive operations identify a destination and require explicit confirmation. Physical-disk copying and restored-system boot behavior still require disposable-device integration testing; signing must not be interpreted as validation of those functions.

Before requesting any release signature, the maintainer will satisfy account MFA and repository review requirements, finalize the signing policy/roles, review artifact metadata and confirm the reviewed source commit. No provider tokens or signing keys are committed. Manual approval will be required for each signing request.

## Prerequisites before submission

- Owner reviews this source package and approves creation of the proposed public repository.
- Enable and verify account MFA; establish source-review and release-approval responsibilities.
- Publish a real project homepage/readme and a traceable unsigned preview after CI succeeds. Keep experimental and unsigned labels explicit.
- Run and review CI; link the source commit, logs and artifact. Fix any failure instead of treating the draft workflow as a successful build.
- Complete device testing and document outstanding limitations. A successful simulated test is not a physical-disk validation.
- Establish and honestly document externally verifiable project reputation; provider assessment is independent and has no promised deadline.
- Provide a contact address directly in the provider's form. Do not send identity documents, passwords or signing secrets in a repository issue or chat.
- Only then submit an accurate request through the official provider route and await its decision.

## Source references checked 2026-11-11

- https://signpath.org/terms
- https://signpath.org/apply
- https://learn.microsoft.com/windows/apps/package-and-deploy/code-signing-options

The Foundation evaluates reputation and enforces a link between the repository, build and signed artifact. Preparation of this repository does not confer membership, a right to signing or an approved certificate.

