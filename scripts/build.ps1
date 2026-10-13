param(
    [string]$OutputPath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'CodexTokenMonitor.exe'),
    [switch]$Production,
    [switch]$Release,
    [string]$ReleaseTag,
    [string]$CodeSigningThumbprint = $env:CODE_SIGNING_THUMBPRINT,
    [string]$TimestampServer = 'http://timestamp.digicert.com'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$releaseCommit = $null
$certificate = $null
if ($Release -and $Production) { throw 'Choose -Release (unsigned) or -Production (signed).' }
$assemblyText = [IO.File]::ReadAllText((Join-Path $projectRoot 'src\AssemblyInfo.cs'))
$versionMatch = [regex]::Match($assemblyText, 'AssemblyVersion\("(\d+\.\d+\.\d+)\.0"\)')
if (-not $versionMatch.Success) { throw 'AssemblyVersion must specify a release version with revision zero.' }
$version = $versionMatch.Groups[1].Value
[xml]$manifest = Get-Content -LiteralPath (Join-Path $projectRoot 'resources\app.manifest')
if ($manifest.assembly.assemblyIdentity.version -ne ($version + '.0')) { throw 'Manifest and assembly versions differ.' }
if ($Production -or $Release) {
    $dirty = @(& git -C $projectRoot status --porcelain --untracked-files=all)
    if ($LASTEXITCODE -ne 0) { throw 'Unable to verify the Git working tree.' }
    if ($dirty.Count -ne 0) { throw 'Release build requires a clean Git working tree.' }
    $releaseCommit = (& git -C $projectRoot rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Unable to read source commit.' }
    if ($Production) {
        $ReleaseTag = (& git -C $projectRoot describe --tags --exact-match HEAD 2>$null)
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($ReleaseTag)) { throw 'Production build requires a release tag on HEAD.' }
    }
    if ($ReleaseTag -cne ('v' + $version)) { throw 'ReleaseTag must match the assembly version: v' + $version }
    & git -C $projectRoot show-ref --verify --quiet ('refs/tags/' + $ReleaseTag)
    if ($LASTEXITCODE -eq 0) {
        $tagCommit = (& git -C $projectRoot rev-list -n 1 $ReleaseTag).Trim()
        if ($tagCommit -ne $releaseCommit) { throw 'Existing release tag points to a different commit.' }
    }
    $releaseTag = $ReleaseTag
}
if ($Production) {
    if ([string]::IsNullOrWhiteSpace($CodeSigningThumbprint)) { throw 'Production build requires -CodeSigningThumbprint or CODE_SIGNING_THUMBPRINT.' }
    $normalizedThumbprint = $CodeSigningThumbprint.Replace(' ', '').ToUpperInvariant()
    $certificate = Get-Item -LiteralPath ('Cert:\CurrentUser\My\' + $normalizedThumbprint) -ErrorAction Stop
    if (-not $certificate.HasPrivateKey -or $certificate.NotBefore -gt (Get-Date) -or $certificate.NotAfter -le (Get-Date)) {
        throw 'The code-signing certificate is not currently valid or has no private key.'
    }
}
$sourceFiles = Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' -File | Select-Object -ExpandProperty FullName
if (-not $sourceFiles) { throw 'No C# source files found.' }

$provider = New-Object Microsoft.CSharp.CSharpCodeProvider
$parameters = New-Object System.CodeDom.Compiler.CompilerParameters
$parameters.GenerateExecutable = $true
$parameters.GenerateInMemory = $false
$parameters.IncludeDebugInformation = $false
$parameters.OutputAssembly = [IO.Path]::GetFullPath($OutputPath)
[void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($parameters.OutputAssembly))
$manifestPath = Join-Path $projectRoot 'resources\app.manifest'
$iconPath = Join-Path $projectRoot 'assets\codex-token-monitor.ico'
if (-not (Test-Path -LiteralPath $iconPath -PathType Leaf)) { throw 'Application icon was not found: ' + $iconPath }
$parameters.CompilerOptions = '/target:winexe /optimize+ /platform:anycpu /win32manifest:"' + $manifestPath + '" /win32icon:"' + $iconPath + '"'
@('System.dll', 'System.Core.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll') | ForEach-Object { [void]$parameters.ReferencedAssemblies.Add($_) }

if (Test-Path -LiteralPath $parameters.OutputAssembly) { Remove-Item -LiteralPath $parameters.OutputAssembly -Force }
$buildInfoPath = $null
try {
    if ($Release -or $Production) {
        $buildInfoPath = Join-Path ([IO.Path]::GetTempPath()) ('CodexBuildInfo-' + [Guid]::NewGuid().ToString('N') + '.cs')
        $buildInfo = '[assembly: System.Reflection.AssemblyInformationalVersion("' + $version + '+' + $releaseCommit + '")]' + [Environment]::NewLine +
            '[assembly: System.Reflection.AssemblyMetadata("SourceCommit", "' + $releaseCommit + '")]'
        [IO.File]::WriteAllText($buildInfoPath, $buildInfo)
        $sourceFiles = @($sourceFiles) + $buildInfoPath
    }
    $result = $provider.CompileAssemblyFromFile($parameters, [string[]]$sourceFiles)
} finally {
    $provider.Dispose()
    if ($buildInfoPath -and (Test-Path -LiteralPath $buildInfoPath)) { Remove-Item -LiteralPath $buildInfoPath }
}
if ($result.Errors.HasErrors) {
    $messages = $result.Errors | ForEach-Object { '{0}({1},{2}): {3}' -f $_.FileName, $_.Line, $_.Column, $_.ErrorText }
    throw ($messages -join [Environment]::NewLine)
}

if ($Production) {
    $signature = Set-AuthenticodeSignature -LiteralPath $parameters.OutputAssembly -Certificate $certificate -HashAlgorithm SHA256 -TimestampServer $TimestampServer
    if ($signature.Status -ne 'Valid') { throw ('Authenticode signing failed: ' + $signature.Status + ' ' + $signature.StatusMessage) }
}
if ($Release -or $Production) {
    $signature = Get-AuthenticodeSignature -LiteralPath $parameters.OutputAssembly
    $hash = Get-FileHash -LiteralPath $parameters.OutputAssembly -Algorithm SHA256
    [ordered]@{
        version = $version
        commit = $releaseCommit
        tag = [string]$releaseTag
        size = (Get-Item -LiteralPath $parameters.OutputAssembly).Length
        sha256 = $hash.Hash
        signature = $signature.Status.ToString()
        signerThumbprint = if ($certificate) { $certificate.Thumbprint } else { $null }
        builtUtc = [DateTime]::UtcNow.ToString('o')
    } | ConvertTo-Json | Set-Content -LiteralPath ($parameters.OutputAssembly + '.release.json') -Encoding UTF8
}

Write-Host ('Built ' + $parameters.OutputAssembly)
