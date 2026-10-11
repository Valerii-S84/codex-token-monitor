$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
$root = Split-Path -Parent $PSScriptRoot
$package = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'package.json') -Raw | ConvertFrom-Json
$output = Join-Path $root ('artifacts\' + $package.name + '-' + $package.version + '.vsix')
$manifest = @'
<?xml version="1.0" encoding="utf-8"?>
<PackageManifest Version="2.0.0" xmlns="http://schemas.microsoft.com/developer/vsx-schema/2011">
  <Metadata>
    <Identity Language="en-US" Id="codex-token-monitor-autostart" Version="1.1.1" Publisher="local" />
    <DisplayName>Codex Token Monitor Autostart</DisplayName>
    <Description xml:space="preserve">Starts the local Codex Token Monitor when VS Code is ready.</Description>
    <Categories>Other</Categories>
    <Properties><Property Id="Microsoft.VisualStudio.Code.Engine" Value="^1.80.0" /></Properties>
  </Metadata>
  <Installation><InstallationTarget Id="Microsoft.VisualStudio.Code" /></Installation>
  <Dependencies />
  <Assets>
    <Asset Type="Microsoft.VisualStudio.Code.Manifest" Path="extension/package.json" Addressable="true" />
    <Asset Type="Microsoft.VisualStudio.Services.Content.Details" Path="extension/README.md" Addressable="true" />
    <Asset Type="Microsoft.VisualStudio.Services.Content.License" Path="extension/LICENSE.txt" Addressable="true" />
  </Assets>
</PackageManifest>
'@
$contentTypes = @'
<?xml version="1.0" encoding="utf-8"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="json" ContentType="application/json" />
  <Default Extension="js" ContentType="application/javascript" />
  <Default Extension="md" ContentType="text/markdown" />
  <Default Extension="txt" ContentType="text/plain" />
  <Default Extension="vsixmanifest" ContentType="text/xml" />
</Types>
'@
$stream = [IO.File]::Open($output, [IO.FileMode]::Create)
$zip = New-Object IO.Compression.ZipArchive($stream, [IO.Compression.ZipArchiveMode]::Create, $false)
try {
    $entries = @{
        'extension.vsixmanifest' = $manifest
        '[Content_Types].xml' = $contentTypes
        'extension/package.json' = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'package.json'))
        'extension/extension.js' = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'extension.js'))
        'extension/README.md' = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'README.md'))
        'extension/LICENSE.txt' = [IO.File]::ReadAllText((Join-Path $root 'LICENSE'))
    }
    foreach ($name in $entries.Keys) {
        $entry = $zip.CreateEntry($name)
        $writer = New-Object IO.StreamWriter($entry.Open(), (New-Object Text.UTF8Encoding($false)))
        try { $writer.Write($entries[$name]) } finally { $writer.Dispose() }
    }
} finally { $zip.Dispose(); $stream.Dispose() }
Write-Output ('VSIX_READY ' + $output)
