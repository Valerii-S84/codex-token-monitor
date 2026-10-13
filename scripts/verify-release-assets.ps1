param(
    [Parameter(Mandatory = $true)][string]$Directory,
    [Parameter(Mandatory = $true)][string]$ExpectedCommit,
    [string]$ReleaseTag = 'v2.0.0'
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $Directory).Path
$metadata = Get-Content -LiteralPath (Join-Path $root 'release.json') -Raw | ConvertFrom-Json
if ($metadata.commit -cne $ExpectedCommit -or $metadata.tag -cne $ReleaseTag -or ('v' + $metadata.version) -cne $ReleaseTag) { throw 'Release metadata identity mismatch.' }
$expectedNames = @('CodexTokenMonitor.exe','codex-token-monitor-autostart-1.1.1.vsix','release.json')
$seen = @()
foreach ($line in Get-Content -LiteralPath (Join-Path $root 'SHA256SUMS.txt')) {
    if ($line -notmatch '^([a-fA-F0-9]{64})  ([a-zA-Z0-9._-]+)$') { throw 'Invalid checksum entry.' }
    $name = $Matches[2]; $expectedHash = $Matches[1]
    if ($expectedNames -notcontains $name -or $seen -contains $name) { throw 'Unexpected or duplicate checksum file.' }
    $actual = Get-FileHash -LiteralPath (Join-Path $root $name) -Algorithm SHA256
    if ($actual.Hash -ine $expectedHash) { throw 'Checksum mismatch: ' + $name }
    $seen += $name
}
if ($seen.Count -ne $expectedNames.Count) { throw 'Incomplete checksum manifest.' }
if (@($metadata.files).Count -ne 2) { throw 'Incomplete release file metadata.' }
$payloadNames = @()
foreach ($file in $metadata.files) {
    if (@('CodexTokenMonitor.exe','codex-token-monitor-autostart-1.1.1.vsix') -notcontains $file.name) { throw 'Unexpected release payload.' }
    $path = Join-Path $root $file.name
    if ($payloadNames -contains $file.name) { throw 'Duplicate release payload metadata.' }
    $payloadNames += $file.name
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ine $file.sha256 -or (Get-Item -LiteralPath $path).Length -ne $file.size) { throw 'Payload metadata mismatch.' }
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead((Join-Path $root 'codex-token-monitor-autostart-1.1.1.vsix'))
try {
    $projectRoot = Split-Path -Parent $PSScriptRoot
    $sources = @{'extension/package.json'='vscode-autostart/package.json'; 'extension/extension.js'='vscode-autostart/extension.js'; 'extension/README.md'='vscode-autostart/README.md'; 'extension/LICENSE.txt'='LICENSE'}
    foreach ($name in $sources.Keys) {
        $entry = $archive.GetEntry($name)
        if (-not $entry) { throw 'VSIX entry missing: ' + $name }
        $reader = New-Object IO.StreamReader($entry.Open())
        try { $content = $reader.ReadToEnd() } finally { $reader.Dispose() }
        if ($content -cne [IO.File]::ReadAllText((Join-Path $projectRoot $sources[$name]))) { throw 'VSIX source mismatch: ' + $name }
    }
    $reader = New-Object IO.StreamReader($archive.GetEntry('extension.vsixmanifest').Open())
    try { [xml]$manifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
    if ($manifest.PackageManifest.Metadata.Identity.Version -ne '1.1.1') { throw 'VSIX manifest version mismatch.' }
} finally { $archive.Dispose() }
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'verify-binary.ps1') -ExecutablePath (Join-Path $root 'CodexTokenMonitor.exe') -ExpectedCommit $ExpectedCommit -ExpectedVersion $metadata.version
if ($LASTEXITCODE -ne 0) { throw 'Release binary verification failed.' }
$signature = Get-AuthenticodeSignature -LiteralPath (Join-Path $root 'CodexTokenMonitor.exe')
if ($signature.Status.ToString() -cne $metadata.signature) { throw 'Signature metadata mismatch.' }
Write-Output ('RELEASE_ASSETS_OK tag=' + $ReleaseTag + ' commit=' + $ExpectedCommit + ' checksums=3 VSIX=source-matched')
