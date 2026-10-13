$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'test-environment.ps1')
$testEnvironment = Enter-TestEnvironment
try {
$projectRoot = Split-Path -Parent $PSScriptRoot
$sources = @((Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' -File | Select-Object -ExpandProperty FullName))
$sources += (Join-Path $projectRoot 'tests\MultiAgentRegressionTest.cs')
$output = Join-Path $testEnvironment.Root 'CodexTokenMonitor.MultiAgentRegressionTest.exe'
$provider = New-Object Microsoft.CSharp.CSharpCodeProvider
$parameters = New-Object System.CodeDom.Compiler.CompilerParameters
$parameters.GenerateExecutable = $true
$parameters.GenerateInMemory = $false
$parameters.OutputAssembly = $output
$parameters.CompilerOptions = '/target:exe /optimize+ /main:CodexTokenMonitor.MultiAgentRegressionTest'
@('System.dll', 'System.Core.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll') | ForEach-Object { [void]$parameters.ReferencedAssemblies.Add($_) }
if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output -Force }
$result = $provider.CompileAssemblyFromFile($parameters, [string[]]$sources)
if ($result.Errors.HasErrors) { throw (($result.Errors | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine) }
& $output
if ($LASTEXITCODE -ne 0) { throw "Multi-agent regression failed with exit code $LASTEXITCODE" }
} finally { Exit-TestEnvironment $testEnvironment }
