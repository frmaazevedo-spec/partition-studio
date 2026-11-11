# Publication review â€” owner action required

The owner approved public source publication. The reviewed 31-file snapshot was published in commit d42862c25402af5ff869d5d4d3856dd75bb9d2c2. Subsequent development is tracked in the repository; no SignPath application has been submitted.

Verified public destination: https://github.com/frmaazevedo-spec/partition-studio under the connected account `frmaazevedo-spec`. Original source is MIT; verify that you wish to publish it under that license. The repository includes substantial AI-assisted initial code that still requires human review, especially physical-device operations.

## Included

- Original C#/PowerShell source, test code and metadata for `0.4.0-preview.1`.
- Readme, operations/privacy/security/signing policies and contributor guidance.
- Draft CI using pinned action commits, read-only permissions and file-only/mocked tests.
- An honest application draft describing the new project's lack of established reputation and remaining validation.
- Original-source MIT license and .NET runtime license/notices.

## Excluded

- Executables, vendor DLLs, private diagnostics, real SMART/inventory results and disk identifiers.
- Signing keys/certificates, tokens, recovery output and disk images.
- Any assertion of SignPath acceptance, certification, passing GitHub CI or completed physical-disk validation.

## Review before publication

1. Review destructive-operation behavior and limitations in `docs/OPERATIONS.md`.
2. Confirm proposed account/roles and activate MFA without sharing credentials.
3. Confirm MIT source publication and public visibility.
4. After publication, review the first CI run. A workflow file is not evidence of a successful build.
5. Decide whether/when to publish an unsigned preview. Do not submit the SignPath draft until its facts, public links and reputation evidence are complete.

The package is intended for a transparent source-publication decision. It is **not** an executable fix for Smart App Control and cannot be used to obtain a signature without provider review.

