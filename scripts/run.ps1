. (Join-Path $PSScriptRoot 'common.ps1')
Invoke-Dotnet run --project $projectFile --configuration Release
