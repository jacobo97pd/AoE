import { spawn } from 'node:child_process';
import { readFile, writeFile, mkdir, stat } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import { dirname, resolve, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

// Offline gallery interaction checks. These do not approve art or measure Unity/mobile performance.
const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const pagePath = join(root, 'Artifacts', 'ArtReview', 'overhaul', 'index.html');
const resultDirectory = join(root, 'TestResults');
const profile = 'D:/EmberfieldWorkingCache/AoE/overhaul-gallery-browser-' + Date.now();
const screenshots = !process.argv.includes('--no-screenshots');
const catalogue = {
  kingdom: ['worker', 'warrior', 'cavalry', 'hero', 'mountainwarrior'],
  caribbean: ['worker', 'pirate', 'hero', 'warrior'],
  desert: ['worker', 'warrior', 'cavalry', 'hero'],
  fantasy: ['elf', 'dwarf', 'dragon', 'hero'],
};
const report = {
  schema: 1, page: pagePath, startedUtc: new Date().toISOString(),
  scope: 'Offline browser loading and native input interaction for 12 environment views and 17 portraits. No artistic acceptance, gameplay or device-performance claim.',
  checks: [], images: [], viewports: [], screenshots: [], errors: [], browserErrors: [], networkRequests: [], passed: false,
};
const pause = ms => new Promise(resolve => setTimeout(resolve, ms));
const hash = bytes => createHash('sha256').update(bytes).digest('hex');
let browser, socket, call, evaluate, startupError;

async function check(name, operation) {
  const result = typeof operation === 'string' ? await evaluate(operation) : await operation();
  const passed = typeof result === 'object' && result !== null && 'passed' in result ? result.passed : Boolean(result);
  report.checks.push({ name, passed, ...(typeof result === 'object' && result !== null ? { details: result } : {}) });
  if (!passed) { const message = 'Gallery check failed: ' + name; report.errors.push(message); throw new Error(message); }
  return result;
}
async function frame() { await evaluate('new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)))'); }
async function click(selector) {
  const point = await evaluate(`(() => {
    const node=document.querySelector(${JSON.stringify(selector)});
    if(!node)throw new Error('Missing clickable element: '+${JSON.stringify(selector)});
    node.scrollIntoView({block:'center',inline:'center',behavior:'instant'});
    const r=node.getBoundingClientRect();if(r.width<=0||r.height<=0)throw new Error('Invisible clickable element');
    return {x:r.left+r.width/2,y:r.top+r.height/2};
  })()`);
  await call('Input.dispatchMouseEvent', { type: 'mouseMoved', ...point });
  await call('Input.dispatchMouseEvent', { type: 'mousePressed', ...point, button: 'left', clickCount: 1 });
  await call('Input.dispatchMouseEvent', { type: 'mouseReleased', ...point, button: 'left', clickCount: 1 });
  await frame();
}
async function key(name, keyCode) {
  await call('Input.dispatchKeyEvent', { type: 'keyDown', key: name, code: name, windowsVirtualKeyCode: keyCode, nativeVirtualKeyCode: keyCode });
  await call('Input.dispatchKeyEvent', { type: 'keyUp', key: name, code: name, windowsVirtualKeyCode: keyCode, nativeVirtualKeyCode: keyCode });
  await frame();
}
async function decoded(selector, source, width, height) {
  return evaluate(`(async()=>{
    const i=document.querySelector(${JSON.stringify(selector)});if(!i)return {passed:false,missing:true};
    i.loading='eager';await i.decode();return {source:i.getAttribute('src'),width:i.naturalWidth,height:i.naturalHeight,
      passed:i.getAttribute('src')===${JSON.stringify(source)}&&i.naturalWidth===${width}&&i.naturalHeight===${height}};
  })()`);
}

try {
  await mkdir(resultDirectory, { recursive: true });
  report.pageSha256 = hash(await readFile(pagePath));
  await mkdir(profile, { recursive: true });
  browser = spawn('C:/Program Files/Google/Chrome/Application/chrome.exe', [
    '--headless=new', '--remote-debugging-port=0', '--no-first-run', '--disable-extensions',
    '--disable-background-networking', '--user-data-dir=' + profile, 'about:blank',
  ], { windowsHide: true, stdio: 'ignore' });
  browser.once('error', error => { startupError = error; });
  let port;
  for (let i = 0; i < 160; i++) {
    if (startupError) throw startupError;
    try { port = (await readFile(profile + '/DevToolsActivePort', 'utf8')).split('\n')[0].trim(); break; }
    catch { await pause(100); }
  }
  if (!port) throw new Error('Chrome did not expose its isolated debugging port.');
  const targets = await (await fetch('http://127.0.0.1:' + port + '/json/list')).json();
  const target = targets.find(item => item.type === 'page');
  if (!target) throw new Error('Chrome has no page target.');
  socket = new WebSocket(target.webSocketDebuggerUrl);
  await new Promise((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error('Chrome WebSocket timeout.')), 10000);
    socket.onopen = () => { clearTimeout(timer); resolve(); };
    socket.onerror = () => { clearTimeout(timer); reject(new Error('Chrome WebSocket failed.')); };
  });
  let nextId = 0;
  const pending = new Map();
  socket.onmessage = event => {
    const message = JSON.parse(event.data);
    if (message.method === 'Runtime.exceptionThrown') {
      const detail = message.params.exceptionDetails;report.browserErrors.push(detail.exception?.description || detail.text);
    }
    if (message.method === 'Network.requestWillBeSent') report.networkRequests.push(message.params.request.url);
    if (!message.id || !pending.has(message.id)) return;
    const { resolve, reject, timer } = pending.get(message.id);pending.delete(message.id);clearTimeout(timer);
    if (message.error) reject(new Error(JSON.stringify(message.error)));else resolve(message.result);
  };
  socket.onclose = () => {
    for (const { reject, timer } of pending.values()) { clearTimeout(timer);reject(new Error('Chrome connection closed.')); }
    pending.clear();
  };
  call = (method, params = {}) => new Promise((resolve, reject) => {
    const id = ++nextId;const timer = setTimeout(() => { pending.delete(id);reject(new Error('CDP timeout: ' + method)); }, 12000);
    pending.set(id, { resolve, reject, timer });socket.send(JSON.stringify({ id, method, params }));
  });
  evaluate = async expression => {
    const result = await call('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true });
    if (result.exceptionDetails) throw new Error(result.exceptionDetails.exception?.description || result.exceptionDetails.text);
    return result.result.value;
  };
  await call('Runtime.enable');await call('Page.enable');await call('Network.enable');
  report.browser = await call('Browser.getVersion');
  await call('Network.emulateNetworkConditions', { offline: true, latency: 0, downloadThroughput: 0, uploadThroughput: 0 });
  await call('Emulation.setDeviceMetricsOverride', { width: 1440, height: 1000, deviceScaleFactor: 1, mobile: false });
  await call('Page.navigate', { url: pathToFileURL(pagePath).href });
  let ready = false;
  for (let i = 0; i < 100; i++) {
    ready = await evaluate("document.readyState==='complete'&&document.querySelectorAll('#portraits .portrait').length===5");
    if (ready) break;await pause(100);
  }
  await check('Gallery initializes from a local file with networking disabled', () => ready);

  for (const [biome, units] of Object.entries(catalogue)) {
    await click(`[data-biome="${biome}"]`);
    await check('Biome button selects ' + biome, `document.querySelector('[data-biome="${biome}"]').getAttribute('aria-pressed')==='true'&&document.querySelectorAll('#biomes [aria-pressed="true"]').length===1&&document.querySelectorAll('#portraits .portrait').length===${units.length}`);
    await check('All portrait thumbnails decode for ' + biome, `(async()=>{
      const images=[...document.querySelectorAll('#portraits img')];images.forEach(i=>i.loading='eager');await Promise.all(images.map(i=>i.decode()));
      return images.length===${units.length}&&images.every(i=>i.naturalWidth===1000&&i.naturalHeight===1200);
    })()`);
    for (const distance of ['close', 'medium', 'rts']) {
      await click(`[data-distance="${distance}"]`);
      const source = `${biome}-${distance}.png`;
      const image = await check('Native camera button loads ' + source, () => decoded('#scene', source, 1920, 1080));
      report.images.push({ category: 'environment', biome, distance, ...image });
      await check('Camera pressed state is unique for ' + source, `document.querySelector('[data-distance="${distance}"]').getAttribute('aria-pressed')==='true'&&document.querySelectorAll('#distances [aria-pressed="true"]').length===1`);
      await click('#open-scene');
      await check('Scene enlargement loads ' + source, () => decoded('#viewer-image', source, 1920, 1080));
      await check('Scene enlargement reports its three-view context', "document.getElementById('viewer').open&&document.getElementById('viewer-count').textContent.endsWith(' / 3')");
      await key('Escape', 27);
    }
    for (let i = 0; i < units.length; i++) {
      const source = `${biome}-${units[i]}.png`;
      await click(`#portraits .portrait:nth-child(${i + 1})`);
      const image = await check('Native portrait button opens ' + source, () => decoded('#viewer-image', source, 1000, 1200));
      report.images.push({ category: 'portrait', biome, unit: units[i], ...image });
      await check('Portrait modal reports index for ' + source, `document.getElementById('viewer').open&&document.getElementById('viewer-count').textContent===${JSON.stringify((i + 1) + ' / ' + units.length)}`);
      await click('#close-viewer');
    }
  }
  await check('All 12 environment views and 17 portraits load exactly once', () => ({
    passed: report.images.length === 29 && new Set(report.images.map(i => i.source)).size === 29 && report.images.every(i => i.passed),
    environments: report.images.filter(i => i.category === 'environment').length,
    portraits: report.images.filter(i => i.category === 'portrait').length,
  }));

  await click('[data-biome="kingdom"]');await click('[data-distance="close"]');await click('#open-scene');
  await click('#next');await check('Viewer next button advances scene', () => decoded('#viewer-image', 'kingdom-medium.png', 1920, 1080));
  await click('#previous');await check('Viewer previous button restores scene', () => decoded('#viewer-image', 'kingdom-close.png', 1920, 1080));
  await key('ArrowLeft', 37);await check('Viewer left arrow wraps to last scene', () => decoded('#viewer-image', 'kingdom-rts.png', 1920, 1080));
  await key('ArrowRight', 39);await check('Viewer right arrow wraps to first scene', () => decoded('#viewer-image', 'kingdom-close.png', 1920, 1080));
  await key('Escape', 27);
  await check('Escape closes viewer and restores body scrolling', "!document.getElementById('viewer').open&&document.body.style.overflow===''");
  await click('#portraits .portrait:first-child');await key('ArrowLeft', 37);
  await check('Portrait viewer wraps within its five-portrait group', () => decoded('#viewer-image', 'kingdom-mountainwarrior.png', 1000, 1200));
  await key('ArrowRight', 39);await check('Portrait viewer keyboard returns to worker', () => decoded('#viewer-image', 'kingdom-worker.png', 1000, 1200));
  await click('#close-viewer');
  await check('Close button dismisses viewer', "!document.getElementById('viewer').open");

  const links = await evaluate("[...document.querySelectorAll('header a,footer a')].map(a=>({title:a.textContent,url:a.href}))");
  await check('Comparison links target an existing local capture', async () => {
    const checks = [];
    for (const link of links) {
      const url = new URL(link.url);let passed = false;
      if (url.protocol === 'file:') { try { passed = (await stat(fileURLToPath(url))).isFile(); } catch {} }
      checks.push({ ...link, passed });
    }
    return { passed: checks.length === 2 && checks.every(x => x.passed), links: checks };
  });

  for (const viewport of [{ name: 'desktop', width: 1440, height: 1000, mobile: false }, { name: 'phone', width: 390, height: 844, mobile: true }]) {
    await call('Emulation.setDeviceMetricsOverride', { width: viewport.width, height: viewport.height, deviceScaleFactor: 1, mobile: viewport.mobile });
    for (const biome of Object.keys(catalogue)) {
      await click(`[data-biome="${biome}"]`);await click('[data-distance="rts"]');
      const layout = await check(viewport.name + ' ' + biome + ' has no horizontal overflow', `(() => {
        const width=document.documentElement.clientWidth,scroll=document.documentElement.scrollWidth;
        const nodes=[...document.querySelectorAll('header,.controls,.main-frame,.main-caption,.troops,footer')];
        const outside=nodes.filter(n=>{const r=n.getBoundingClientRect();return r.left<-.5||r.right>innerWidth+.5}).map(n=>n.className||n.tagName);
        return {passed:scroll<=innerWidth+1&&Math.abs(innerWidth-${viewport.width})<=1&&outside.length===0,width,innerWidth,scrollWidth:scroll,outside};
      })()`);
      report.viewports.push({ ...viewport, biome, ...layout });
      await click('#open-scene');
      await check(viewport.name + ' ' + biome + ' lightbox fits viewport', `(() => {
        const modal=document.getElementById('viewer'),r=modal.getBoundingClientRect();
        return {passed:modal.open&&r.left>=-1&&r.right<=innerWidth+1&&r.top>=-1&&r.bottom<=innerHeight+1&&modal.scrollWidth<=modal.clientWidth+1,width:r.width,height:r.height};
      })()`);
      await key('Escape', 27);
    }
    await click('[data-biome="kingdom"]');
    await evaluate("window.scrollTo({top:0,left:0,behavior:'instant'})");await frame();
    await evaluate("(async()=>{const images=[...document.images].filter(i=>i.getAttribute('src')&&i.getBoundingClientRect().top<innerHeight);images.forEach(i=>i.loading='eager');await Promise.all(images.map(i=>i.decode()));})()");await frame();
    await check(viewport.name + ' portrait images preserve their visible 5:6 aspect ratio', `(() => {
      const images=[...document.querySelectorAll('#portraits img')].map(i=>{const r=i.getBoundingClientRect();return {source:i.getAttribute('src'),width:r.width,height:r.height,ratio:r.height/r.width,passed:Math.abs(r.height/r.width-1.2)<.025};});
      return {passed:images.length===5&&images.every(i=>i.passed),images};
    })()`);
    if (screenshots) {
      const name = 'overhaul-gallery-' + viewport.name + '.png';
      const screenshot = await call('Page.captureScreenshot', { format: 'png', captureBeyondViewport: false });
      await writeFile(join(resultDirectory, name), Buffer.from(screenshot.data, 'base64'));report.screenshots.push(name);
    }
  }
  await check('No remote resources requested', () => {
    const remote = report.networkRequests.filter(url => /^https?:/i.test(url));return { passed: remote.length === 0, remote };
  });
  await check('No JavaScript runtime exceptions', () => report.browserErrors.length === 0);
  await check('Gallery source remains unchanged by the browser check', async () => hash(await readFile(pagePath)) === report.pageSha256);
  report.passed = report.errors.length === 0;
} catch (error) {
  if (!report.errors.includes(error.message)) report.errors.push(error.message);
  process.exitCode = 1;
} finally {
  if (call && socket?.readyState === WebSocket.OPEN) await call('Browser.close').catch(() => {});
  if (socket && socket.readyState !== WebSocket.CLOSED) socket.close();
  if (browser && browser.exitCode === null && !browser.killed) browser.kill();
  report.completedUtc = new Date().toISOString();
  await mkdir(resultDirectory, { recursive: true });
  await writeFile(join(resultDirectory, 'overhaul-gallery-check.json'), JSON.stringify(report, null, 2));
  console.log(JSON.stringify({ passed: report.passed, checks: report.checks.length, images: report.images.length, errors: report.errors, report: join(resultDirectory, 'overhaul-gallery-check.json') }));
}
