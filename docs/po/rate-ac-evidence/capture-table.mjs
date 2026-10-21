import { spawn } from "node:child_process";
import { writeFile } from "node:fs/promises";
import { setTimeout as delay } from "node:timers/promises";

const chrome = "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe";
const outDir = "c:\\A1\\git\\cms\\docs\\po\\rate-ac-evidence";
const profile = `${outDir}\\chrome-profile-table`;
const child = spawn(chrome, [
  "--headless=new",
  "--disable-gpu",
  "--remote-debugging-port=9224",
  `--user-data-dir=${profile}`,
  "--window-size=1366,768",
  "about:blank"
], { stdio: "ignore" });

let version;
for (let i = 0; i < 40; i++) {
  try {
    version = await (await fetch("http://127.0.0.1:9224/json/version")).json();
    break;
  } catch {
    await delay(250);
  }
}
const browser = new WebSocket(version.webSocketDebuggerUrl);
const pending = new Map();
let id = 0;
function send(method, params = {}, sessionId) {
  const messageId = ++id;
  const body = { id: messageId, method, params };
  if (sessionId) body.sessionId = sessionId;
  return new Promise((resolve, reject) => {
    pending.set(messageId, { resolve, reject });
    browser.send(JSON.stringify(body));
  });
}
browser.addEventListener("message", (event) => {
  const message = JSON.parse(event.data);
  if (!message.id || !pending.has(message.id)) return;
  const waiter = pending.get(message.id);
  pending.delete(message.id);
  if (message.error) waiter.reject(new Error(JSON.stringify(message.error)));
  else waiter.resolve(message.result);
});
await new Promise((resolve) => browser.addEventListener("open", resolve));
const { targetId } = await send("Target.createTarget", { url: "about:blank" });
const { sessionId } = await send("Target.attachToTarget", { targetId, flatten: true });
await send("Page.enable", {}, sessionId);
await send("Runtime.enable", {}, sessionId);
async function evaluate(expression) {
  const result = await send("Runtime.evaluate", { expression, awaitPromise: true, returnByValue: true }, sessionId);
  if (result.exceptionDetails) throw new Error(JSON.stringify(result.exceptionDetails));
  return result.result.value;
}
async function go(url) {
  await send("Page.navigate", { url }, sessionId);
  await delay(1200);
}
await go("http://127.0.0.1:3000/login");
await evaluate(`
  const setValue = (selector, value) => {
    const el = document.querySelector(selector);
    const setter = Object.getOwnPropertyDescriptor(Object.getPrototypeOf(el), "value").set;
    setter.call(el, value);
    el.dispatchEvent(new Event("input", { bubbles: true }));
  };
  setValue("#email", "admin@cms.local");
  setValue("#password", "abc123");
  document.querySelector("button[type=submit]").click();
`);
await delay(2000);
await go("http://127.0.0.1:3000/rate-cards/new");
await delay(1500);

async function shot(name, width, height, zoom) {
  await send("Emulation.setDeviceMetricsOverride", { width, height, deviceScaleFactor: 1, mobile: false }, sessionId);
  await evaluate(`document.documentElement.style.zoom = ${JSON.stringify(zoom)}; "ok"`);
  await delay(300);
  const metrics = await evaluate(`(() => {
    document.querySelector(".rate-declaration .table-wrap")?.scrollIntoView({ block: "center" });
    const wrap = document.querySelector(".rate-declaration .table-wrap");
    const weight = document.querySelector("[aria-label='Trọng lượng từ 1']");
    const route = document.querySelector("[aria-label='Chọn tuyến 1']");
    const box = (el) => {
      if (!el) return null;
      const r = el.getBoundingClientRect();
      return { width: r.width, height: r.height, top: r.top, left: r.left };
    };
    return {
      wrap: wrap ? { scrollWidth: wrap.scrollWidth, clientWidth: wrap.clientWidth, overflowX: getComputedStyle(wrap).overflowX } : null,
      weight: box(weight),
      route: box(route)
    };
  })()`);
  await delay(300);
  const image = await send("Page.captureScreenshot", { format: "png", fromSurface: true }, sessionId);
  await writeFile(`${outDir}\\${name}.png`, Buffer.from(image.data, "base64"));
  console.log(name, JSON.stringify(metrics));
}

await shot("ac34-1366-rules", 1366, 768, "1");
await shot("ac35-1920-rules", 1920, 1080, "1");
await shot("ac35-zoom125-rules", 1366, 768, "1.25");
browser.close();
child.kill();
