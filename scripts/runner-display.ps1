param([switch]$Prepare)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$output = Join-Path $env:TEMP ('CodexDisplayProbe-' + [Guid]::NewGuid().ToString('N') + '.exe')
$provider = New-Object Microsoft.CSharp.CSharpCodeProvider
$parameters = New-Object System.CodeDom.Compiler.CompilerParameters
$parameters.GenerateExecutable = $true
$parameters.OutputAssembly = $output
$parameters.CompilerOptions = '/target:exe /win32manifest:"' + (Join-Path $projectRoot 'resources\app.manifest') + '"'
@('System.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll') | ForEach-Object { [void]$parameters.ReferencedAssemblies.Add($_) }
try {
    $result = $provider.CompileAssemblyFromFile($parameters, [string[]]@(Join-Path $projectRoot 'tests\RunnerDisplayProbe.cs'))
    if ($result.Errors.HasErrors) { throw (($result.Errors | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine) }
    if ($Prepare) {
        & $output --prepare
        if ($LASTEXITCODE -ne 0) { throw 'Runner display preparation failed.' }
    }
    & $output
    if ($LASTEXITCODE -ne 0) { throw 'Display probe failed.' }
} finally {
    $provider.Dispose()
    if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output }
}
