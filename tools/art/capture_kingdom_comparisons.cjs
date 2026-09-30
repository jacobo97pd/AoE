#!/usr/bin/env node
'use strict';

// Native Chrome screenshots of the existing HTML comparison section. No image
// generation, compositing or model edits. Requires Node 22+ and installed Chrome.
//   node tools/art/capture_kingdom_comparisons.cjs --check-only
//   node tools/art/capture_kingdom_comparisons.cjs --source editor
//   node tools/art/capture_kingdom_comparisons.cjs --source player --outdir <path>
const fs = require('node:fs/promises');
const path = require('node:path');
const os = require('node:os');
const { spawn } = require('node:child_process');
const { pathToFileURL } = require('node:url');

const project = path.resolve(__dirname, '../..');
const gallery = path.join(project, 'Artifacts/ArtReview/kingdom-premium');
const args = process.argv.slice(2);
const options = { source: 'editor', outdir: gallery, checkOnly: false, browser: '' };
for (let i = 0; i < args.length; i++) {
    if (args[i] === '--check-only') options.checkOnly = true;
    else if (['--source', '--outdir', '--browser'].includes(args[i]) && args[i + 1]) options[args[i].slice(2)] = args[++i];
    else throw new Error('Unknown or incomplete argument: ' + args[i]);
}
if (!['editor', 'player'].includes(options.source)) throw new Error('--source must be editor or player.');
options.outdir = path.resolve(options.outdir);

const delay = milliseconds => new Promise(resolve => setTimeout(resolve, milliseconds));
async function exists(file) { try { await fs.access(file); return true; } catch { return false; } }
async function waitFor(action, label, timeout = 30000) {
    const end = Date.now() + timeout;
    while (Date.now() < end) { const result = await action(); if (result) return result; await delay(100); }
    throw new Error('Timed out: ' + label);
}

