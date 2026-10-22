. (Join-Path $PSScriptRoot 'common.ps1')
Invoke-Dotnet build $projectFile --configuration Release --nologo
$reportDirectory = Join-Path $projectRoot ('.cache\shell-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
$libraryPath = Join-Path $projectRoot 'src\DayKeep.Desktop\bin\Release\net10.0-windows\DayKeep.dll'
$processInfo = [System.Diagnostics.ProcessStartInfo]::new($dotnetExecutable)
$processInfo.UseShellExecute = $false
$processInfo.CreateNoWindow = $true
$processInfo.ArgumentList.Add($libraryPath)
$processInfo.ArgumentList.Add('--smoke-test')
$processInfo.ArgumentList.Add($reportDirectory)
$process = [System.Diagnostics.Process]::Start($processInfo)
if (-not $process.WaitForExit(60000)) { $process.Kill(); throw 'WPF smoke check timed out.' }
$reportPath = Join-Path $reportDirectory 'report.json'
if (-not (Test-Path -LiteralPath $reportPath)) { throw "Smoke report was not produced; process exit code $($process.ExitCode)." }
$report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
$report.checks | ForEach-Object { if ($_.PSObject.Properties.Name -contains 'passed') { Write-Output (('{0}: {1}' -f $_.passed, $_.name)) } }
Write-Output "Report: $reportPath"
if ($process.ExitCode -ne 0 -or $report.failures -ne 0) { throw 'Shell checks failed. Inspect report.json.' }
