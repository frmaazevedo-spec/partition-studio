$ErrorActionPreference='Stop'
$source=Join-Path $PSScriptRoot 'Storage.ps1'
$tokens=$null; $parseErrors=$null
[System.Management.Automation.Language.Parser]::ParseFile((Resolve-Path $source),[ref]$tokens,[ref]$parseErrors) | Out-Null
if($parseErrors.Count){throw ($parseErrors | Out-String)}
Write-Output 'PASS: PowerShell syntax'
# Exercise the actual backend guard paths in isolated child processes with mocked storage commands.
$part=@{Number=1;Offset=1048576;Size=1073741824}
$disk=@{Number=3;UniqueId='expected';Serial='serial';Size=2147483648;Partitions=@($part)}
foreach($case in @('Identity','System','Layout','Special','Limits')) {
  $request=@{Action='Resize';Disk=$disk;Partition=$part;Size=536870912}
  $payload=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes(($request|ConvertTo-Json -Depth 8 -Compress)))
  $mock=@'
function Import-Module {}
function Get-Disk {
  [pscustomobject]@{ Number=3; UniqueId= 'expected'; SerialNumber='serial'; Size=2147483648; IsBoot=($testCase -eq 'System');
    IsSystem=$false; IsOffline=$false; IsReadOnly=$false; PartitionStyle='GPT' }
}
function Get-Partition {
  [pscustomobject]@{PartitionNumber=1;Offset=1048576;Size=$(if($testCase -eq 'Layout'){99}else{1073741824});
    IsBoot=$false;IsSystem=$false;IsHidden=($testCase -eq 'Special');GptType='{ebd0a0a2-b9e5-4433-87c0-68b6b72699c7}';DriveLetter='Z'}
}
function Get-Volume { [pscustomobject]@{FileSystemType='NTFS'} }
function Get-CimInstance { @() }
function Get-PartitionSupportedSize { [pscustomobject]@{SizeMin=1073741824;SizeMax=2147483648} }
function Resize-Partition { throw 'UNEXPECTED_MUTATION' }
'@
  if($case -eq 'Identity'){$mock=$mock.Replace("UniqueId= 'expected'","UniqueId= 'changed'")}
  $code="`$testCase='$case';"+$mock+"`n& '$source' -RequestBase64 '$payload'"
  $encoded=[Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($code))
  $psi=[Diagnostics.ProcessStartInfo]::new('powershell.exe',"-NoProfile -NonInteractive -EncodedCommand $encoded")
  $psi.StandardOutputEncoding=[Text.UTF8Encoding]::new($false);$psi.StandardErrorEncoding=[Text.UTF8Encoding]::new($false);$psi.UseShellExecute=$false;$psi.CreateNoWindow=$true;$psi.RedirectStandardOutput=$true;$psi.RedirectStandardError=$true
  $process=[Diagnostics.Process]::Start($psi);$output=$process.StandardOutput.ReadToEnd();$err=$process.StandardError.ReadToEnd();$process.WaitForExit()
  $reason=@{Identity='identidade do disco mudou';System='disco de arranque/sistema';Layout='disposição das partições mudou';Special='partições de dados básicas';Limits='fora dos limites permitidos'}[$case]
  if($process.ExitCode -ne 1 -or $err -match 'UNEXPECTED_MUTATION' -or [string]::IsNullOrWhiteSpace($err) -or $err -notmatch [regex]::Escape($reason)){throw "Guard failed: $case $output $err"}
  Write-Output "PASS: backend rejects $case before mutation"
}
