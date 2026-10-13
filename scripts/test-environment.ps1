function Enter-TestEnvironment {
    $root = Join-Path ([IO.Path]::GetTempPath()) ('CodexMonitor-Test-' + [Guid]::NewGuid().ToString('N'))
    $state = @{ Root = $root; DataHome = $env:CODEX_TOKEN_MONITOR_DATA_HOME; CodexHome = $env:CODEX_HOME }
    New-Item -ItemType Directory -Path (Join-Path $root 'codex-home\sessions') -Force | Out-Null
    $env:CODEX_TOKEN_MONITOR_DATA_HOME = Join-Path $root 'data'
    $env:CODEX_HOME = Join-Path $root 'codex-home'
    return $state
}

function Exit-TestEnvironment($state) {
    $env:CODEX_TOKEN_MONITOR_DATA_HOME = $state.DataHome
    $env:CODEX_HOME = $state.CodexHome
    $root = [IO.Path]::GetFullPath($state.Root)
    $temp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if (-not $root.StartsWith($temp, [StringComparison]::OrdinalIgnoreCase) -or
        -not ([IO.Path]::GetFileName($root)).StartsWith('CodexMonitor-Test-')) {
        throw 'Refusing to remove an unexpected test directory.'
    }
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}
