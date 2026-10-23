param([switch]$NoShortcuts)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'PackageTools.ps1')
$root = [IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\','/')
if (((Get-Item -LiteralPath $root).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Installation root is a link or junction. No files were removed.' }
$manifest = Read-DayKeepPackage $root
$markerPath = Join-Path $root 'INSTALLED.json'
if (-not (Test-Path -LiteralPath $markerPath)) { throw 'This is a portable folder, not an installed copy. No files were removed.' }
$marker = Get-Content -LiteralPath $markerPath -Raw | ConvertFrom-Json
if ($marker.product -ne $dayKeepProductId -or $marker.sourceCommit -ne $manifest.sourceCommit) { throw 'Installation marker does not match. No files were removed.' }
Assert-DayKeepNotRunning $root
Test-DayKeepFiles $root $manifest
$allowed = @{}
foreach ($entry in $manifest.files) { $allowed[(Get-DayKeepChild $root $entry.path)]=$true }
$allowed[$markerPath]=$true; $allowed[(Join-Path $root 'PACKAGE.json')]=$true
$all = @(Get-ChildItem -LiteralPath $root -Recurse -Force)
foreach ($item in $all) {
    if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'A link or junction was found. No files were removed.' }
    if (-not $item.PSIsContainer -and -not $allowed.ContainsKey($item.FullName)) { throw 'Extra files were found. Preserve them outside this program folder before uninstalling.' }
}
if (-not $NoShortcuts) {
    $shell = New-Object -ComObject WScript.Shell
    foreach ($folder in @([Environment]::GetFolderPath('Desktop'),[Environment]::GetFolderPath('Programs'))) {
        $path=Join-Path $folder 'DayKeep.lnk'
        if ((Test-Path -LiteralPath $path) -and $shell.CreateShortcut($path).TargetPath -eq (Join-Path $root 'DayKeep.exe')) { Remove-Item -LiteralPath $path }
    }
}
# Only manifest-listed files inside this verified root are removed; no recursive deletion.
foreach ($path in $allowed.Keys) { Remove-Item -LiteralPath $path -ErrorAction Stop }
foreach ($folder in @($all | Where-Object PSIsContainer | Sort-Object { $_.FullName.Length } -Descending)) {
    if (@(Get-ChildItem -LiteralPath $folder.FullName -Force).Count -eq 0) { Remove-Item -LiteralPath $folder.FullName }
}
if (@(Get-ChildItem -LiteralPath $root -Force).Count -eq 0) { Remove-Item -LiteralPath $root }
Write-Output 'This program version was removed. Private task data and other versions were preserved.'
Write-Output 'Any old login-startup setting can be managed in Windows Settings > Apps > Startup.'
