param([Parameter(Mandatory=$true)][string]$PackagePath,[Parameter(Mandatory=$true)][string]$SourcePath,[Parameter(Mandatory=$true)][string]$ReportDirectory)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
New-Item -ItemType Directory -Path $ReportDirectory -ErrorAction Stop | Out-Null
$unpacked=Join-Path $ReportDirectory 'unpacked'
[IO.Compression.ZipFile]::ExtractToDirectory($PackagePath,$unpacked)
. (Join-Path $unpacked 'PackageTools.ps1')
$manifest=Read-DayKeepPackage $unpacked
$checks=[Collections.Generic.List[object]]::new()
function Test-PackageCheck([string]$Name,[scriptblock]$Action) {
    & $Action
    $checks.Add([ordered]@{name=$Name;passed=$true})
    Write-Output "PASS $Name"
}
function Invoke-PackageScript([string]$Script,[string[]]$Arguments,[bool]$MustSucceed=$true) {
    $info=[Diagnostics.ProcessStartInfo]::new((Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'))
    $info.UseShellExecute=$false; $info.CreateNoWindow=$true; $info.RedirectStandardOutput=$true; $info.RedirectStandardError=$true
    foreach ($arg in @('-NoLogo','-NoProfile','-ExecutionPolicy','Bypass','-File',$Script)+$Arguments) { $info.ArgumentList.Add($arg) }
    $process=[Diagnostics.Process]::Start($info)
    $output=$process.StandardOutput.ReadToEndAsync(); $errorText=$process.StandardError.ReadToEndAsync()
    if (-not $process.WaitForExit(60000)) { $process.Kill(); throw 'Installer verification timed out.' }
    $null=$output.GetAwaiter().GetResult(); $detail=$errorText.GetAwaiter().GetResult()
    if (($process.ExitCode -eq 0) -ne $MustSucceed) { throw "Unexpected script exit $($process.ExitCode). $detail" }
}
$install=Join-Path $unpacked 'Install.ps1'
$target=Join-Path $ReportDirectory '安装 空间\DayKeep'
Test-PackageCheck 'Extracted payload matches every manifest hash' { Test-DayKeepFiles $unpacked $manifest }
Test-PackageCheck 'Payload includes the nine tutorials and language-switching guide' {
    foreach ($path in @('README.md','README.zh-TW.md','README.en.md','README.ja.md','README.ko.md','README.fr.md','README.de.md','README.es.md','README.pt.md','docs/LANGUAGES.md')) {
        if (-not (Test-Path -LiteralPath (Get-DayKeepChild $unpacked $path) -PathType Leaf)) { throw "Missing packaged language guide: $path" }
        if ($path -notin @($manifest.files.path)) { throw "Language guide is absent from package manifest: $path" }
    }
}
Test-PackageCheck 'Portable uninstaller refuses an uninstalled folder' { Invoke-PackageScript (Join-Path $unpacked 'Uninstall.ps1') @('-NoShortcuts') $false; Test-DayKeepFiles $unpacked $manifest }
Test-PackageCheck 'Windows PowerShell 5.1 installs into an isolated Unicode path' { Invoke-PackageScript $install @('-TargetDirectory',$target,'-NoShortcuts'); Test-DayKeepFiles $target $manifest }
Test-PackageCheck 'Identical reinstall is safe and leaves file hashes unchanged' { Invoke-PackageScript $install @('-TargetDirectory',$target,'-NoShortcuts'); Test-DayKeepFiles $target $manifest }
Test-PackageCheck 'Shortcut adapter creates links and preserves unrelated targets in an isolated folder' {
    $path=Join-Path $ReportDirectory 'test.lnk';$exe=Join-Path $target 'DayKeep.exe'
    Set-DayKeepShortcut $path $exe
    $shell=New-Object -ComObject WScript.Shell
    if ($shell.CreateShortcut($path).TargetPath -ne $exe) { throw 'Shortcut did not point to installed app.' }
    $unrelated=Join-Path $ReportDirectory 'unrelated.exe';$link=$shell.CreateShortcut($path);$link.TargetPath=$unrelated;$link.Save()
    Set-DayKeepShortcut $path $exe
    if ($shell.CreateShortcut($path).TargetPath -ne $unrelated) { throw 'Unrelated shortcut changed.' }
}
Test-PackageCheck 'Manifest paths cannot escape the package root' {
    $rejected=$false;try { $null=Get-DayKeepChild $unpacked '../outside.txt' } catch { $rejected=$true }
    if (-not $rejected) { throw 'Traversal path was accepted.' }
}
Test-PackageCheck 'Tampered payload prevents installation before creating a target' {
    $path=Join-Path $unpacked 'README.md';$original=[IO.File]::ReadAllBytes($path);[IO.File]::AppendAllText($path,'controlled modification')
    $destination=Join-Path $ReportDirectory 'tampered-target'
    try { Invoke-PackageScript $install @('-TargetDirectory',$destination,'-NoShortcuts') $false; if (Test-Path -LiteralPath $destination) { throw 'Tampered package created an installation.' } }
    finally { [IO.File]::WriteAllBytes($path,$original) }
}
Test-PackageCheck 'Unknown existing installation directory is preserved' {
    $unknown=Join-Path $ReportDirectory 'unknown'; New-Item -ItemType Directory -Path $unknown | Out-Null
    $sentinel=Join-Path $unknown 'keep.txt'; [IO.File]::WriteAllText($sentinel,'preserve')
    Invoke-PackageScript $install @('-TargetDirectory',$unknown,'-NoShortcuts') $false
    if ([IO.File]::ReadAllText($sentinel) -ne 'preserve') { throw 'Unknown target changed.' }
}
Test-PackageCheck 'Modified installed files block reinstall and uninstall' {
    $path=Join-Path $target 'README.md'; $original=[IO.File]::ReadAllBytes($path); [IO.File]::AppendAllText($path,'controlled modification')
    Invoke-PackageScript $install @('-TargetDirectory',$target,'-NoShortcuts') $false
    Invoke-PackageScript (Join-Path $target 'Uninstall.ps1') @('-NoShortcuts') $false
    if (-not [IO.File]::ReadAllText($path).EndsWith('controlled modification')) { throw 'Modified content lost.' }
    [IO.File]::WriteAllBytes($path,$original)
}
Test-PackageCheck 'Extra files prevent uninstall and stay intact' {
    $path=Join-Path $target 'keep.txt'; [IO.File]::WriteAllText($path,'preserve')
    Invoke-PackageScript (Join-Path $target 'Uninstall.ps1') @('-NoShortcuts') $false
    if ([IO.File]::ReadAllText($path) -ne 'preserve') { throw 'Extra content lost.' }
    Remove-Item -LiteralPath $path
}
Test-PackageCheck 'Installed executable passes real WPF and data actions' {
    $smoke=Join-Path $ReportDirectory 'installed-smoke'
    $info=[Diagnostics.ProcessStartInfo]::new((Join-Path $target 'DayKeep.exe')); $info.UseShellExecute=$false; $info.CreateNoWindow=$true
    $info.ArgumentList.Add('--smoke-test'); $info.ArgumentList.Add($smoke)
    $process=[Diagnostics.Process]::Start($info)
    if (-not $process.WaitForExit(60000)) { $process.Kill(); throw 'Packaged smoke check timed out.' }
    $report=Get-Content -LiteralPath (Join-Path $smoke 'report.json') -Raw | ConvertFrom-Json
    if ($process.ExitCode -ne 0 -or $report.failures -ne 0) { throw 'Installed WPF checks failed.' }
}
Test-PackageCheck 'Uninstall removes only this version and preserves sibling data' {
    $sentinel=Join-Path (Split-Path -Parent $target) 'preserve.txt'; [IO.File]::WriteAllText($sentinel,'sibling')
    Invoke-PackageScript (Join-Path $target 'Uninstall.ps1') @('-NoShortcuts')
    if ((Test-Path -LiteralPath $target) -or [IO.File]::ReadAllText($sentinel) -ne 'sibling') { throw 'Uninstall scope mismatch.' }
}
Test-PackageCheck 'Corresponding source includes localization, language checks and nine tutorials' {
    $zip=[IO.Compression.ZipFile]::OpenRead($SourcePath)
    try {
        $requiredSource = @('src/DayKeep.Core/Backups.cs','src/DayKeep.Desktop/TaskDataWindow.xaml','src/DayKeep.Desktop/Localization/Localizer.cs','src/DayKeep.Desktop/Infrastructure/LanguageUiChecks.cs','docs/LANGUAGES.md','packaging/Install.ps1','LICENSE','README.md','README.zh-TW.md','README.en.md','README.ja.md','README.ko.md','README.fr.md','README.de.md','README.es.md','README.pt.md')
        $requiredSource += @('zh-CN','zh-TW','en','ja','ko','fr','de','es','pt' | ForEach-Object { "src/DayKeep.Desktop/Localization/$_.json" })
        foreach ($path in $requiredSource) {
            if (-not $zip.GetEntry("DayKeep-$($manifest.version)-source/$path")) { throw "Missing source entry: $path" }
        }
        if ($zip.Entries | Where-Object { $_.FullName -match '/(\.cache|\.tools|bin|obj|dist|release|Recovery)/|\.daykeep\.json$|\.db$' }) { throw 'Private/generated data in source archive.' }
    } finally { $zip.Dispose() }
}
$smokeReport=Get-Content -LiteralPath (Join-Path $ReportDirectory 'installed-smoke\report.json') -Raw | ConvertFrom-Json
[ordered]@{stage=$manifest.version;passed=$true;version=$manifest.version;sourceCommit=$manifest.sourceCommit;checks=$checks;wpfChecks=@($smokeReport.checks | Where-Object { $_.passed -eq $true }).Count;scope='Extracted ZIP with nine tutorials and language guide, isolated per-user installer path without real shortcuts, Windows PowerShell 5.1; installed app smoke including language switching. Private task database and login startup untouched.'} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $ReportDirectory 'package-report.json') -Encoding UTF8
