param([string]$RequestBase64)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
try {
  $r = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($RequestBase64)) | ConvertFrom-Json
  Import-Module Storage -ErrorAction Stop
  if ($r.Action -eq 'Inventory') {
    $result = @(foreach ($d in Get-Disk) {
      $diskParts = @()
      if (-not $d.IsOffline -and $d.PartitionStyle -ne 'RAW') { $diskParts = @(Get-Partition -DiskNumber $d.Number -ErrorAction Stop) }
      $parts = @(foreach ($p in $diskParts) {
        $v = $p | Get-Volume -ErrorAction SilentlyContinue
        [ordered]@{ Number=[int]$p.PartitionNumber; Offset=[long]$p.Offset; Size=[long]$p.Size;
          Letter=$(if ([int]$p.DriveLetter -gt 0) { [string]$p.DriveLetter } else { '' }); Label=[string]$v.FileSystemLabel; FileSystem=[string]$v.FileSystemType;
          Free=[long]$v.SizeRemaining; Boot=[bool]$p.IsBoot; System=[bool]$p.IsSystem; Hidden=[bool]$p.IsHidden;
          Type=[string]$p.Type; GptType=[string]$p.GptType; MbrType=[int]$p.MbrType }
      })
      [ordered]@{ Number=[int]$d.Number; Name=[string]$d.FriendlyName; UniqueId=[string]$d.UniqueId;
        Serial=[string]$d.SerialNumber; Size=[long]$d.Size; Style=[string]$d.PartitionStyle;
        Boot=[bool]$d.IsBoot; System=[bool]$d.IsSystem; Offline=[bool]$d.IsOffline; ReadOnly=[bool]$d.IsReadOnly;
        Health=[string]$d.HealthStatus; LargestFree=[long]$d.LargestFreeExtent; Partitions=$parts }
    })
    ConvertTo-Json -InputObject $result -Depth 8 -Compress
    exit 0
  }
  $d = Get-Disk -Number $r.Disk.Number
  if ([string]::IsNullOrWhiteSpace($r.Disk.UniqueId) -or $d.UniqueId -ne $r.Disk.UniqueId -or
      $d.SerialNumber -ne $r.Disk.Serial -or $d.Size -ne $r.Disk.Size) { throw 'A identidade do disco mudou. Atualize e volte a selecionar.' }
  if ($d.IsOffline -or $d.IsReadOnly -or $d.PartitionStyle -notin @('GPT','MBR')) { throw 'Disco indisponível, protegido ou não inicializado.' }
  $current = @(Get-Partition -DiskNumber $d.Number | Sort-Object PartitionNumber)
  $old = @($r.Disk.Partitions | Sort-Object Number)
  if ($current.Count -ne $old.Count) { throw 'A tabela de partições mudou. Atualize a lista.' }
  for ($i=0; $i -lt $current.Count; $i++) {
    if ($current[$i].PartitionNumber -ne $old[$i].Number -or $current[$i].Offset -ne $old[$i].Offset -or $current[$i].Size -ne $old[$i].Size) {
      throw 'A disposição das partições mudou. Atualize a lista.'
    }
  }
  if ($r.Action -ne 'Scan' -and ($d.IsBoot -or $d.IsSystem)) { throw 'Alterações ao disco de arranque/sistema estão bloqueadas nesta versão.' }
  if ($r.Action -eq 'Create') {
    if ($r.Size -lt 16MB -or $r.Size -gt $d.LargestFreeExtent) { throw 'Tamanho inválido para o espaço livre contíguo atual.' }
    $new = New-Partition -DiskNumber $d.Number -Size ([uint64]$r.Size) -AssignDriveLetter
    @{ Message="Partição $($new.PartitionNumber) criada sem formatação. Atualize e formate-a quando pretender." } | ConvertTo-Json -Compress
    exit 0
  }
  $p = Get-Partition -DiskNumber $d.Number -PartitionNumber $r.Partition.Number
  $basic = ($d.PartitionStyle -eq 'GPT' -and $p.GptType -eq '{ebd0a0a2-b9e5-4433-87c0-68b6b72699c7}') -or
           ($d.PartitionStyle -eq 'MBR' -and [int]$p.MbrType -in @(7,11,12,6,14))
  if ($p.IsBoot -or $p.IsSystem -or $p.IsHidden -or -not $basic) { throw 'Apenas partições de dados básicas e visíveis são suportadas.' }
  $v = $p | Get-Volume -ErrorAction SilentlyContinue
  if ($r.Action -in @('Limits','Resize','Scan','Repair') -and $v.FileSystemType -ne 'NTFS') { throw 'Esta operação requer um volume NTFS.' }
  if ($r.Action -eq 'Limits') {
    Get-PartitionSupportedSize -DiskNumber $d.Number -PartitionNumber $p.PartitionNumber | Select-Object SizeMin,SizeMax | ConvertTo-Json -Compress
    exit 0
  }
  # Fail closed if the encryption provider cannot establish the volume state.
  if ($p.DriveLetter) {
    $encrypted = @(Get-CimInstance -Namespace 'root/CIMV2/Security/MicrosoftVolumeEncryption' -ClassName Win32_EncryptableVolume -ErrorAction Stop |
      Where-Object DriveLetter -eq "$($p.DriveLetter):")
    foreach ($e in $encrypted) {
      $status = Invoke-CimMethod -InputObject $e -MethodName GetConversionStatus
      if ($status.ReturnValue -ne 0 -or $status.ConversionStatus -ne 0) { throw 'Volume encriptado ou estado de encriptação incerto. Operação bloqueada.' }
    }
  } elseif ($r.Action -ne 'Format') { throw 'A operação requer uma letra de unidade e um volume acessível.' }
  switch ($r.Action) {
    'Resize' {
      $s = Get-PartitionSupportedSize -DiskNumber $d.Number -PartitionNumber $p.PartitionNumber
      if ($r.Size -lt $s.SizeMin -or $r.Size -gt $s.SizeMax) { throw 'Tamanho fora dos limites permitidos pelo Windows.' }
      Resize-Partition -DiskNumber $d.Number -PartitionNumber $p.PartitionNumber -Size ([uint64]$r.Size)
      $message = 'Partição redimensionada.'
    }
    'Format' {
      if ($r.FileSystem -notin @('NTFS','exFAT')) { throw 'Sistema de ficheiros inválido.' }
      if ($r.Label.Length -gt 32 -or $r.Label -match '[\\/:*?"<>|]') { throw 'Nome de volume inválido.' }
      $p | Format-Volume -FileSystem $r.FileSystem -NewFileSystemLabel $r.Label -Confirm:$false | Out-Null
      $message = 'Formatação concluída.'
    }
    'Delete' { $p | Remove-Partition -Confirm:$false; $message = 'Partição eliminada.' }
    'Scan' { $state = Repair-Volume -DriveLetter $p.DriveLetter -Scan; $message = "Verificação: $state" }
    'Repair' { $state = Repair-Volume -DriveLetter $p.DriveLetter -OfflineScanAndFix; $message = "Reparação: $state" }
    default { throw 'Operação não reconhecida.' }
  }
  @{ Message=$message } | ConvertTo-Json -Compress
} catch {
  [Console]::Error.WriteLine($_.Exception.Message)
  exit 1
}
