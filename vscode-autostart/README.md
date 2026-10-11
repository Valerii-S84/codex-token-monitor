# Codex Token Monitor Autostart

This local-only VS Code extension starts `CodexTokenMonitor.exe` after VS Code finishes loading. The monitor remains single-instance even when several VS Code windows are opened.

The extension looks for `CodexTokenMonitor.exe` on the Desktop in `Codex Token Monitor`, `codex-token-monitor`, then the legacy `кодекс токен монітор` folder. It starts with VS Code, not with Windows. The extension runs locally even when VS Code opens a remote workspace.

If the executable is moved, set `codexTokenMonitor.executablePath` in VS Code settings.

Package locally with `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\vscode-autostart\package.ps1`. Install the resulting VSIX using `code --install-extension artifacts\codex-token-monitor-autostart-1.1.1.vsix --force`. Restart VS Code after updating an already active version.

## License

MIT. See [LICENSE](https://github.com/Valerii-S84/codex-token-monitor/blob/main/LICENSE). The packaged extension includes `LICENSE.txt`.
