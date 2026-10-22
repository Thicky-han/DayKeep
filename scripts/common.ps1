$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$projectFile = Join-Path $projectRoot 'src\DayKeep.Desktop\DayKeep.Desktop.csproj'
$localDotnet = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
if (Test-Path -LiteralPath $localDotnet) { $dotnetExecutable = $localDotnet }
elseif (Get-Command dotnet -ErrorAction SilentlyContinue) { $dotnetExecutable = (Get-Command dotnet).Source }
else { throw 'Install the SDK in global.json or run scripts/bootstrap.ps1 first.' }
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
Set-Location -LiteralPath $projectRoot
function Invoke-Dotnet {
    & $dotnetExecutable @args
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed with exit code $LASTEXITCODE" }
}
