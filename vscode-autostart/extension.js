'use strict';

const childProcess = require('child_process');
const fs = require('fs');
const os = require('os');
const path = require('path');
const vscode = require('vscode');

function monitorPath() {
    const configured = vscode.workspace
        .getConfiguration('codexTokenMonitor')
        .get('executablePath', '')
        .trim();

    if (configured) {
        return path.resolve(configured.replace(/^~(?=$|[\\/])/, os.homedir()));
    }

    const desktop = path.join(os.homedir(), 'Desktop');
    const directories = ['Codex Token Monitor', 'codex-token-monitor', 'кодекс токен монітор'];
    for (const directory of directories) {
        const executable = path.join(desktop, directory, 'CodexTokenMonitor.exe');
        if (fs.existsSync(executable)) return executable;
    }
    return path.join(desktop, directories[0], 'CodexTokenMonitor.exe');
}

function activate() {
    const executable = monitorPath();
    if (!fs.existsSync(executable)) {
        console.warn(`[Codex Token Monitor] Executable not found: ${executable}`);
        return;
    }

    try {
        const monitor = childProcess.spawn(executable, [], {
            cwd: path.dirname(executable),
            detached: true,
            stdio: 'ignore',
            windowsHide: false
        });
        // Spawn failures are asynchronous; try/catch alone cannot handle them.
        monitor.once('error', error => {
            console.error('[Codex Token Monitor] Could not start the monitor.', error);
        });
        monitor.unref();
    } catch (error) {
        console.error('[Codex Token Monitor] Could not start the monitor.', error);
    }
}

function deactivate() {}

module.exports = { activate, deactivate };
