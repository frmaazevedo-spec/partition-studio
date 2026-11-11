# Code signing policy — not yet active

Signing status: **none**. SignPath Foundation application status: **draft, not submitted, not approved**. There is no signing certificate, account token, approved project identifier or signing policy identifier configured in this repository.

## Proposed responsibilities

- Source maintainer/committer: GitHub account `frmaazevedo-spec`, subject to confirmation.
- Reviewer: the same maintainer for external contributions; additional reviewers may be appointed as the project grows.
- Release/signing approver: the maintainer, using an explicit manual approval after reviewing source revision, CI result, artifact and disk-operation risk.

MFA must be enabled on repository and signing accounts before enrollment; it has **not been verified or configured** in this preparation. Repository branch protection/review rules and a provider-side manual approval must be configured before a signed release. A CODEOWNERS file alone does not enforce those rules.

## Eligible signing scope

Sign only the first-party `PartitionStudio.exe` built from the exact reviewed source revision by the approved build workflow. Never apply a project signing identity to untouched upstream executables or DLLs. The optional CGSecurity binaries are not part of this candidate. The Microsoft .NET runtime bundled by the self-contained build is a system/runtime dependency; licensing and the proposed artifact scope must be reviewed with the provider.

No workflow currently requests a signature. Once accepted by a provider, add only provider-verified project/policy IDs and an appropriately scoped secret; do not commit tokens. Keep signing unavailable to fork pull requests, require release approval and bind a request to the CI artifact from the reviewed commit. Verify signatures and timestamps after signing, then recalculate package hashes. Do not label a release signed until it has passed verification and Windows accepts the relevant binaries.

## Provider attribution

If SignPath Foundation accepts the project and provides signing, update the homepage and release page with its required attribution. The future attribution text is:

> Free code signing provided by SignPath.io, certificate by SignPath Foundation.

This conditional quotation is **not** a claim that this project currently receives that service.

Provider acceptance and signing do not certify the correctness of RAW copying, recovery, backups or partition manipulation. Device testing and system-boot testing remain separate release requirements.

References: https://signpath.org/terms and https://signpath.org/apply

