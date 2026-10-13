param(
    [Parameter(Mandatory = $true)][string]$ExecutablePath,
    [Parameter(Mandatory = $true)][string]$ExpectedCommit,
    [string]$ExpectedVersion = '2.0.0'
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'test-environment.ps1')
$testEnvironment = Enter-TestEnvironment
try {
    $projectRoot = Split-Path -Parent $PSScriptRoot
    $executable = (Resolve-Path -LiteralPath $ExecutablePath).Path
    if ($ExpectedCommit -notmatch '^[a-f0-9]{40}$' -or $ExpectedVersion -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid expected release identity.' }
    $output = Join-Path $testEnvironment.Root 'ReleaseBinaryTest.exe'
    $provider = New-Object Microsoft.CSharp.CSharpCodeProvider
    $parameters = New-Object System.CodeDom.Compiler.CompilerParameters
    $parameters.GenerateExecutable = $true
    $parameters.OutputAssembly = $output
    $parameters.CompilerOptions = '/target:exe /win32manifest:"' + (Join-Path $projectRoot 'resources\app.manifest') + '"'
    @('System.dll', 'System.Core.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll') | ForEach-Object { [void]$parameters.ReferencedAssemblies.Add($_) }
    try { $result = $provider.CompileAssemblyFromFile($parameters, [string[]]@(Join-Path $projectRoot 'tests\ReleaseBinaryTest.cs')) }
    finally { $provider.Dispose() }
    if ($result.Errors.HasErrors) { throw (($result.Errors | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine) }
    & $output $executable $ExpectedCommit $ExpectedVersion
    if ($LASTEXITCODE -ne 0) { throw 'Final binary verification failed.' }
} finally { Exit-TestEnvironment $testEnvironment }
