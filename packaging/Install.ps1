param([string]$TargetDirectory, [switch]$NoShortcuts)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'PackageTools.ps1')
$manifest = Read-DayKeepPackage $PSScriptRoot
Test-DayKeepFiles $PSScriptRoot $manifest
if (-not $TargetDirectory) { $TargetDirectory = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) ('Programs\DayKeep\'+$manifest.version) }
$target = [IO.Path]::GetFullPath($TargetDirectory).TrimEnd('\','/')
$source = [IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\','/')
$privateData = [IO.Path]::GetFullPath((Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'DayKeep')).TrimEnd('\','/')
if ($target -eq $source -or $target.StartsWith($source+'\',[StringComparison]::OrdinalIgnoreCase) -or $target -eq $privateData -or $target.StartsWith($privateData+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Choose a separate program directory; private task data is never an installation target.' }
Assert-DayKeepNotRunning $target
if (Test-Path -LiteralPath $target) {
    $marker = Join-Path $target 'INSTALLED.json'
    if (-not (Test-Path -LiteralPath $marker)) { throw 'Target already exists without a DayKeep installation marker. It was preserved; choose a new folder.' }
    $installed = Get-Content -LiteralPath $marker -Raw | ConvertFrom-Json
    if ($installed.product -ne $dayKeepProductId -or $installed.sourceCommit -ne $manifest.sourceCommit -or $installed.version -ne $manifest.version) { throw 'A different installation exists. Use a new version folder; no files were overwritten.' }
    Test-DayKeepFiles $target $manifest
} else {
    $parent = Split-Path -Parent $target
    if (-not $parent) { throw 'Invalid installation directory.' }
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
    $stage = Join-Path $parent ('.daykeep-stage-'+[Guid]::NewGuid().ToString('N'))
    $stageFull = [IO.Path]::GetFullPath($stage)
    if (-not $stageFull.StartsWith([IO.Path]::GetFullPath($parent).TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid staging directory.' }
    New-Item -ItemType Directory -Path $stage | Out-Null
    try {
        foreach ($entry in $manifest.files) {
            $destination = Get-DayKeepChild $stage $entry.path
            New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
            Copy-Item -LiteralPath (Get-DayKeepChild $source $entry.path) -Destination $destination
        }
        Copy-Item -LiteralPath (Join-Path $source 'PACKAGE.json') -Destination (Join-Path $stage 'PACKAGE.json')
        [ordered]@{product=$dayKeepProductId;version=$manifest.version;sourceCommit=$manifest.sourceCommit} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $stage 'INSTALLED.json') -Encoding UTF8
        Test-DayKeepFiles $stage $manifest
        # Both absolute paths were checked above; never replace an existing target.
        [IO.Directory]::Move($stage,$target)
    } catch {
        # Leave partial staging for inspection instead of deleting unknown data.
        throw "Installation did not finish. Original directories were preserved. Staging: $stage. $($_.Exception.Message)"
    }
}
if (-not $NoShortcuts) {
    $exe = Join-Path $target 'DayKeep.exe'
    Set-DayKeepShortcut (Join-Path ([Environment]::GetFolderPath('Desktop')) 'DayKeep.lnk') $exe
    Set-DayKeepShortcut (Join-Path ([Environment]::GetFolderPath('Programs')) 'DayKeep.lnk') $exe
}
Write-Output "Installed: $target"
Write-Output 'Open DayKeep.exe or the DayKeep shortcut. Startup at login remains opt-in. Task data was preserved.'
Write-Output 'To remove this version, exit DayKeep from the tray and run Uninstall.cmd in the installed folder.'
