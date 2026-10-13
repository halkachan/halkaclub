// Drives the Phase 1 POC WebGL build in a separate, throw-away headless Chrome over the
// Chrome DevTools Protocol (Node 22+ built-in WebSocket; no packages, not the person's Chrome).
//
//   chrome.exe --headless=new --remote-debugging-port=9334 --user-data-dir=<temp> --window-size=1280,720
//   node poc_cli.mjs <command> [...args] [-- <command> ...]
//   Commands separated by "--" share one DevTools session, which device emulation needs.
//
// Commands: nav URL | wait-unity | shot FILE | burst N MS PREFIX | hold-async CODE MS | keys "KeyD:130,KeyW:130" | hold CODE MS
//           click X Y | touch X Y MS | fps SECONDS | device W H SCALE MOBILE | eval EXPR
import { writeFileSync } from "node:fs";

const PORT = Number(process.env.POC_CDP_PORT || 9334);
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const targets = await (await fetch(`http://127.0.0.1:${PORT}/json`)).json();
const page = targets.find((t) => t.type === "page");
const ws = new WebSocket(page.webSocketDebuggerUrl);
await new Promise((r) => ws.addEventListener("open", r, { once: true }));
let nextId = 0;
const pending = new Map();
ws.addEventListener("message", (event) => {
  const msg = JSON.parse(event.data);
  if (!msg.id || !pending.has(msg.id)) return;
  const { resolve, reject } = pending.get(msg.id);
  pending.delete(msg.id);
  msg.error ? reject(new Error(JSON.stringify(msg.error))) : resolve(msg.result);
});
const send = (method, params = {}) => new Promise((resolve, reject) => {
  const id = ++nextId;
  pending.set(id, { resolve, reject });
  ws.send(JSON.stringify({ id, method, params }));
});
const evaluate = async (expression) => {
  const r = await send("Runtime.evaluate", { expression, awaitPromise: true, returnByValue: true });
  if (r.exceptionDetails) throw new Error(JSON.stringify(r.exceptionDetails));
  return r.result.value;
};
const KEYS = { KeyW: ["w", 87], KeyA: ["a", 65], KeyS: ["s", 83], KeyD: ["d", 68],
  ArrowUp: ["ArrowUp", 38], ArrowDown: ["ArrowDown", 40], ArrowLeft: ["ArrowLeft", 37], ArrowRight: ["ArrowRight", 39] };
const key = (type, code) => send("Input.dispatchKeyEvent", { type, code, key: KEYS[code][0],
  windowsVirtualKeyCode: KEYS[code][1], nativeVirtualKeyCode: KEYS[code][1] });
const mouse = (type, x, y) => send("Input.dispatchMouseEvent", { type, x, y, button: "left", clickCount: 1 });

async function run(command, args) {
switch (command) {
  case "nav":
    await send("Page.navigate", { url: args[0] });
    break;
  case "wait-unity":
    for (let i = 0; i < 120 && !(await evaluate("!document.getElementById('loading')")); i++) await sleep(500);
    await sleep(Number(args[0] || 2000));
    console.log("unity ready");
    break;
  case "shot": {
    const { data } = await send("Page.captureScreenshot", { format: "png" });
    writeFileSync(args[0], Buffer.from(data, "base64"));
    console.log("saved " + args[0]);
    break;
  }
  case "keys":
    for (const item of args[0].split(",")) {
      const [code, ms] = item.split(":");
      await key("keyDown", code);
      await sleep(Number(ms));
      await key("keyUp", code);
      await sleep(Number(args[1] || 120));
    }
    break;
  case "hold":
    await key("keyDown", args[0]);
    await sleep(Number(args[1]));
    await key("keyUp", args[0]);
    break;
  case "hold-async":
    // Starts holding a key and returns at once, so a following burst records the walk.
    await key("keyDown", args[0]);
    setTimeout(() => key("keyUp", args[0]), Number(args[1]));
    break;
  case "click":
    await mouse("mouseMoved", Number(args[0]), Number(args[1]));
    await mouse("mousePressed", Number(args[0]), Number(args[1]));
    await sleep(60);
    await mouse("mouseReleased", Number(args[0]), Number(args[1]));
    break;
  case "touch": {
    const point = [{ x: Number(args[0]), y: Number(args[1]), id: 1 }];
    await send("Input.dispatchTouchEvent", { type: "touchStart", touchPoints: point });
    await sleep(Number(args[2] || 80));
    await send("Input.dispatchTouchEvent", { type: "touchEnd", touchPoints: [] });
    break;
  }
  case "fps": {
    const seconds = Number(args[0] || 5);
    console.log(JSON.stringify(await evaluate(`new Promise((resolve) => {
      const times = []; let last = performance.now(); const end = last + ${seconds * 1000};
      function frame(now) { times.push(now - last); last = now;
        if (now < end) requestAnimationFrame(frame); else {
          const s = times.slice(1).sort((a, b) => a - b); const q = (p) => s[Math.min(s.length - 1, Math.floor(p * s.length))];
          resolve({ frames: s.length, fps: +(s.length / ${seconds}).toFixed(1), p50: +q(0.5).toFixed(1),
            p95: +q(0.95).toFixed(1), max: +s[s.length - 1].toFixed(1),
            jsHeapMB: performance.memory ? +(performance.memory.usedJSHeapSize / 1048576).toFixed(1) : null });
        } }
      requestAnimationFrame(frame); })`)));
    break;
  }
  case "device":
    await send("Emulation.setDeviceMetricsOverride", { width: Number(args[0]), height: Number(args[1]),
      deviceScaleFactor: Number(args[2]), mobile: args[3] === "mobile" });
    await send("Emulation.setTouchEmulationEnabled", { enabled: args[3] === "mobile", maxTouchPoints: 5 });
    break;
  case "eval":
    console.log(JSON.stringify(await evaluate(args[0])));
    break;
  case "burst": {
    // burst COUNT INTERVAL_MS FILE_PREFIX : numbered PNG frames for shimmer checks and videos.
    const started = Date.now();
    for (let i = 0; i < Number(args[0]); i++) {
      const { data } = await send("Page.captureScreenshot", { format: "png" });
      writeFileSync(`${args[2]}_${String(i).padStart(3, "0")}.png`, Buffer.from(data, "base64"));
      await sleep(Number(args[1]));
    }
    console.log(`burst ${args[0]} frames in ${Date.now() - started} ms`);
    break;
  }
  case "sleep":
    await sleep(Number(args[0]));
    break;
  default:
    throw new Error("Unknown command " + command);
}
}

let chain = [];
for (const token of [...process.argv.slice(2), "--"]) {
  if (token !== "--") { chain.push(token); continue; }
  if (chain.length) await run(chain[0], chain.slice(1));
  chain = [];
}
ws.close();
process.exit(0);
