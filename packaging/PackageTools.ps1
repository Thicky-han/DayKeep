$ErrorActionPreference = 'Stop'
$dayKeepProductId = 'DayKeep.Desktop.Local'
function Get-DayKeepChild([string]$Root, [string]$Relative) {
    if ([string]::IsNullOrWhiteSpace($Relative) -or [IO.Path]::IsPathRooted($Relative) -or $Relative.Contains(':')) { throw 'Invalid package path.' }
    $base = [IO.Path]::GetFullPath($Root).TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar
    $child = [IO.Path]::GetFullPath((Join-Path $Root $Relative))
    if (-not $child.StartsWith($base,[StringComparison]::OrdinalIgnoreCase)) { throw 'Package path escapes its directory.' }
    return $child
}
function Read-DayKeepPackage([string]$Root) {
    $manifest = Get-Content -LiteralPath (Join-Path $Root 'PACKAGE.json') -Raw | ConvertFrom-Json
    if ($manifest.product -ne $dayKeepProductId -or $manifest.format -ne 1 -or $manifest.version -notmatch '^\d+\.\d+\.\d+[-.a-zA-Z0-9]*$' -or @($manifest.files).Count -eq 0) { throw 'Unrecognized package manifest.' }
    $seen = @{}
    foreach ($entry in $manifest.files) {
        $path = Get-DayKeepChild $Root $entry.path
        if ($seen.ContainsKey($path) -or $entry.sha256 -notmatch '^[A-Fa-f0-9]{64}$' -or $entry.path -eq 'PACKAGE.json' -or $entry.path -eq 'INSTALLED.json') { throw 'Invalid package file entry.' }
        $seen[$path] = $true
    }
    return $manifest
}
function Test-DayKeepFiles([string]$Root, $Manifest) {
    foreach ($entry in $Manifest.files) {
        $path = Get-DayKeepChild $Root $entry.path
        if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $entry.sha256) { throw "Missing or modified package file: $($entry.path)" }
    }
}
function Assert-DayKeepNotRunning([string]$Root) {
    $exe = [IO.Path]::GetFullPath((Join-Path $Root 'DayKeep.exe'))
    foreach ($process in @(Get-Process -Name DayKeep -ErrorAction SilentlyContinue)) {
        try { $path = $process.Path } catch { throw 'Cannot inspect a running DayKeep process. Save and exit DayKeep from its tray before continuing.' }
        if ($path -and [string]::Equals($path,$exe,[StringComparison]::OrdinalIgnoreCase)) { throw 'DayKeep is running from this directory. Save forms and exit from the tray first.' }
    }
}
function Set-DayKeepShortcut([string]$Path,[string]$Exe) {
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($Path)
    if ((Test-Path -LiteralPath $Path) -and $shortcut.TargetPath -ne $Exe) {
        $oldRoot = Split-Path -Parent $shortcut.TargetPath
        $oldMarker = Join-Path $oldRoot 'INSTALLED.json'
        if (-not (Test-Path -LiteralPath $oldMarker)) { Write-Output "Existing shortcut preserved: $Path"; return }
        $old = Get-Content -LiteralPath $oldMarker -Raw | ConvertFrom-Json
        if ($old.product -ne $dayKeepProductId -or [IO.Path]::GetFileName($shortcut.TargetPath) -ne 'DayKeep.exe') { throw 'Unrecognized shortcut target was preserved.' }
    }
    $shortcut.TargetPath=$Exe; $shortcut.WorkingDirectory=Split-Path -Parent $Exe
    $shortcut.IconLocation=(Join-Path (Split-Path -Parent $Exe) 'DayKeep.ico')+',0'
    $shortcut.Description='DayKeep - daily reminders'; $shortcut.Save()
}
