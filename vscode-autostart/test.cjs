'use strict';
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const source = fs.readFileSync(path.join(__dirname, 'extension.js'), 'utf8');
const desktop = path.join('C:\\Users\\Test', 'Desktop', 'кодекс токен монітор');
function launch(configured, available, fail) {
    const state = { started: [], errors: [], unrefs: 0, handler: null };
    const mocks = {
        fs: { existsSync: file => available.includes(file) },
        os: { homedir: () => 'C:\\Users\\Test' }, path,
        vscode: { workspace: { getConfiguration: () => ({ get: () => configured }) } },
        child_process: { spawn: (file, args, options) => {
            state.started.push({ file, args, options });
            if (fail) throw new Error('blocked');
            return { once: (event, handler) => { assert.equal(event, 'error'); state.handler = handler; },
                unref: () => { state.unrefs++; } };
        } }
    };
    const context = { module: { exports: {} }, require: name => mocks[name],
        console: { warn: message => state.errors.push(message), error: message => state.errors.push(message) } };
    vm.runInNewContext(source, context, { filename: 'extension.js' });
    context.module.exports.activate();
    return state;
}
const spectrum = path.join(desktop, 'CodexTokenMonitor.Spectrum.exe');
const original = path.join(desktop, 'CodexTokenMonitor.exe');
let result = launch('', [spectrum, original]);
assert.equal(result.started[0].file, original);
assert.equal(result.started[0].options.detached, true);
assert.equal(result.started[0].options.cwd, desktop);
assert.equal(result.unrefs, 1);
result.handler(new Error('ENOENT'));
assert.equal(result.errors.length, 1);
result = launch('', [original]);
assert.equal(result.started[0].file, original);
const custom = path.resolve('C:\\Custom\\Monitor.exe');
result = launch(custom, [custom, spectrum]);
assert.equal(result.started[0].file, custom);
result = launch('~/Monitor.exe', [path.join('C:\\Users\\Test', 'Monitor.exe')]);
assert.equal(result.started[0].file, path.join('C:\\Users\\Test', 'Monitor.exe'));
result = launch('', []);
assert.equal(result.started.length, 0);
assert.equal(result.errors.length, 1);
result = launch('', [spectrum], true);
assert.equal(result.errors.length, 1);
const renamedDirectory = path.join(path.dirname(desktop), 'Codex Token Monitor');
const renamed = path.join(renamedDirectory, 'CodexTokenMonitor.exe');
result = launch('', [renamed]);
assert.equal(result.started[0].file, renamed);
assert.equal(result.started[0].options.cwd, renamedDirectory);
const cloned = path.join(path.dirname(desktop), 'codex-token-monitor', 'CodexTokenMonitor.exe');
result = launch('', [cloned]);
assert.equal(result.started[0].file, cloned);
result = launch('', [renamed, cloned, original]);
assert.equal(result.started[0].file, renamed);
result = launch(custom, [custom, renamed, cloned, original]);
assert.equal(result.started[0].file, custom);
result = launch(custom, [renamed]);
assert.equal(result.started.length, 0);
assert.equal(result.errors.length, 1);
console.log('AUTOSTART_TESTS_OK cases=12');