class DevTools {
    constructor(socket) {
        this.socket = socket; this.nextId = 1; this.pending = new Map();
        socket.addEventListener('message', event => {
            const reply = JSON.parse(event.data);
            const request = this.pending.get(reply.id);
            if (!request) return;
            this.pending.delete(reply.id); clearTimeout(request.timer);
            if (reply.error) request.reject(new Error(JSON.stringify(reply.error)));
            else request.resolve(reply.result);
        });
        socket.addEventListener('close', () => {
            for (const request of this.pending.values()) { clearTimeout(request.timer); request.reject(new Error('Chrome connection closed.')); }
            this.pending.clear();
        });
    }
    static async connect(url) {
        const socket = new WebSocket(url);
        await new Promise((resolve, reject) => { socket.addEventListener('open', resolve, { once: true }); socket.addEventListener('error', reject, { once: true }); });
        return new DevTools(socket);
    }
    call(method, params = {}) {
        const id = this.nextId++;
        return new Promise((resolve, reject) => {
            const timer = setTimeout(() => { this.pending.delete(id); reject(new Error('DevTools timeout: ' + method)); }, 30000);
            this.pending.set(id, { resolve, reject, timer });
            this.socket.send(JSON.stringify({ id, method, params }));
        });
    }
    async evaluate(expression) {
        const response = await this.call('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true });
        if (response.exceptionDetails) throw new Error(JSON.stringify(response.exceptionDetails));
        return response.result.value;
    }
}

async function main() {
    const candidates = [options.browser, process.env.CHROME_PATH,
        'C:/Program Files/Google/Chrome/Application/chrome.exe',
        'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
        path.join(os.homedir(), 'AppData/Local/ms-playwright/chromium-1228/chrome-win64/chrome.exe')].filter(Boolean);
    let executable;
    for (const candidate of candidates) if (await exists(candidate)) { executable = candidate; break; }
    if (!executable) throw new Error('No installed Chrome found. Supply --browser <executable>; this script installs nothing.');
    if (!await exists(path.join(gallery, 'index.html'))) throw new Error('Gallery index.html is missing.');
    const tempRoot = path.resolve(await exists('D:/EmberfieldWorkingCache/AoE') ? 'D:/EmberfieldWorkingCache/AoE' : os.tmpdir());
    const profile = await fs.mkdtemp(path.join(tempRoot, 'kingdom-comparisons-'));
    const browser = spawn(executable, ['--headless=new', '--disable-gpu', '--no-first-run', '--no-default-browser-check',
        '--disable-background-networking', '--disable-extensions', '--allow-file-access-from-files',
        '--remote-debugging-port=0', '--user-data-dir=' + profile, 'about:blank'], { windowsHide: true, stdio: 'ignore' });
    let launchError, client;
    browser.on('error', error => { launchError = error; });
    const report = { mode: options.checkOnly ? 'check-only' : 'capture', source: options.source, gallery, browser: executable, items: [] };
    try {
        const portFile = path.join(profile, 'DevToolsActivePort');
        const port = await waitFor(async () => {
            if (launchError) throw launchError;
            if (browser.exitCode !== null) throw new Error('Chrome exited during startup: ' + browser.exitCode);
            return await exists(portFile) ? (await fs.readFile(portFile, 'utf8')).split(/\r?\n/)[0] : false;
        }, 'Chrome startup');
        const targetResponse = await fetch('http://127.0.0.1:' + port + '/json/new?about:blank', { method: 'PUT' });
        if (!targetResponse.ok) throw new Error('Cannot open Chrome target: ' + targetResponse.status);
        const target = await targetResponse.json();
        client = await DevTools.connect(target.webSocketDebuggerUrl);
        await client.call('Page.enable');
        await client.call('Runtime.enable');
        await client.call('Emulation.setDeviceMetricsOverride', { width: 1800, height: 1200, deviceScaleFactor: 1, mobile: false });
        await client.call('Page.navigate', { url: pathToFileURL(path.join(gallery, 'index.html')).href });
        await waitFor(() => client.evaluate("document.readyState === 'complete' && !!document.querySelector('#character-tabs button')"), 'gallery startup');
        await client.evaluate(`document.querySelector('[data-source="${options.source}"]').click()`);
        if (!options.checkOnly) await fs.mkdir(options.outdir, { recursive: true });

        for (const [kind, label] of [['worker', 'Trabajador'], ['warrior', 'Guerrero'], ['hero', 'Héroe']]) {
            await client.evaluate(`document.querySelector('[data-character="${kind}"]').click(); document.querySelector('.comparison').scrollIntoView();`);
            await waitFor(() => client.evaluate("['reference-image','previous-image','current-image'].every(id => { const image=document.getElementById(id); return image.complete && image.naturalWidth>0 && !image.parentElement.classList.contains('unavailable'); })"), 'three loaded images for ' + kind);
            await client.evaluate('document.fonts.ready.then(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))))');
            const state = await client.evaluate(`(() => {
                const section=document.querySelector('.comparison'), rect=section.getBoundingClientRect();
                return { selected:document.querySelector('#character-tabs button[aria-pressed="true"]').dataset.character,
                    source:document.querySelector('#source-tabs button[aria-pressed="true"]').dataset.source,
                    currentCaption:document.getElementById('current-caption').textContent,
                    headings:[...section.querySelectorAll('figcaption strong')].map(node=>node.textContent),
                    images:['reference-image','previous-image','current-image'].map(id=>{const image=document.getElementById(id);return {id,url:image.src,width:image.naturalWidth,height:image.naturalHeight};}),
                    clip:{x:rect.left+scrollX,y:rect.top+scrollY,width:rect.width,height:rect.height,scale:1},
                    scrollWidth:document.documentElement.scrollWidth,viewportWidth:innerWidth,viewportContentWidth:document.documentElement.clientWidth };
            })()`);
            const expected = pathToFileURL(path.join(gallery, options.source === 'player' ? 'player' : '', kind + '.png')).href;
            if (state.selected !== kind || state.source !== options.source || state.images[2].url !== expected) throw new Error('Gallery source mismatch for ' + kind);
            if (state.headings.join('|') !== 'Referencia de diseño|Versión anterior|Nueva escena') throw new Error('Comparison labels missing.');
            if (state.scrollWidth > state.viewportWidth || Math.abs(state.clip.width - state.viewportContentWidth) > 1 || state.clip.width < 1700 || state.clip.height < 300) throw new Error('Comparison section has an invalid layout: ' + JSON.stringify(state.clip));
            const filename = 'comparison-' + kind + '.png';
            if (!options.checkOnly) {
                const screenshot = await client.call('Page.captureScreenshot', { format: 'png', fromSurface: true, captureBeyondViewport: true, clip: state.clip });
                const bytes = Buffer.from(screenshot.data, 'base64');
                if (bytes.subarray(1, 4).toString() !== 'PNG') throw new Error('Chrome did not return PNG data.');
                await fs.writeFile(path.join(options.outdir, filename), bytes);
            }
            report.items.push({ kind, label, filename: options.checkOnly ? null : filename, ...state });
        }
        report.passed = true;
        console.log(JSON.stringify(report, null, 2));
    } finally {
        if (client && client.socket.readyState === WebSocket.OPEN) {
            client.socket.send(JSON.stringify({ id: 999999, method: 'Browser.close' }));
            await delay(400);
            client.socket.close();
        }
        if (browser.exitCode === null) browser.kill();
        // Delete only the uniquely created profile beneath the resolved cache root.
        const resolvedProfile = path.resolve(profile);
        if (path.dirname(resolvedProfile) === tempRoot && path.basename(resolvedProfile).startsWith('kingdom-comparisons-')) {
            await fs.rm(resolvedProfile, { recursive: true, force: true, maxRetries: 10, retryDelay: 250 }).catch(error => console.warn('Chrome cache cleanup: ' + error.message));
        }
    }
}

main().catch(error => { console.error(error.stack || error); process.exitCode = 1; });
