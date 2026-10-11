$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$sources = @((Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' -File | Select-Object -ExpandProperty FullName))
$sources += (Join-Path $projectRoot 'tests\RenderTest.cs')
$sources += (Join-Path $projectRoot 'tests\SpectrumEffectsTest.cs')
$sources += (Join-Path $projectRoot 'tests\ProductViewTest.cs')
$output = Join-Path $env:TEMP 'CodexTokenMonitor.RenderTest.exe'
$renderDir = Join-Path $projectRoot 'artifacts\renders'
$provider = New-Object Microsoft.CSharp.CSharpCodeProvider
$parameters = New-Object System.CodeDom.Compiler.CompilerParameters
$parameters.GenerateExecutable = $true
$parameters.GenerateInMemory = $false
$parameters.OutputAssembly = $output
$parameters.CompilerOptions = '/target:exe /optimize+ /main:CodexTokenMonitor.RenderTest /win32manifest:"' + (Join-Path $projectRoot 'resources\app.manifest') + '"'
@('System.dll', 'System.Core.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll') | ForEach-Object { [void]$parameters.ReferencedAssemblies.Add($_) }
if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output -Force }
$result = $provider.CompileAssemblyFromFile($parameters, [string[]]$sources)
if ($result.Errors.HasErrors) { throw (($result.Errors | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine) }
& $output $renderDir
if ($LASTEXITCODE -ne 0) { throw "Render test failed with exit code $LASTEXITCODE" }
