. (Join-Path $PSScriptRoot 'common.ps1')
Add-Type -AssemblyName System.IO.Compression.FileSystem
$dirty=@(git status --porcelain --untracked-files=all)
if ($LASTEXITCODE -ne 0 -or $dirty.Count) { throw 'Release packaging requires a clean committed source tree.' }
& (Join-Path $PSScriptRoot 'audit-release.ps1')
$commit=(git rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot identify corresponding source commit.' }
$props=[xml](Get-Content -LiteralPath (Join-Path $projectRoot 'Directory.Build.props') -Raw)
$version=[string]$props.Project.PropertyGroup.Version
$output=Join-Path $projectRoot ("release\DayKeep-$version-"+$commit.Substring(0,8))
if (Test-Path -LiteralPath $output) { throw 'This release directory already exists. Preserve it; use a new source commit for a new package.' }
New-Item -ItemType Directory -Path $output | Out-Null
$payload=Join-Path $output 'payload'
& (Join-Path $PSScriptRoot 'publish-preview.ps1') -OutputDirectory $payload
Get-ChildItem -LiteralPath $projectRoot -Filter '*.md' -File | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $payload }
$docsRoot=Join-Path $payload 'docs'
New-Item -ItemType Directory -Path $docsRoot -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $projectRoot 'docs') -Force | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $docsRoot -Recurse -Force }
$assetRoot=Join-Path $payload 'assets'; New-Item -ItemType Directory -Path $assetRoot | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'assets\brand') -Destination $assetRoot -Recurse
Copy-Item -LiteralPath (Join-Path $projectRoot 'assets\screenshots') -Destination $assetRoot -Recurse
Get-ChildItem -LiteralPath (Join-Path $projectRoot 'packaging') -File | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $payload }
$sourceName="DayKeep-$version-source.zip"; $binaryName="DayKeep-$version-win-x64.zip"
$sourcePath=Join-Path $output $sourceName; $binaryPath=Join-Path $output $binaryName
git archive --format=zip "--prefix=DayKeep-$version-source/" "--output=$sourcePath" $commit
if ($LASTEXITCODE -ne 0) { throw 'Corresponding-source archive failed.' }
@("# Corresponding source",'',"Version: $version",'',"Commit: $commit",'',"Companion archive: $sourceName",'',"Original work: AGPL-3.0-only. See LICENSE; third-party notices are in licenses/.",'','Build from the companion source with the SDK in global.json and scripts/publish-preview.ps1. No private task data, SDK binaries or NuGet caches belong to this source archive.','', 'This is an unsigned pre-release candidate. Three-day human trial and hardware acceptance remain pending. Public publication requires maintainer confirmation.') | Set-Content -LiteralPath (Join-Path $payload 'SOURCE_INFO.md') -Encoding UTF8
$files=@(Get-ChildItem -LiteralPath $payload -File -Recurse | Sort-Object FullName | ForEach-Object {
    $relative=[IO.Path]::GetRelativePath($payload,$_.FullName).Replace('\','/')
    if ($relative -match '(^|/)(\.cache|\.tools|Recovery|bin|obj)/|\.db($|-)|\.daykeep\.json$|\.pdb$') { throw 'Generated/private data found in binary payload.' }
    [ordered]@{path=$relative;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
})
[ordered]@{product='DayKeep.Desktop.Local';format=1;version=$version;sourceCommit=$commit;files=$files} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $payload 'PACKAGE.json') -Encoding UTF8
[IO.Compression.ZipFile]::CreateFromDirectory($payload,$binaryPath,[IO.Compression.CompressionLevel]::Optimal,$false)
Copy-Item -LiteralPath (Join-Path $projectRoot 'RELEASE_NOTES.md') -Destination $output
$hashes=@($binaryName,$sourceName,'RELEASE_NOTES.md' | ForEach-Object { (Get-FileHash -LiteralPath (Join-Path $output $_) -Algorithm SHA256).Hash+'  '+$_ })
$hashes | Set-Content -LiteralPath (Join-Path $output 'SHA256SUMS') -Encoding ASCII
$reports=Join-Path $projectRoot ('.cache\release-'+$version+'-'+$commit.Substring(0,8))
& (Join-Path $PSScriptRoot 'test-package.ps1') -PackagePath $binaryPath -SourcePath $sourcePath -ReportDirectory $reports
foreach ($line in $hashes) { $expected,$name=$line -split '  ',2; if ((Get-FileHash -LiteralPath (Join-Path $output $name) -Algorithm SHA256).Hash -ne $expected) { throw 'Final archive checksum mismatch.' } }
Copy-Item -LiteralPath (Join-Path $reports 'package-report.json') -Destination $output
$hashes += (Get-FileHash -LiteralPath (Join-Path $output 'package-report.json') -Algorithm SHA256).Hash+'  package-report.json'
$hashes | Set-Content -LiteralPath (Join-Path $output 'SHA256SUMS') -Encoding ASCII
Write-Output "Release candidate packaged and verified: $output"
Write-Output "Source commit: $commit"
Write-Output 'This update candidate has not been pushed, tagged or published as a GitHub Release. The existing public repository and 0.6.0-l6 release are unchanged. Await maintainer confirmation.'
