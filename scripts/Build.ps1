$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$project=Join-Path $root 'src/PartitionStudio.csproj'
$output=Join-Path $root 'artifacts/app'
& dotnet publish $project -c Release -r win-x64 --self-contained true '-p:PublishSingleFile=true' '-p:IncludeNativeLibrariesForSelfExtract=true' '-p:ContinuousIntegrationBuild=true' "-p:PathMap=$root=/_/" -o $output
if($LASTEXITCODE -ne 0){throw 'Compilation failed.'}
Write-Output 'Compiled unsigned candidate. No application or disk operation was executed.'

