# Reporting security and data-safety problems

This project is an experimental disk utility, not a security testing or bypass tool. It must not disable Windows App Control, install a trusted root, or conceal an operation's destination to make a blocked build run.

A report should include application/source version, Windows version, a synthetic reproduction when possible, the operation selected, whether the disk was offline and the observed result. Do not include full disk images, unredacted serial numbers, private keys, account tokens, recovered personal files or unrelated diagnostics in a public issue.

The public issue tracker is available in the repository. Private vulnerability reporting has not been configured or verified; do not post secrets or personal disk data in public issues. The maintainer should establish a private disclosure route before distributing a signed release. For suspected destructive behavior, stop using the preview and preserve a safe reproduction; do not repeatedly run an operation on the original data.

