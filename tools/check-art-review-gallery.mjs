import { spawn } from 'node:child_process';
import { readFile, writeFile, mkdir, stat } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import { dirname, resolve, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

// Functional browser checks only. They do not approve the 3D assets or measure game performance.
const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const pagePath = join(root, 'Artifacts', 'ArtReview', 'index.html');
const resultDirectory = join(root, 'TestResults');
const profile = 'D:/EmberfieldWorkingCache/AoE/art-review-browser-' + Date.now();
const captureScreenshots = !process.argv.includes('--no-screenshots');
const report = {
  schema: 1,
  scope: 'Offline gallery browser behavior, image loading, local documentation links and responsive layout. No art acceptance or Unity/mobile performance result.',
  startedUtc: new Date().toISOString(),
  page: pagePath,
  checks: [],
  browserErrors: [],
  networkRequests: [],
  screenshots: [],
  viewports: [],
  passed: false,
};
const pause = ms => new Promise(resolve => setTimeout(resolve, ms));
const hash = bytes => createHash('sha256').update(bytes).digest('hex');
let browser, socket, call, evaluate, startupError;

async function assertCheck(name, operation) {
  const value = typeof operation === 'string' ? await evaluate(operation) : await operation();
  const passed = typeof value === 'object' && value !== null && 'passed' in value ? value.passed : Boolean(value);
  report.checks.push({ name, passed, ...(typeof value === 'object' && value !== null ? { details: value } : {}) });
  if (!passed) throw new Error('Gallery check failed: ' + name);
  return value;
}

async function frame() {
  await evaluate('new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)))');
}

async function click(selector) {
  const point = await evaluate(`(() => {
    const node = document.querySelector(${JSON.stringify(selector)});
    if (!node) throw new Error('Missing clickable element: ' + ${JSON.stringify(selector)});
    node.scrollIntoView({block:'center',inline:'center',behavior:'instant'});
    const rect = node.getBoundingClientRect();
    if (rect.width <= 0 || rect.height <= 0) throw new Error('Clickable element is not visible');
    return {x:rect.left+rect.width/2,y:rect.top+rect.height/2};
  })()`);
  await call('Input.dispatchMouseEvent', { type: 'mouseMoved', ...point });
  await call('Input.dispatchMouseEvent', { type: 'mousePressed', ...point, button: 'left', clickCount: 1 });
  await call('Input.dispatchMouseEvent', { type: 'mouseReleased', ...point, button: 'left', clickCount: 1 });
  await frame();
}

async function key(keyName, code, keyCode) {
  await call('Input.dispatchKeyEvent', { type: 'keyDown', key: keyName, code, windowsVirtualKeyCode: keyCode, nativeVirtualKeyCode: keyCode });
  await call('Input.dispatchKeyEvent', { type: 'keyUp', key: keyName, code, windowsVirtualKeyCode: keyCode, nativeVirtualKeyCode: keyCode });
  await frame();
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
  for (let attempt = 0; attempt < 160; attempt++) {
    if (startupError) throw startupError;
    try { port = (await readFile(profile + '/DevToolsActivePort', 'utf8')).split('\n')[0].trim(); break; }
    catch { await pause(100); }
  }
  if (!port) throw new Error('Chrome did not expose its isolated debugging port.');
  const targets = await (await fetch('http://127.0.0.1:' + port + '/json/list')).json();
  const target = targets.find(item => item.type === 'page');
  if (!target) throw new Error('Chrome has no page debugging target.');
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
      const detail = message.params.exceptionDetails;
      report.browserErrors.push(detail.exception?.description || detail.text);
    }
    if (message.method === 'Network.requestWillBeSent') report.networkRequests.push(message.params.request.url);
    if (!message.id || !pending.has(message.id)) return;
    const { resolve, reject, timer } = pending.get(message.id);
    pending.delete(message.id); clearTimeout(timer);
    if (message.error) reject(new Error(JSON.stringify(message.error)));
    else resolve(message.result);
  };
  socket.onclose = () => {
    for (const { reject, timer } of pending.values()) { clearTimeout(timer); reject(new Error('Chrome connection closed.')); }
    pending.clear();
  };
  call = (method, params = {}) => new Promise((resolve, reject) => {
    const id = ++nextId;
    const timer = setTimeout(() => { pending.delete(id); reject(new Error('CDP timeout: ' + method)); }, 12000);
    pending.set(id, { resolve, reject, timer });
    socket.send(JSON.stringify({ id, method, params }));
  });
  evaluate = async expression => {
    const result = await call('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true });
    if (result.exceptionDetails) throw new Error(result.exceptionDetails.exception?.description || result.exceptionDetails.text);
    return result.result.value;
  };
  await call('Runtime.enable'); await call('Page.enable'); await call('Network.enable');
  report.browser = await call('Browser.getVersion');
  await call('Network.emulateNetworkConditions', { offline: true, latency: 0, downloadThroughput: 0, uploadThroughput: 0 });
  await call('Emulation.setDeviceMetricsOverride', { width: 1440, height: 1000, deviceScaleFactor: 1, mobile: false });
  await call('Page.navigate', { url: pathToFileURL(pagePath).href });
  let ready = false;
  for (let attempt = 0; attempt < 100; attempt++) {
    ready = await evaluate("document.readyState==='complete' && document.querySelectorAll('#stats .stat').length===4 && document.querySelectorAll('#lod-body tr').length>0");
    if (ready) break;
    await pause(100);
  }
  await assertCheck('Gallery initializes from a local file with networking disabled', () => ready);
  report.loadedEvidence = await evaluate(`(() => {const d=JSON.parse(document.getElementById('review-data').textContent);return {
    galleryImageCount:d.images.length,assetValidationPassed:d.validation.passed,
    assetReportGeneratedUtc:d.validation.generatedUtc,playerReportPresent:Boolean(d.player),
    galleryWarnings:d.warnings||[]};})()`);
  await assertCheck('Every manifest image loads offline at its declared dimensions', `(async () => {
    const d=JSON.parse(document.getElementById('review-data').textContent);
    const images=await Promise.all(d.images.map(async item=>{
      const image=new Image();image.src=new URL(item.image,location.href).href;
      try {await image.decode();return {key:item.key,width:image.naturalWidth,height:image.naturalHeight,passed:image.naturalWidth===item.width&&image.naturalHeight===item.height};}
      catch {return {key:item.key,passed:false};}
    }));return {passed:images.length>=8&&images.every(x=>x.passed),images};
  })()`);
  await assertCheck('Visible page images decode offline', `(async()=>{const all=[...document.images].filter(i=>i.getAttribute('src'));all.forEach(i=>i.loading='eager');await Promise.all(all.map(i=>i.decode()));return all.every(i=>i.naturalWidth>0);})()`);

  for (const view of ['close', 'mid', 'rts', 'comparison']) {
    await click(`[data-view="${view}"]`);
    await assertCheck('Camera tab selects ' + view, `(async()=>{const image=document.getElementById('main-image');await image.decode();return image.getAttribute('src')===${JSON.stringify(view + '.png')}&&document.getElementById('main-picture').dataset.zoom===${JSON.stringify(view)}&&document.querySelector('[data-view="${view}"]').getAttribute('aria-pressed')==='true'&&document.querySelectorAll('#view-tabs [aria-pressed="true"]').length===1;})()`);
  }
  for (const [reference, file] of [['reference-warrior', 'warrior-reference.png'], ['reference-worker', 'worker-reference.png']]) {
    await click(`[data-reference="${reference}"]`);
    await assertCheck('Concept comparison selects ' + reference, `(async()=>{const image=document.getElementById('concept-image');await image.decode();return image.getAttribute('src')===${JSON.stringify('references/' + file)}&&document.getElementById('concept-picture').dataset.zoom===${JSON.stringify(reference)}&&document.querySelector('[data-reference="${reference}"]').getAttribute('aria-pressed')==='true';})()`);
  }
  await click('#concept-picture');
  await assertCheck('Concept opens in enlarged viewer', "document.getElementById('viewer').open&&document.getElementById('viewer-image').getAttribute('src')==='references/worker-reference.png'");
  const originalImage = await evaluate("document.getElementById('viewer-image').src");
  await click('#viewer-next');
  await assertCheck('Viewer next button changes the image', `document.getElementById('viewer-image').src!==${JSON.stringify(originalImage)}`);
  await click('#viewer-prev');
  await assertCheck('Viewer previous button restores the image', `document.getElementById('viewer-image').src===${JSON.stringify(originalImage)}`);
  await key('ArrowRight', 'ArrowRight', 39);
  await assertCheck('Keyboard right arrow advances the viewer', `document.getElementById('viewer-image').src!==${JSON.stringify(originalImage)}`);
  await key('ArrowLeft', 'ArrowLeft', 37);
  await assertCheck('Keyboard left arrow restores the viewer', `document.getElementById('viewer-image').src===${JSON.stringify(originalImage)}`);
  await key('Escape', 'Escape', 27);
  await assertCheck('Escape closes the enlarged viewer', "!document.getElementById('viewer').open");
  await click('#main-picture'); await click('#viewer-close');
  await assertCheck('Close button dismisses enlarged render', "!document.getElementById('viewer').open");

  const links = await evaluate("[...document.querySelectorAll('#documents a')].map(a=>({name:a.textContent,url:a.href}))");
  await assertCheck('All local documentation and report links exist', async () => {
    const checked = [];
    for (const link of links) {
      const url = new URL(link.url);
      if (url.protocol !== 'file:') { checked.push({ ...link, passed: false }); continue; }
      url.hash = ''; url.search = '';
      let exists = false;
      try { exists = (await stat(fileURLToPath(url))).isFile(); } catch { /* Recorded below. */ }
      checked.push({ ...link, passed: exists });
    }
    return { passed: checked.length >= 5 && checked.every(x => x.passed), links: checked };
  });

  for (const viewport of [
    { name: 'desktop', width: 1440, height: 1000, mobile: false },
    { name: 'tablet', width: 1024, height: 1366, mobile: true },
    { name: 'phone', width: 390, height: 844, mobile: true },
  ]) {
    await call('Emulation.setDeviceMetricsOverride', { width: viewport.width, height: viewport.height, deviceScaleFactor: 1, mobile: viewport.mobile });
    await click('[data-view="rts"]');
    await evaluate("window.scrollTo({top:0,left:0,behavior:'instant'})"); await frame();
    const layout = await assertCheck(viewport.name + ' page has no horizontal overflow', `(() => {
      const width=document.documentElement.clientWidth,scroll=document.documentElement.scrollWidth;
      return {passed:scroll<=innerWidth+1&&Math.abs(innerWidth-${viewport.width})<=1,width,innerWidth,scrollWidth:scroll,documentHeight:document.documentElement.scrollHeight};
    })()`);
    report.viewports.push({ ...viewport, ...layout });
    if (captureScreenshots) {
      const name = 'art-review-browser-' + viewport.name + '.png';
      const screenshot = await call('Page.captureScreenshot', { format: 'png', captureBeyondViewport: false });
      await writeFile(join(resultDirectory, name), Buffer.from(screenshot.data, 'base64'));
      report.screenshots.push(name);
    }
    await click('#main-picture');
    await assertCheck(viewport.name + ' enlarged viewer fits the viewport', `(() => {
      const modal=document.getElementById('viewer'),r=modal.getBoundingClientRect();
      return {passed:modal.open&&r.left>=-1&&r.right<=innerWidth+1&&r.top>=-1&&r.bottom<=innerHeight+1&&modal.scrollWidth<=modal.clientWidth+1,width:r.width,height:r.height,scrollWidth:modal.scrollWidth,clientWidth:modal.clientWidth};
    })()`);
    await key('Escape', 'Escape', 27);
  }
  await assertCheck('Gallery does not request remote resources', () => {
    const remote = report.networkRequests.filter(url => /^https?:/i.test(url));
    return { passed: remote.length === 0, remote };
  });
  await assertCheck('No JavaScript runtime exceptions', () => report.browserErrors.length === 0);
  await assertCheck('Gallery source stayed unchanged throughout this browser check', async () => hash(await readFile(pagePath)) === report.pageSha256);
  report.passed = true;
} catch (error) {
  report.error = error.message;
  process.exitCode = 1;
} finally {
  if (call && socket?.readyState === WebSocket.OPEN) await call('Browser.close').catch(() => {});
  if (socket && socket.readyState !== WebSocket.CLOSED) socket.close();
  if (browser && browser.exitCode === null && !browser.killed) browser.kill();
  report.completedUtc = new Date().toISOString();
  await mkdir(resultDirectory, { recursive: true });
  await writeFile(join(resultDirectory, 'art-review-browser.json'), JSON.stringify(report, null, 2));
  console.log(JSON.stringify({ passed: report.passed, checks: report.checks.length, images: report.loadedEvidence?.galleryImageCount, error: report.error, report: join(resultDirectory, 'art-review-browser.json') }));
}
