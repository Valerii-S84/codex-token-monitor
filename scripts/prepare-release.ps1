param(
    [string]$ReleaseTag = 'v2.1.0',
    [string]$Directory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts\build')
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$dirty = @(& git -C $projectRoot status --porcelain --untracked-files=all)
if ($LASTEXITCODE -ne 0 -or $dirty.Count -ne 0) { throw 'Release packaging requires a clean source checkout.' }
$commit = (& git -C $projectRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Unable to identify source commit.' }
$root = (Resolve-Path -LiteralPath $Directory).Path
$binary = Join-Path $root 'CodexTokenMonitor.exe'
$build = Get-Content -LiteralPath ($binary + '.release.json') -Raw | ConvertFrom-Json
if ($build.commit -cne $commit -or $build.tag -cne $ReleaseTag -or ('v' + $build.version) -cne $ReleaseTag) { throw 'Candidate belongs to a different commit/version.' }
if ((Get-FileHash -LiteralPath $binary -Algorithm SHA256).Hash -ine $build.sha256) { throw 'Candidate changed since building.' }
$vsixName = 'codex-token-monitor-autostart-1.1.1.vsix'
Copy-Item -LiteralPath (Join-Path $projectRoot ('artifacts\' + $vsixName)) -Destination (Join-Path $root $vsixName) -Force
$files = foreach ($name in @('CodexTokenMonitor.exe',$vsixName)) {
    $path = Join-Path $root $name
    [ordered]@{name=$name; size=(Get-Item -LiteralPath $path).Length; sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()}
}
[ordered]@{version=$build.version; tag=$ReleaseTag; commit=$commit; signature=$build.signature; builtUtc=$build.builtUtc; files=@($files)} |
    ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $root 'release.json') -Encoding UTF8
$lines = foreach ($name in @('CodexTokenMonitor.exe',$vsixName,'release.json')) {
    (Get-FileHash -LiteralPath (Join-Path $root $name) -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $name
}
[IO.File]::WriteAllLines((Join-Path $root 'SHA256SUMS.txt'), $lines, (New-Object Text.UTF8Encoding($false)))
$notes = @"
Codex Token Monitor $ReleaseTag is a local Windows monitor for Codex sessions, parallel agents, token usage, context, and daily analytics.

Requirements: Windows 10 or 11 and .NET Framework 4.8 or later. The complete interface is available in Ukrainian and English. No API key or installer is required.

Choose Appearance / Language to switch immediately without restarting. The choice persists between launches. Existing installations retain Ukrainian until you choose otherwise; new installations use Ukrainian on Ukrainian Windows and English elsewhere. Dates, durations, tooltips, menus, chart labels, and analytics update together while your selected agent, tab, window size, and chart history remain intact. Original task, repository, and model names are preserved.

This release preserves the compact/expanded layouts, analytics, incremental indexing, palettes, animation, always-on-top mode, and VS Code autostart. Windows CI verifies nine render scenarios per language (18 cases), localization and settings migration, regression gates, and the exact candidate binary in isolated data directories.

Source commit: $commit
Monitor version: $($build.version) (EXE/assembly/manifest $($build.version).0)
VS Code autostart extension: 1.1.1
Authenticode status: $($build.signature)

Download CodexTokenMonitor.exe and optionally codex-token-monitor-autostart-1.1.1.vsix. Compare Get-FileHash -Algorithm SHA256 output with SHA256SUMS.txt from this release. release.json records the source commit, sizes, hashes, and signing status. SHA-256 verifies file identity, not publisher identity.

The root repository EXE is historical; use these versioned assets. Read the repository README for setup and metric definitions. This independent project is MIT licensed and is not an official OpenAI product.
"@
[IO.File]::WriteAllText((Join-Path $root 'release-notes.md'), $notes, (New-Object Text.UTF8Encoding($false)))
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'verify-release-assets.ps1') -Directory $root -ExpectedCommit $commit -ReleaseTag $ReleaseTag
if ($LASTEXITCODE -ne 0) { throw 'Release candidate gate failed.' }
Write-Output ('RELEASE_CANDIDATE_OK ' + $root)
