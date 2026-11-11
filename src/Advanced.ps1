param([string]$RequestBase64)
$ErrorActionPreference='Stop'
$ProgressPreference='SilentlyContinue'
[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
function Assert-Identity($expected) {
  $disk=Get-Disk -Number $expected.Number
  if ([string]::IsNullOrWhiteSpace($expected.UniqueId) -or $disk.UniqueId -ne $expected.UniqueId -or
      $disk.SerialNumber -ne $expected.Serial -or $disk.Size -ne $expected.Size) {throw 'A identidade do disco mudou. Atualize a lista.'}
  return $disk
}
function Assert-DataDisk($disk) {
  $osLetter=$env:SystemDrive.TrimEnd(':')
  $osDisk=Get-Partition -DriveLetter $osLetter | Get-Disk
  if ($disk.IsBoot -or $disk.IsSystem -or $disk.Number -in @($osDisk.Number)) {throw 'O disco que contém o Windows em execução está protegido.'}
}
function Get-TargetDisk([string]$path) {
  if ($path -notmatch '^[A-Za-z]:\\') {throw 'Escolha um caminho local com letra de unidade, noutro disco físico.'}
  $letter=$path.Substring(0,1)
  $part=Get-Partition -DriveLetter $letter
  $disk=$part | Get-Disk
  if (@($disk).Count -ne 1 -or $disk.IsOffline -or $disk.IsReadOnly) {throw 'O disco de destino não está disponível para escrita.'}
  $volume=$part | Get-Volume
  if ($volume.FileSystemType -notin @('NTFS','ReFS','exFAT')) {throw 'O destino requer NTFS, ReFS ou exFAT para ficheiros de imagem grandes.'}
  return @{Disk=$disk;Volume=$volume;Letter=$letter}
}
try {
  $r=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($RequestBase64)) | ConvertFrom-Json
  Import-Module Storage
  if ($r.Action -eq 'Topology') {
    $items=@(Get-Disk | Sort-Object Number | ForEach-Object {
      [ordered]@{Number=[int]$_.Number;UniqueId=[string]$_.UniqueId;Serial=[string]$_.SerialNumber;Size=[long]$_.Size;
        Style=[string]$_.PartitionStyle;Offline=[bool]$_.IsOffline;ReadOnly=[bool]$_.IsReadOnly;Boot=[bool]$_.IsBoot;System=[bool]$_.IsSystem}
    })
    ConvertTo-Json -InputObject $items -Depth 4 -Compress;exit 0
  }
  if ($r.Action -eq 'BackupTarget') {
    $target=Get-TargetDisk $r.Path
    if ($target.Disk.Number -eq $r.Disk.Number) {throw 'A imagem tem de ficar noutro disco físico.'}
    if ($target.Volume.SizeRemaining -lt ($r.Size + 1MB)) {throw 'Não há espaço livre para a imagem completa e os metadados.'}
    @{Message='Destino disponível';Number=$target.Disk.Number} | ConvertTo-Json -Compress;exit 0
  }
  if ($r.Action -eq 'WindowsBackup') {
    $target=Get-TargetDisk $r.Path
    if ($target.Volume.FileSystemType -ne 'NTFS') {throw 'O backup Windows requer um volume de destino NTFS.'}
    Assert-DataDisk $target.Disk
    $critical=@(Get-Partition | Where-Object { $_.IsBoot -or $_.IsSystem } | Get-Disk)
    if ($target.Disk.Number -in @($critical.Number)) {throw 'O destino não pode conter volumes críticos do sistema.'}
    $wb=Join-Path $env:SystemRoot 'System32/wbadmin.exe'
    if (-not (Test-Path -LiteralPath $wb)) {throw 'wbadmin não está disponível nesta instalação do Windows.'}
    $os=$env:SystemDrive
    $result=& $wb start backup "-backupTarget:$($target.Letter):" "-include:$os" -allCritical -vssCopy -quiet 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) {throw "Backup Windows não concluído: $result"}
    @{Message=$result} | ConvertTo-Json -Compress;exit 0
  }
  $d=Assert-Identity $r.Disk
  if ($r.Action -eq 'Inspect') {
    $osDisk=Get-Partition -DriveLetter ($env:SystemDrive.TrimEnd(':')) | Get-Disk
    @{Number=[int]$d.Number;Offline=[bool]$d.IsOffline;ReadOnly=[bool]$d.IsReadOnly;Boot=[bool]$d.IsBoot;System=([bool]$d.IsSystem -or $d.Number -in @($osDisk.Number));
      LogicalSectorSize=[int]$d.LogicalSectorSize;PhysicalSectorSize=[int]$d.PhysicalSectorSize;Size=[long]$d.Size;Style=[string]$d.PartitionStyle} | ConvertTo-Json -Compress;exit 0
  }
  if ($r.Action -eq 'Health') {
    $warnings=@();$counter=$null;$smart=@{};$prediction=$null
    try {$counter=$d | Get-StorageReliabilityCounter -ErrorAction Stop} catch {$warnings += $_.Exception.Message}
    try {
      $wd=Get-CimInstance Win32_DiskDrive -Filter "Index=$($d.Number)"
      if ([string]::IsNullOrWhiteSpace($wd.PNPDeviceID)) {throw 'Sem identificador PnP para corresponder os dados SMART.'}
      $data=@(Get-CimInstance -Namespace root/wmi -ClassName MSStorageDriver_FailurePredictData | Where-Object { $_.InstanceName.StartsWith($wd.PNPDeviceID,[StringComparison]::OrdinalIgnoreCase) })
      $status=@(Get-CimInstance -Namespace root/wmi -ClassName MSStorageDriver_FailurePredictStatus | Where-Object { $_.InstanceName.StartsWith($wd.PNPDeviceID,[StringComparison]::OrdinalIgnoreCase) })
      if ($status.Count -eq 1) {$prediction=[bool]$status[0].PredictFailure}
      if ($data.Count -eq 1) {
        $bytes=$data[0].VendorSpecific
        for ($i=2;$i+11 -lt [Math]::Min(362,$bytes.Length);$i+=12) {
          $id=[int]$bytes[$i];if ($id -eq 0) {continue}
          [long]$raw=0;for ($b=0;$b -lt 6;$b++) {$raw=$raw -bor ([long]$bytes[$i+5+$b] -shl (8*$b))}
          $smart[[string]$id]=$raw
        }
      }
    } catch {$warnings += 'SMART ATA não disponível: '+$_.Exception.Message}
    @{Health=[string]$d.HealthStatus;Temperature=$(if($counter.Temperature -gt 0){$counter.Temperature}else{$null});
      PowerOnHours=$counter.PowerOnHours;StartStopCycles=$counter.StartStopCycleCount;LoadUnloadCycles=$counter.LoadUnloadCycleCount;
      Wear=$counter.Wear;ReadErrorsUncorrected=$counter.ReadErrorsUncorrected;WriteErrorsUncorrected=$counter.WriteErrorsUncorrected;
      Smart=$smart;PredictFailure=$prediction;Warnings=$warnings} | ConvertTo-Json -Depth 6 -Compress;exit 0
  }
  if ($r.Action -eq 'Offline' -or $r.Action -eq 'Online') {
    Assert-DataDisk $d
    Set-Disk -Number $d.Number -IsOffline:($r.Action -eq 'Offline') -ErrorAction Stop
    @{Message='Estado do disco atualizado. Volte a atualizar o inventário.'} | ConvertTo-Json -Compress;exit 0
  }
  if ($r.Action -in @('Optimize','BadRepair')) {
    if ($d.IsOffline -or $d.IsReadOnly) {throw 'O disco tem de estar online e disponível para escrita.'}
    $p=Get-Partition -DiskNumber $d.Number -PartitionNumber $r.Partition.Number
    if ($p.Offset -ne $r.Partition.Offset -or $p.Size -ne $r.Partition.Size -or [string]$p.DriveLetter -ne $r.Partition.Letter) {throw 'A partição mudou. Atualize o inventário.'}
    $v=$p | Get-Volume
    if ([int]$p.DriveLetter -eq 0 -or $p.IsHidden -or $v.FileSystemType -ne 'NTFS') {throw 'Selecione um volume NTFS acessível com letra de unidade.'}
    if ($r.Action -eq 'Optimize') {
      Optimize-Volume -DriveLetter $p.DriveLetter -ErrorAction Stop
      $message='Otimização concluída pelo Windows: desfragmentação/TRIM conforme o tipo de volume.'
    } else {
      Assert-DataDisk $d
      # No /x and stdin answers N: never force a dismount or schedule a restart.
      $result='N' | & "$env:SystemRoot/System32/chkdsk.exe" "$($p.DriveLetter):" /r 2>&1 | Out-String
      $code=$LASTEXITCODE
      if ($code -notin @(0,1)) {throw "CHKDSK terminou com código $code. O volume pode estar em uso. $result"}
      $message=$result+"`nCódigo CHKDSK: $code. Marcação de clusters defeituosos não é realocação física pelo firmware."
    }
    @{Message=$message} | ConvertTo-Json -Compress;exit 0
  }
  throw 'Operação avançada desconhecida.'
} catch {[Console]::Error.WriteLine($_.Exception.Message);exit 1}
