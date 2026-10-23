$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $projectRoot
& (Join-Path $PSScriptRoot 'check.ps1')
$revisions = @(git rev-list --all)
if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate reachable history.' }
$pattern = '[A-Za-z]:[\\/](Users|Github项目)[\\/]|gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,}|-----BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY-----'
$matches = @()
foreach ($revision in $revisions) {
    $found = @(git grep --name-only -I -E -e $pattern $revision -- '*.md' '*.cs' '*.xaml' '*.json' '*.ps1' '*.cmd' '*.yml' '*.props' '*.csproj' '*.manifest')
    if ($LASTEXITCODE -notin @(0,1)) { throw 'History scan failed.' }
    $matches += $found
    $names = @(git ls-tree -r --name-only $revision)
    if ($LASTEXITCODE -ne 0) { throw 'History tree inspection failed.' }
    if ($names | Where-Object { $_ -match '(^|/)(\.cache|\.tools|bin|obj|dist|release|Recovery)/|\.(db|db-wal|db-shm|daykeep\.json)$' }) { throw 'Generated or private data found in reachable history.' }
}
if ($matches.Count) { $matches | Select-Object -Unique; throw 'Public-content findings require review; matched values withheld.' }
$emails = @(git log --all --format='%ae%n%ce' | Sort-Object -Unique)
if ($emails | Where-Object { $_ -notmatch '@users\.noreply\.github\.com$' }) { throw 'A non-noreply commit address requires public-content review.' }
$readmes = @('README.md','README.zh-TW.md','README.en.md','README.ja.md','README.ko.md','README.fr.md','README.de.md','README.es.md','README.pt.md')
foreach ($path in $readmes) {
    $body = Get-Content -LiteralPath $path -Raw
    # Count characters rather than UTF-8 bytes; CJK tutorials need fewer characters.
    if ($body.Length -lt 1800 -or ([regex]::Matches($body,'(?m)^## ')).Count -lt 6 -or $body -notmatch 'Install.cmd' -or $body -notmatch 'Uninstall.cmd' -or $body -notmatch '09:00' -or $body -notmatch '合并' -or $body -notmatch '替换' -or $body -notmatch '历史与备份' -or $body -notmatch 'SHA256SUMS' -or $body -notmatch 'AGPL-3.0-only') { throw "Incomplete localized tutorial: $path" }
    foreach ($other in $readmes) { if (-not $body.Contains('('+ $other +')')) { throw "Language navigation missing from $path" } }
}
Write-Output "Release source audit passed: $($revisions.Count) reachable commits; no matched secrets/private paths/generated data; noreply metadata; nine full tutorials."
Write-Output 'This is a bounded source scan and manual image/license review, not a guarantee against every disclosure or a hosted CI result.'
