. (Join-Path $PSScriptRoot 'common.ps1')
$outputDirectory = Join-Path $projectRoot 'dist\l1-win-x64'
Invoke-Dotnet publish $projectFile --configuration Release --runtime win-x64 --self-contained true --output $outputDirectory '-p:DebugType=None' '-p:DebugSymbols=false' --nologo
Copy-Item -LiteralPath (Join-Path $projectRoot 'LICENSE') -Destination $outputDirectory
Copy-Item -LiteralPath (Join-Path $projectRoot 'INSTALL.md') -Destination $outputDirectory
$sqliteNotices = Join-Path $outputDirectory 'licenses\sqlite'
New-Item -ItemType Directory -Path $sqliteNotices -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $projectRoot 'third-party') -File | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $sqliteNotices -Force }
$assets = Get-Content -LiteralPath (Join-Path $projectRoot 'src\DayKeep.Desktop\obj\project.assets.json') -Raw | ConvertFrom-Json
$runtimeConfig = Get-Content -LiteralPath (Join-Path $outputDirectory 'DayKeep.runtimeconfig.json') -Raw | ConvertFrom-Json
foreach ($framework in $runtimeConfig.runtimeOptions.includedFrameworks) {
    $packageName = $framework.name.ToLowerInvariant() + '.runtime.win-x64'
    $packageDirectory = $null
    foreach ($folder in $assets.packageFolders.PSObject.Properties.Name) {
        $candidate = Join-Path $folder "$packageName\$($framework.version)"
        if (Test-Path -LiteralPath $candidate) { $packageDirectory = $candidate; break }
    }
    if (-not $packageDirectory) { throw "Framework license source missing: $($framework.name)" }
    $licenseDirectory = Join-Path $outputDirectory "licenses\$($framework.name)"
    New-Item -ItemType Directory -Path $licenseDirectory -Force | Out-Null
    $notices = @(Get-ChildItem -LiteralPath $packageDirectory -File | Where-Object Name -Match '^(LICENSE(\.TXT)?|THIRD-PARTY-NOTICES\.TXT)$')
    if ($notices.Count -eq 0) { throw "No framework license found: $($framework.name)" }
    foreach ($notice in $notices) { Copy-Item -LiteralPath $notice.FullName -Destination $licenseDirectory }
}
Write-Output "Preview: $(Join-Path $outputDirectory 'DayKeep.exe')"
