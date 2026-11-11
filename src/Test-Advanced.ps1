$ErrorActionPreference='Stop'
$source=Join-Path $PSScriptRoot 'Advanced.ps1'
$tokens=$null;$errors=$null
[System.Management.Automation.Language.Parser]::ParseFile((Resolve-Path $source),[ref]$tokens,[ref]$errors)|Out-Null
if($errors.Count){throw ($errors|Out-String)}
Write-Output 'PASS: Advanced.ps1 syntax'
$disk=@{Number=7;UniqueId='expected';Serial='serial';Size=2147483648}
$part=@{Number=1;Offset=1048576;Size=1073741824;Letter='Z'}
foreach($case in @('Identity','OfflineSystem','BackupSame','BackupSpace','WindowsSame','OptimizeChanged')){
  $action=switch($case){'Identity'{'Inspect'};'OfflineSystem'{'Offline'};'BackupSame'{'BackupTarget'};'BackupSpace'{'BackupTarget'};'WindowsSame'{'WindowsBackup'};'OptimizeChanged'{'Optimize'}}
  $request=@{Action=$action;Disk=$disk;Partition=$part;Path='Z:\image.psimg';Size=2147483648}
  $payload=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes(($request|ConvertTo-Json -Depth 8 -Compress)))
  $mock=@'
function Import-Module {}
function Get-Disk {
 param([int]$Number=-1,[Parameter(ValueFromPipeline=$true)]$InputObject)
 process {
   if($Number -eq -1){$n=$InputObject.DiskNumber}else{$n=$Number}
   [pscustomobject]@{Number=$n;UniqueId=$(if($testCase -eq 'Identity'){'changed'}else{'expected'});SerialNumber='serial';Size=2147483648;
     IsBoot=($testCase -eq 'OfflineSystem');IsSystem=$false;IsOffline=$false;IsReadOnly=$false;PartitionStyle='GPT'}
 }
}
function Get-Partition {
 param([string]$DriveLetter,[int]$DiskNumber,[int]$PartitionNumber)
 [pscustomobject]@{DiskNumber=$(if($DriveLetter -eq 'C' -or $testCase -eq 'WindowsSame'){0}elseif($testCase -eq 'BackupSpace'){8}else{7});PartitionNumber=1;
   Offset=1048576;Size=$(if($testCase -eq 'OptimizeChanged'){123}else{1073741824});DriveLetter='Z';IsHidden=$false}
}
function Get-Volume {[pscustomobject]@{FileSystemType='NTFS';SizeRemaining=$(if($testCase -eq 'BackupSpace'){1024}else{1099511627776})}}
function Set-Disk {throw 'UNEXPECTED_MUTATION'}
function Optimize-Volume {throw 'UNEXPECTED_MUTATION'}
'@
  $code="`$testCase='$case';`$env:SystemDrive='C:';"+$mock+"`n& '$source' -RequestBase64 '$payload'"
  $encoded=[Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($code))
  $psi=[Diagnostics.ProcessStartInfo]::new('powershell.exe',"-NoProfile -NonInteractive -EncodedCommand $encoded")
  $psi.StandardOutputEncoding=[Text.UTF8Encoding]::new($false);$psi.StandardErrorEncoding=[Text.UTF8Encoding]::new($false);$psi.UseShellExecute=$false;$psi.CreateNoWindow=$true;$psi.RedirectStandardOutput=$true;$psi.RedirectStandardError=$true
  $proc=[Diagnostics.Process]::Start($psi);$output=$proc.StandardOutput.ReadToEnd();$err=$proc.StandardError.ReadToEnd();$proc.WaitForExit()
  $reason=@{Identity='identidade do disco mudou';OfflineSystem='Windows em execução está protegido';BackupSame='noutro disco físico';BackupSpace='espaço livre';WindowsSame='Windows em execução está protegido';OptimizeChanged='partição mudou'}[$case]
  if($proc.ExitCode -ne 1 -or $err -match 'UNEXPECTED_MUTATION' -or [string]::IsNullOrWhiteSpace($err) -or $err -notmatch [regex]::Escape($reason)){throw "Guard failed: $case $output $err"}
  Write-Output "PASS: $case rejected before mutation"
}
