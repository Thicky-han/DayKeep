$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$applicationPath = Join-Path $projectRoot 'dist\l1-win-x64\DayKeep.exe'
if (-not (Test-Path -LiteralPath $applicationPath)) { throw 'Run publish-preview.ps1 first.' }
$desktopDirectory = [Environment]::GetFolderPath('Desktop')
$shortcutPath = Join-Path $desktopDirectory 'DayKeep 日守.lnk'
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$previousPreview = Join-Path $projectRoot 'dist\l0-win-x64\DayKeep.exe'
if ((Test-Path -LiteralPath $shortcutPath) -and $shortcut.TargetPath -ne $applicationPath -and $shortcut.TargetPath -ne $previousPreview) { throw 'A shortcut with a different target already exists; it was preserved.' }
$shortcut.TargetPath = $applicationPath
$shortcut.WorkingDirectory = Split-Path -Parent $applicationPath
$shortcut.IconLocation = "$applicationPath,0"
$shortcut.Description = 'DayKeep 日守 · L1 本地任务管理，尚未接入定时提醒'
$shortcut.Save()
Write-Output "Created desktop shortcut: $shortcutPath"
