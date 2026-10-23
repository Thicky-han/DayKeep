$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$applicationPath = Join-Path $projectRoot 'dist\0.7.0-beta.1-win-x64\DayKeep.exe'
if (-not (Test-Path -LiteralPath $applicationPath)) { throw 'Run publish-preview.ps1 first.' }
$desktopDirectory = [Environment]::GetFolderPath('Desktop')
$shortcutPath = Join-Path $desktopDirectory 'DayKeep 日守.lnk'
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$previousPreviews = @('l0-win-x64','l1-win-x64','l1-logo-win-x64','l2-win-x64','l3-win-x64','l4-win-x64','l5-win-x64','l6-win-x64' | ForEach-Object { Join-Path $projectRoot "dist\$_\DayKeep.exe" })
if ((Test-Path -LiteralPath $shortcutPath) -and $shortcut.TargetPath -ne $applicationPath -and $shortcut.TargetPath -notin $previousPreviews) { throw 'A shortcut with a different target already exists; it was preserved.' }
$shortcut.TargetPath = $applicationPath
$shortcut.WorkingDirectory = Split-Path -Parent $applicationPath
$iconPath = Join-Path (Split-Path -Parent $applicationPath) 'DayKeep.ico'
if (-not (Test-Path -LiteralPath $iconPath)) { throw 'Preview icon is missing; publish the preview again.' }
$shortcut.IconLocation = "$iconPath,0"
$shortcut.Description = 'DayKeep 日守 · 0.7.0-beta.1 九语界面与备份恢复'
$shortcut.Save()
Write-Output "Created desktop shortcut: $shortcutPath"
