$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'test-environment.ps1')
$testEnvironment = Enter-TestEnvironment
try {
    $projectRoot = Split-Path -Parent $PSScriptRoot
    $sources = @((Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' -File | Select-Object -ExpandProperty FullName))
    # UI translation literals must live in the compiled catalogs, including painted graph text.
    foreach ($source in $sources) {
        if ([IO.Path]::GetFileName($source).StartsWith('Localization')) { continue }
        if ([IO.File]::ReadAllText($source) -match '[\u0410-\u044f\u0406\u0456\u0407\u0457\u0404\u0454\u0490\u0491]') { throw "Uncatalogued Ukrainian text: $source" }
    }
    $sources += (Join-Path $projectRoot 'tests\LocalizationTest.cs')
    $output = Join-Path $testEnvironment.Root 'CodexTokenMonitor.LocalizationTest.exe'
    $provider = New-Object Microsoft.CSharp.CSharpCodeProvider
    $parameters = New-Object System.CodeDom.Compiler.CompilerParameters
    $parameters.GenerateExecutable = $true
    $parameters.OutputAssembly = $output
    $parameters.CompilerOptions = '/target:exe /optimize+ /main:CodexTokenMonitor.LocalizationTest'
    @('System.dll', 'System.Core.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll') | ForEach-Object { [void]$parameters.ReferencedAssemblies.Add($_) }
    $result = $provider.CompileAssemblyFromFile($parameters, [string[]]$sources)
    if ($result.Errors.HasErrors) { throw (($result.Errors | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine) }
    & $output
    if ($LASTEXITCODE -ne 0) { throw "Localization gate failed with exit code $LASTEXITCODE" }
} finally { Exit-TestEnvironment $testEnvironment }
