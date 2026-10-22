param([switch]$KeepOpen)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$applicationPath = Join-Path $projectRoot 'dist\l1-win-x64\DayKeep.exe'
if (-not (Test-Path -LiteralPath $applicationPath)) { throw 'Run publish-preview.ps1 first.' }
if (Get-Process -Name 'DayKeep' -ErrorAction SilentlyContinue) { throw 'An existing DayKeep process is running; test stopped to preserve it.' }
$first = $null
$second = $null
$passed = $false
try {
    $first = Start-Process -FilePath $applicationPath -PassThru -WindowStyle Hidden
    if (-not $first.WaitForInputIdle(10000)) { throw 'First instance did not initialize.' }
    $second = Start-Process -FilePath $applicationPath -PassThru -WindowStyle Hidden
    if (-not $second.WaitForExit(10000)) { throw 'Second launch did not exit.' }
    $first.Refresh()
    if ($second.ExitCode -ne 0 -or $first.HasExited) { throw 'Single-instance handoff failed.' }
    $passed = $true
    $report = [ordered]@{ stage = 'L1'; passed = $true; firstProcess = $first.Id; secondProcess = $second.Id; secondExitCode = $second.ExitCode; originalStillRunning = -not $first.HasExited; scope = 'Two actual self-contained executable launches; no physical keyboard or human tray acceptance.' }
    $reportDirectory = Join-Path $projectRoot '.cache'
    New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
    $report | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $reportDirectory 'single-instance-report.json') -Encoding utf8
    Write-Output 'Self-contained executable and single-instance handoff passed.'
}
finally {
    if ($second -and -not $second.HasExited) { $second.Kill(); $second.WaitForExit() }
    if ($first -and -not $first.HasExited -and (-not $KeepOpen -or -not $passed)) { $first.Kill(); $first.WaitForExit() }
}
