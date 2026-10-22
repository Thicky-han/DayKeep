param([switch]$KeepOpen)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$applicationPath = Join-Path $projectRoot 'dist\l4-win-x64\DayKeep.exe'
if (-not (Test-Path -LiteralPath $applicationPath)) { throw 'Run publish-preview.ps1 first.' }
$reportDirectory = Join-Path $projectRoot ('.cache\instance-l4-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
$first = $null
$second = $null
$third = $null
$state = $null
function Start-IsolatedInstance([bool]$Background) {
    $info = [System.Diagnostics.ProcessStartInfo]::new($applicationPath)
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.ArgumentList.Add('--instance-test')
    $info.ArgumentList.Add($reportDirectory)
    if ($Background) { $info.ArgumentList.Add('--background') }
    return [System.Diagnostics.Process]::Start($info)
}
function Read-Observation([bool]$Visible) {
    for ($attempt=0; $attempt -lt 80; $attempt++) {
        $path = Join-Path $reportDirectory 'instance.json'
        if (Test-Path -LiteralPath $path) {
            try { $item = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json; if ($item.visible -eq $Visible) { return $item } } catch { }
        }
        Start-Sleep -Milliseconds 100
    }
    throw 'Isolated instance did not reach expected visibility.'
}
try {
    $first = Start-IsolatedInstance $true
    $state = Read-Observation $false
    $second = Start-IsolatedInstance $true
    if (-not $second.WaitForExit(10000) -or $second.ExitCode -ne 0) { throw 'Second background launch did not exit cleanly.' }
    $state = Read-Observation $false
    if ($state.showSignals -ne 0) { throw 'Background launch activated the first window.' }
    $third = Start-IsolatedInstance $false
    if (-not $third.WaitForExit(10000) -or $third.ExitCode -ne 0) { throw 'Manual second launch did not exit cleanly.' }
    $state = Read-Observation $true
    if ($state.process -ne $first.Id -or $state.showSignals -ne 1 -or $first.HasExited) { throw 'Manual launch did not restore the first window.' }
    $report = [ordered]@{stage='L4';passed=$true;backgroundDidNotActivate=$true;manualRestoredOriginal=$true;observation=$state;scope='Three real executable launches with isolated database and named objects; existing user instance preserved. No real Windows logoff or sleep.'}
    if (-not $KeepOpen) {
        $signal = [System.Threading.EventWaitHandle]::OpenExisting($state.exitEvent)
        try { $null=$signal.Set() } finally { $signal.Dispose() }
        if (-not $first.WaitForExit(10000) -or $first.ExitCode -ne 0) { throw 'Tray-equivalent graceful application exit failed.' }
        $report.gracefulExit = $true
    }
    $report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $reportDirectory 'report.json') -Encoding utf8
    $report | ConvertTo-Json -Depth 5
    Write-Output "Report: $reportDirectory"
}
finally {
    foreach ($child in @($third,$second)) { if ($child -and -not $child.HasExited) { $child.Kill(); $child.WaitForExit() } }
    if ($first -and -not $first.HasExited -and -not $KeepOpen) { $first.Kill(); $first.WaitForExit() }
}
