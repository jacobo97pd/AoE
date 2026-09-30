#!/usr/bin/env node
'use strict';

// Captures the existing #comparison section in Chrome. The source Unity renders
// are loaded without modification; this script only takes a browser screenshot.
// Requires Node 22+ and installed Chrome/Edge. Installs no dependencies.
//   node tools/art/capture_royal_soldier_comparison.cjs --check-only
//   node tools/art/capture_royal_soldier_comparison.cjs
//   node tools/art/capture_royal_soldier_comparison.cjs --browser <executable>
const fs = require('node:fs/promises');
const path = require('node:path');
const { spawn } = require('node:child_process');
const { pathToFileURL, fileURLToPath } = require('node:url');

const project = path.resolve(__dirname, '../..');
const gallery = path.join(project, 'Artifacts/ArtReview/royal-soldier');
const output = path.join(gallery, 'comparison.png');
const cacheRoot = path.resolve('D:/EmberfieldWorkingCache/AoE');
const options = { checkOnly: false, browser: '' };
const args = process.argv.slice(2);
for (let i = 0; i < args.length; i++) {
    if (args[i] === '--check-only') options.checkOnly = true;
    else if (args[i] === '--browser' && args[i + 1]) options.browser = args[++i];
    else throw new Error('Unknown or incomplete argument: ' + args[i]);
}

const delay = ms => new Promise(resolve => setTimeout(resolve, ms));
async function exists(file) { try { await fs.access(file); return true; } catch { return false; } }
async function waitFor(action, label, timeout = 30000) {
    const end = Date.now() + timeout;
    while (Date.now() < end) {
        const result = await action();
        if (result) return result;
        await delay(100);
    }
    throw new Error('Timed out: ' + label);
}

