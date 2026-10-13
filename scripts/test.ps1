$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'test-environment.ps1')
$testEnvironment = Enter-TestEnvironment
try {
$projectRoot = Split-Path -Parent $PSScriptRoot
$sources = @((Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' -File | Select-Object -ExpandProperty FullName))
$sources += (Join-Path $projectRoot 'tests\SmokeTest.cs')
$sources += (Join-Path $projectRoot 'tests\AnalyticsRegressionTest.cs')
$output = Join-Path $testEnvironment.Root 'CodexTokenMonitor.SmokeTest.exe'
$provider = New-Object Microsoft.CSharp.CSharpCodeProvider
$parameters = New-Object System.CodeDom.Compiler.CompilerParameters
$parameters.GenerateExecutable = $true
$parameters.GenerateInMemory = $false
$parameters.OutputAssembly = $output
$parameters.CompilerOptions = '/target:exe /optimize+ /main:CodexTokenMonitor.SmokeTest'
@('System.dll', 'System.Core.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll') | ForEach-Object { [void]$parameters.ReferencedAssemblies.Add($_) }
if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output -Force }
$result = $provider.CompileAssemblyFromFile($parameters, [string[]]$sources)
if ($result.Errors.HasErrors) { throw (($result.Errors | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine) }
& $output
if ($LASTEXITCODE -ne 0) { throw "Smoke test failed with exit code $LASTEXITCODE" }
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'crash-test.ps1')
if ($LASTEXITCODE -ne 0) { throw "Crash gate failed with exit code $LASTEXITCODE" }
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'dashboard-test.ps1')
if ($LASTEXITCODE -ne 0) { throw "Dashboard gate failed with exit code $LASTEXITCODE" }
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'multi-agent-test.ps1')
if ($LASTEXITCODE -ne 0) { throw "Multi-agent gate failed with exit code $LASTEXITCODE" }
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'localization-test.ps1')
if ($LASTEXITCODE -ne 0) { throw "Localization gate failed with exit code $LASTEXITCODE" }
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'render-test.ps1')
if ($LASTEXITCODE -ne 0) { throw "Render gate failed with exit code $LASTEXITCODE" }
& node (Join-Path $projectRoot 'vscode-autostart\test.cjs')
if ($LASTEXITCODE -ne 0) { throw "Autostart gate failed with exit code $LASTEXITCODE" }
Write-Output 'RELEASE_GATE_OK'
} finally { Exit-TestEnvironment $testEnvironment }
