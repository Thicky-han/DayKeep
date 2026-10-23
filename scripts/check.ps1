$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $projectRoot
$paths = @(git ls-files --cached --others --exclude-standard)
if ($LASTEXITCODE -ne 0) { throw 'Cannot list source files.' }
$required = @('README.md', 'PROJECT_SPEC.md', 'IMPLEMENTATION_PLAN.md', 'AGENTS.md', 'LICENSE', 'PRIVACY.md', 'INSTALL.md', 'DEPENDENCIES.md', 'L0_REPORT.md', 'L1_REPORT.md', 'L2_REPORT.md', 'L3_REPORT.md', 'L4_REPORT.md', 'L5_REPORT.md', 'docs/DATA_MODEL.md', 'docs/REMINDERS.md', 'docs/BACKUP_FORMAT.md', 'docs/TRIAL_CHECKLIST.md', 'src/DayKeep.Core/packages.lock.json', 'src/DayKeep.Desktop/packages.lock.json', 'tests/DayKeep.Core.Checks/packages.lock.json')
foreach ($path in $required) { if (-not (Test-Path -LiteralPath (Join-Path $projectRoot $path))) { throw "Missing $path" } }
foreach ($path in @('L6_REPORT.md','RELEASE_NOTES.md','README.zh-TW.md','README.en.md','README.ja.md','README.ko.md','README.fr.md','README.de.md','README.es.md','README.pt.md','packaging/Install.ps1','packaging/Uninstall.ps1','scripts/package-release.ps1')) {
    if (-not (Test-Path -LiteralPath (Join-Path $projectRoot $path))) { throw "Missing $path" }
}
$failures = [System.Collections.Generic.List[string]]::new()
foreach ($path in $paths) {
    if ($path -match '(^|/)(\.tools|\.cache|bin|obj|dist|release)/') { $failures.Add("Generated or local file: $path"); continue }
    $fullPath = Join-Path $projectRoot $path
    if ($path -match '\.(xaml|csproj|props|manifest|svg|config)$') {
        try { $null = [xml](Get-Content -LiteralPath $fullPath -Raw) }
        catch { $failures.Add("Invalid XML: $path") }
    }
    if ($path -match '\.json$') {
        try { $null = Get-Content -LiteralPath $fullPath -Raw | ConvertFrom-Json }
        catch { $failures.Add("Invalid JSON: $path") }
    }
    if ($path -match '\.ps1$') {
        $tokens=$null; $parseErrors=$null
        $null=[Management.Automation.Language.Parser]::ParseFile($fullPath,[ref]$tokens,[ref]$parseErrors)
        if ($parseErrors.Count) { $failures.Add("Invalid PowerShell: $path") }
    }
    if ($path -match '\.(md|cs|xaml|json|ps1|cmd|yml|props|csproj|manifest)$') {
        $body = Get-Content -LiteralPath $fullPath -Raw
        if ($body -match 'gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,}|-----BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY-----') { $failures.Add("Possible secret in $path (value withheld)") }
        if ($body -match '[A-Za-z]:[\\/](Users|Github项目)[\\/]') { $failures.Add("Machine-specific path in $path") }
        if ($path -match '\.md$') {
            foreach ($match in [regex]::Matches($body, '\[[^\]]+\]\(([^\s)]+)\)')) {
                $target = $match.Groups[1].Value
                if ($target -match '^(https?:|#|mailto:)') { continue }
                $localTarget = ($target -split '#')[0]
                if ($localTarget -and -not (Test-Path -LiteralPath (Join-Path (Split-Path -Parent $fullPath) $localTarget))) { $failures.Add("Broken relative link in $path") }
            }
        }
    }
}
if ($failures.Count -gt 0) { $failures | Write-Output; throw "$($failures.Count) source checks failed." }
Write-Output "Source check passed: $($paths.Count) files; XML/JSON, relative links, generated-file and basic public-content checks."
