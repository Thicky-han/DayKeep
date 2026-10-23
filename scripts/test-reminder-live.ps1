. (Join-Path $PSScriptRoot 'common.ps1')
Invoke-Dotnet build $projectFile --configuration Release --nologo
$reportDirectory = Join-Path $projectRoot ('.cache\live-0.7-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
$libraryPath = Join-Path $projectRoot 'src\DayKeep.Desktop\bin\Release\net10.0-windows\DayKeep.dll'
$info = [System.Diagnostics.ProcessStartInfo]::new($dotnetExecutable)
$info.UseShellExecute = $false
$info.CreateNoWindow = $true
$info.ArgumentList.Add($libraryPath)
$info.ArgumentList.Add('--live-reminder-test')
$info.ArgumentList.Add($reportDirectory)
$process = [System.Diagnostics.Process]::Start($info)
Write-Output "Waiting for actual next HH:mm; target and result: $reportDirectory"
if (-not $process.WaitForExit(90000)) { $process.Kill(); throw 'Live reminder check timed out.' }
$report = Get-Content -LiteralPath (Join-Path $reportDirectory 'live-report.json') -Raw | ConvertFrom-Json
$report | ConvertTo-Json
if ($process.ExitCode -ne 0 -or -not $report.passed) { throw 'Live reminder check failed.' }