class DevTools {
    constructor(socket) {
        this.socket = socket; this.nextId = 1; this.pending = new Map();
        socket.addEventListener('message', event => {
            const reply = JSON.parse(event.data), request = this.pending.get(reply.id);
            if (!request) return;
            this.pending.delete(reply.id); clearTimeout(request.timer);
            if (reply.error) request.reject(new Error(JSON.stringify(reply.error)));
            else request.resolve(reply.result);
        });
        socket.addEventListener('close', () => {
            for (const request of this.pending.values()) {
                clearTimeout(request.timer); request.reject(new Error('Chrome connection closed.'));
            }
            this.pending.clear();
        });
    }
    static async connect(url) {
        const socket = new WebSocket(url);
        await new Promise((resolve, reject) => {
            socket.addEventListener('open', resolve, { once: true });
            socket.addEventListener('error', reject, { once: true });
        });
        return new DevTools(socket);
    }
    call(method, params = {}) {
        const id = this.nextId++;
        return new Promise((resolve, reject) => {
            const timer = setTimeout(() => {
                this.pending.delete(id); reject(new Error('DevTools timeout: ' + method));
            }, 30000);
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
    if (typeof WebSocket !== 'function') throw new Error('Node 22 or newer is required.');
    const index = path.join(gallery, 'index.html');
    if (!await exists(index)) throw new Error('Missing gallery: ' + index);
    const candidates = [options.browser, process.env.CHROME_PATH,
        'C:/Program Files/Google/Chrome/Application/chrome.exe',
        'C:/Program Files (x86)/Google/Chrome/Application/chrome.exe',
        'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'].filter(Boolean);
    let executable;
    for (const candidate of candidates) if (await exists(candidate)) { executable = candidate; break; }
    if (!executable) throw new Error('No Chrome/Edge found. Pass --browser <executable>.');

    // All browser cache is on D; never fall back to the nearly full C drive.
    await fs.mkdir(cacheRoot, { recursive: true });
    const profile = await fs.mkdtemp(path.join(cacheRoot, 'royal-soldier-comparison-'));
    const browser = spawn(executable, ['--headless=new', '--disable-gpu', '--no-first-run',
        '--no-default-browser-check', '--disable-background-networking', '--disable-extensions',
        '--allow-file-access-from-files', '--remote-debugging-address=127.0.0.1',
        '--remote-debugging-port=0', '--user-data-dir=' + profile, 'about:blank'],
    { windowsHide: true, stdio: 'ignore' });
    let launchError, client;
    browser.on('error', error => { launchError = error; });
    try {
        const portFile = path.join(profile, 'DevToolsActivePort');
        const port = await waitFor(async () => {
            if (launchError) throw launchError;
            if (browser.exitCode !== null) throw new Error('Chrome startup exit: ' + browser.exitCode);
            return await exists(portFile) ? (await fs.readFile(portFile, 'utf8')).split(/\r?\n/)[0] : false;
        }, 'Chrome startup');
        const response = await fetch('http://127.0.0.1:' + port + '/json/new?about:blank', { method: 'PUT' });
        if (!response.ok) throw new Error('Cannot open Chrome page: ' + response.status);
        client = await DevTools.connect((await response.json()).webSocketDebuggerUrl);
        await client.call('Page.enable'); await client.call('Runtime.enable');
        await client.call('Emulation.setDeviceMetricsOverride', {
            width: 1800, height: 1200, deviceScaleFactor: 1, mobile: false,
        });
        await client.call('Page.navigate', { url: pathToFileURL(index).href });
        await waitFor(() => client.evaluate("document.readyState === 'complete' && !!document.querySelector('#comparison')"), 'gallery section');
        await client.evaluate("document.querySelector('#comparison').scrollIntoView({behavior:'instant', block:'start'})");
        await waitFor(() => client.evaluate(`(() => {
            const images=[...document.querySelectorAll('#comparison img')];
            return images.length === 2 && images.every(img => img.complete && img.naturalWidth > 0 && img.naturalHeight > 0);
        })()`), 'both native comparison images');
        await client.evaluate('document.fonts.ready.then(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))))');

        const state = await client.evaluate(`(() => {
            const section=document.querySelector('#comparison'), rect=section.getBoundingClientRect();
            return {
                heading:section.querySelector('h2')?.textContent.trim(),
                captions:[...section.querySelectorAll('figcaption')].map(el=>el.textContent.trim()),
                images:[...section.querySelectorAll('img')].map(img=>({
                    url:img.currentSrc || img.src, width:img.naturalWidth, height:img.naturalHeight, alt:img.alt,
                    displayedWidth:img.getBoundingClientRect().width, displayedHeight:img.getBoundingClientRect().height,
                })),
                clip:{x:rect.left+scrollX,y:rect.top+scrollY,width:rect.width,height:rect.height,scale:1},
                scrollWidth:document.documentElement.scrollWidth,viewportWidth:innerWidth,
            };
        })()`);
        const expected = [path.join(gallery, '../kingdom-premium/warrior.png'), path.join(gallery, 'close.png')];
        for (let i = 0; i < expected.length; i++) {
            if (state.images[i].url !== pathToFileURL(expected[i]).href)
                throw new Error('Unexpected comparison source: ' + state.images[i].url);
            if (state.images[i].width < 500 || state.images[i].height < 500 || !state.images[i].alt)
                throw new Error('Missing full-size native image or accessible caption.');
            const source = await fs.stat(fileURLToPath(state.images[i].url));
            state.images[i].modifiedUtc = source.mtime.toISOString();
        }
        if (state.heading !== 'Antes y ahora' || state.captions.length !== 2 ||
            !state.captions[0].includes('anterior') || !state.captions[1].includes('nuevo'))
            throw new Error('Before/after labels are missing or ambiguous.');
        if (state.scrollWidth > state.viewportWidth || state.clip.x < 0 || state.clip.y < 0 ||
            state.clip.width < 1000 || state.clip.height < 650 || state.clip.height > 1800 ||
            state.images.some(img => img.displayedWidth < 350 || img.displayedHeight < 500))
            throw new Error('Invalid comparison layout: ' + JSON.stringify(state.clip));
        if (!options.checkOnly) {
            const screenshot = await client.call('Page.captureScreenshot', {
                format: 'png', fromSurface: true, captureBeyondViewport: true, clip: state.clip,
            });
            const bytes = Buffer.from(screenshot.data, 'base64');
            if (!bytes.subarray(0, 8).equals(Buffer.from([137,80,78,71,13,10,26,10])))
                throw new Error('Chrome did not return a PNG.');
            await fs.writeFile(output, bytes);
        }
        console.log(JSON.stringify({ passed: true, mode: options.checkOnly ? 'check-only' : 'capture',
            gallery: index, output: options.checkOnly ? null : output, ...state }, null, 2));
    } finally {
        if (client && client.socket.readyState === WebSocket.OPEN) {
            client.socket.send(JSON.stringify({ id: 999999, method: 'Browser.close' }));
            await delay(400); client.socket.close();
        }
        if (browser.exitCode === null) browser.kill();
        // Remove only this run's unique profile directly beneath the known D cache.
        const resolved = path.resolve(profile);
        if (path.dirname(resolved) !== cacheRoot || !path.basename(resolved).startsWith('royal-soldier-comparison-'))
            throw new Error('Refusing to remove unexpected Chrome profile: ' + resolved);
        await fs.rm(resolved, { recursive: true, force: true, maxRetries: 10, retryDelay: 250 })
            .catch(error => console.warn('Chrome profile cleanup: ' + error.message));
    }
}

main().catch(error => { console.error(error.stack || error); process.exitCode = 1; });
