$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$app=Join-Path $root 'artifacts/app'
$exe=Join-Path $app 'PartitionStudio.exe'
$reports=Join-Path $root 'artifacts/tests'
if(-not (Test-Path -LiteralPath $exe)){throw 'Build the application first.'}
$resultFile=Join-Path $reports 'ci-results.txt'
if(-not (Test-Path -LiteralPath $resultFile)){throw 'File-only tests have not run.'}
$results=Get-Content -LiteralPath $resultFile -Raw
if($results -match 'FAIL:' -or $results -notmatch 'PASS: file-only model and disk engine tests'){throw 'Required tests did not pass. No candidate will be packaged.'}
$release=Join-Path $root ('artifacts/release-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $release|Out-Null
Copy-Item -LiteralPath $exe -Destination $release
foreach($name in @('README.md','LICENSE.txt','PRIVACY.md','THIRD-PARTY.md','SIGNING.md','BRANDING.md','DOTNET-LICENSE.txt','DOTNET-THIRD-PARTY-NOTICES.txt')){
  Copy-Item -LiteralPath (Join-Path $root $name) -Destination $release
}
Copy-Item -LiteralPath (Join-Path $root 'docs') -Destination (Join-Path $release 'docs') -Recurse
Copy-Item -LiteralPath $reports -Destination (Join-Path $release 'verification') -Recurse
$manifest=[ordered]@{}
foreach($file in Get-ChildItem -LiteralPath $release -File -Recurse){
  $relative=$file.FullName.Substring($release.Length+1)
  $manifest[$relative]=(Get-FileHash -Algorithm SHA256 -LiteralPath $file.FullName).Hash.ToLowerInvariant()
}
$manifest | ConvertTo-Json -Depth 4 | Set-Content -Encoding utf8 -LiteralPath (Join-Path $release 'SHA256.json')
$archive=Join-Path $root 'artifacts/PartitionStudio-unsigned-windows-x64.zip'
Compress-Archive -Path (Join-Path $release '*') -DestinationPath $archive -Force
Write-Output "Unsigned candidate: $archive"

