param([switch]$IncludeGui)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$exe=Join-Path $root 'artifacts/app/PartitionStudio.exe'
$reports=Join-Path $root 'artifacts/tests'
New-Item -ItemType Directory -Force -Path $reports|Out-Null
$results=[Collections.Generic.List[string]]::new()
function Invoke-SelfTest([string[]]$Arguments){
  $start=[Diagnostics.ProcessStartInfo]::new($exe)
  $start.UseShellExecute=$false
  # These arguments are fixed test switches and a workspace fixture directory.
  foreach($argument in $Arguments){$start.ArgumentList.Add($argument)}
  $process=[Diagnostics.Process]::Start($start)
  if(-not $process.WaitForExit(90000)){$process.Kill($true);throw 'File-only self-test timed out.'}
  if($process.ExitCode -ne 0){throw "Self-test failed: exit $($process.ExitCode)."}
}
# Intended for PowerShell 7 on a normal Windows CI runner. Never replace the
# loader, disable App Control, or elevate to work around a blocked executable.
# The switches below use only synthetic in-memory data and workspace files.
try{
  foreach($name in @('Storage.ps1','Advanced.ps1','Test-Guards.ps1','Test-Advanced.ps1')){
    $tokens=$null;$parseErrors=$null
    [Management.Automation.Language.Parser]::ParseFile((Join-Path $root "src/$name"),[ref]$tokens,[ref]$parseErrors)|Out-Null
    if($parseErrors.Count){throw "PowerShell parse error: $name"}
    $results.Add("PASS: syntax $name")
  }
  foreach($name in @('Test-Guards.ps1','Test-Advanced.ps1')){
    $lines=& powershell.exe -NoProfile -NonInteractive -File (Join-Path $root "src/$name") 2>&1
    $lines | ForEach-Object {$results.Add([string]$_)}
    if($LASTEXITCODE -ne 0){throw "Mocked storage guard tests failed: $name"}
  }
  Invoke-SelfTest -Arguments @('--file-tests',$reports)
  $results.Add('PASS: file-only model and disk engine tests')
  if($IncludeGui){
    Invoke-SelfTest -Arguments @('--self-test')
    $results.Add('PASS: existing model/UI self-test')
    Invoke-SelfTest -Arguments @('--extended-self-test',$reports)
    $results.Add('PASS: desktop rendering tests')
  }else{
    $results.Add('NOT RUN: desktop rendering; use -IncludeGui in an authorized interactive Windows session.')
  }
}catch{
  $results.Add('FAIL: '+$_.Exception.Message)
  throw
}finally{
  foreach($name in @('verificacao-visual.png','verificacao-compacta.png','verificacao-arranque.png','self-test.txt','self-test-progress.txt')){
    $path=Join-Path $root "artifacts/app/$name"
    if(Test-Path -LiteralPath $path){Copy-Item -LiteralPath $path -Destination (Join-Path $reports $name)}
  }
  $results | Set-Content -Encoding utf8 -LiteralPath (Join-Path $reports 'ci-results.txt')
}

