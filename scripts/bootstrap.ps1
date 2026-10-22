$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$lock = Get-Content -LiteralPath (Join-Path $projectRoot 'tools\sdk.lock.json') -Raw | ConvertFrom-Json
$destination = Join-Path $projectRoot '.tools\dotnet'
if (Test-Path -LiteralPath (Join-Path $destination 'dotnet.exe')) {
    $installed = & (Join-Path $destination 'dotnet.exe') --version
    if ($installed -eq $lock.version) { Write-Output "SDK $installed is ready."; exit 0 }
    throw 'A different local SDK exists. Review it before replacing the tools directory.'
}
$cacheDirectory = Join-Path $projectRoot '.cache'
New-Item -ItemType Directory -Force -Path $cacheDirectory | Out-Null
$archivePath = Join-Path $cacheDirectory "dotnet-sdk-$($lock.version)-win-x64.zip"
if (-not (Test-Path -LiteralPath $archivePath)) { Invoke-WebRequest -Uri $lock.url -OutFile $archivePath }
if ((Get-FileHash -LiteralPath $archivePath -Algorithm SHA512).Hash.ToLowerInvariant() -ne $lock.sha512) { throw 'SDK checksum mismatch. Cached archive retained for inspection.' }
Expand-Archive -LiteralPath $archivePath -DestinationPath $destination
& (Join-Path $destination 'dotnet.exe') --version
if ($LASTEXITCODE -ne 0) { throw 'SDK installation verification failed.' }
