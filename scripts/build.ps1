param(
    [string]$OutputPath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'CodexTokenMonitor.exe'),
    [switch]$Production,
    [string]$CodeSigningThumbprint = $env:CODE_SIGNING_THUMBPRINT,
    [string]$TimestampServer = 'http://timestamp.digicert.com'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$releaseCommit = $null
$releaseTag = $null
$certificate = $null
if ($Production) {
    $dirty = @(& git -C $projectRoot status --porcelain --untracked-files=all)
    if ($LASTEXITCODE -ne 0) { throw 'Unable to verify the Git working tree.' }
    if ($dirty.Count -ne 0) { throw 'Production build requires a clean Git working tree.' }
    $releaseCommit = (& git -C $projectRoot rev-parse HEAD).Trim()
    $releaseTag = (& git -C $projectRoot describe --tags --exact-match HEAD 2>$null)
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($releaseTag)) { throw 'Production build requires a release tag on HEAD.' }
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
$manifestPath = Join-Path $projectRoot 'resources\app.manifest'
$iconPath = Join-Path $projectRoot 'assets\codex-token-monitor.ico'
if (-not (Test-Path -LiteralPath $iconPath -PathType Leaf)) { throw 'Application icon was not found: ' + $iconPath }
$parameters.CompilerOptions = '/target:winexe /optimize+ /platform:anycpu /win32manifest:"' + $manifestPath + '" /win32icon:"' + $iconPath + '"'
@('System.dll', 'System.Core.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll') | ForEach-Object { [void]$parameters.ReferencedAssemblies.Add($_) }

if (Test-Path -LiteralPath $parameters.OutputAssembly) { Remove-Item -LiteralPath $parameters.OutputAssembly -Force }
$result = $provider.CompileAssemblyFromFile($parameters, [string[]]$sourceFiles)
if ($result.Errors.HasErrors) {
    $messages = $result.Errors | ForEach-Object { '{0}({1},{2}): {3}' -f $_.FileName, $_.Line, $_.Column, $_.ErrorText }
    throw ($messages -join [Environment]::NewLine)
}

if ($Production) {
    $signature = Set-AuthenticodeSignature -LiteralPath $parameters.OutputAssembly -Certificate $certificate -HashAlgorithm SHA256 -TimestampServer $TimestampServer
    if ($signature.Status -ne 'Valid') { throw ('Authenticode signing failed: ' + $signature.Status + ' ' + $signature.StatusMessage) }
    $hash = Get-FileHash -LiteralPath $parameters.OutputAssembly -Algorithm SHA256
    [ordered]@{
        commit = $releaseCommit
        tag = [string]$releaseTag
        size = (Get-Item -LiteralPath $parameters.OutputAssembly).Length
        sha256 = $hash.Hash
        signature = $signature.Status.ToString()
        signerThumbprint = $certificate.Thumbprint
        builtUtc = [DateTime]::UtcNow.ToString('o')
    } | ConvertTo-Json | Set-Content -LiteralPath ($parameters.OutputAssembly + '.release.json') -Encoding UTF8
}

Write-Host ('Built ' + $parameters.OutputAssembly)
