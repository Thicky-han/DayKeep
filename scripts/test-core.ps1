. (Join-Path $PSScriptRoot 'common.ps1')
$checkProject = Join-Path $projectRoot 'tests\DayKeep.Core.Checks\DayKeep.Core.Checks.csproj'
Invoke-Dotnet build $checkProject --configuration Release --nologo
$reportDirectory = Join-Path $projectRoot ('.cache\core-l4-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
$checkLibrary = Join-Path $projectRoot 'tests\DayKeep.Core.Checks\bin\Release\net10.0\DayKeep.Core.Checks.dll'
Invoke-Dotnet $checkLibrary $reportDirectory
Invoke-Dotnet $checkLibrary $reportDirectory --write-restart
Invoke-Dotnet $checkLibrary $reportDirectory --read-restart
Write-Output "Reports: $reportDirectory"
