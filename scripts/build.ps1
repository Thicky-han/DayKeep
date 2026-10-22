. (Join-Path $PSScriptRoot 'common.ps1')
Invoke-Dotnet build $projectFile --configuration Release --nologo '-p:ContinuousIntegrationBuild=true'
