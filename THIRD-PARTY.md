# Third-party software

## Microsoft .NET

The application targets .NET 8 and Windows Forms. A self-contained build embeds Microsoft runtime components; their licensing is separate from original application code. Preserve `DOTNET-LICENSE.txt` and `DOTNET-THIRD-PARTY-NOTICES.txt` in release packages. Framework/runtime sources: https://github.com/dotnet/runtime and https://github.com/dotnet/winforms . Windows itself and its Storage/PowerShell/wbadmin/chkdsk tools are platform dependencies, not third-party code copied into this repository.

## Optional TestDisk / PhotoRec 7.2

This public source candidate does **not** redistribute CGSecurity executables, DLLs, source archives or documentation. Obtain an official Windows x64 7.2 package from https://www.cgsecurity.org/wiki/TestDisk_Download and extract it so that `tools/testdisk-7.2/testdisk_win.exe` and `tools/testdisk-7.2/qphotorec_win.exe` are alongside the built application's folder. The original directory structure and DLLs must be preserved. Review the vendor package's license and notices.

These are upstream GPL tools from Christophe GRENIER / CGSecurity and contributors. They run in separate processes; source is provided by the upstream project at https://github.com/cgsecurity/testdisk . This project's MIT license does not relicense those tools. Their confirmations and policies apply when launched; the app's partition queue cannot protect against writes made within a separate tool. Windows may independently evaluate or block those upstream files.

No upstream binary will be signed under the proposed Partition Studio identity. Any future redistribution of recovery binaries must first audit their dependency licenses and complete corresponding-source obligations. The initial local 0.3 bundle is not used as the source-publication package.

