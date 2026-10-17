import { spawn } from "node:child_process";
import { mkdir, writeFile } from "node:fs/promises";
import { setTimeout as delay } from "node:timers/promises";

const phase = process.argv[2] ?? "before";
const detailId = process.argv[3];
const chrome = "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe";
const outDir = `c:\\A1\\git\\cms\\docs\\po\\17102026-LCMS_UAT_RATE_UI_05_07_DEV_Package_v1.0\\evidence\\${phase}`;
await mkdir(outDir, { recursive: true });
const port = phase === "before" ? 9241 : 9242;
const child = spawn(chrome, ["--headless=new", "--disable-gpu", `--remote-debugging-port=${port}`, `--user-data-dir=${process.env.TEMP}\\lcms-chrome-ui0507-${phase}`, "about:blank"], { stdio: "ignore" });
let version;
for (let i = 0; i < 40; i++) {
  try { version = await (await fetch(`http://127.0.0.1:${port}/json/version`)).json(); break; } catch { await delay(250); }
}
if (!version) throw new Error("no chrome");
const browser = new WebSocket(version.webSocketDebuggerUrl);
const pending = new Map();
let id = 0;
function send(method, params = {}, sessionId) {
  const messageId = ++id;
  return new Promise((resolve, reject) => {
    pending.set(messageId, { resolve, reject });
    browser.send(JSON.stringify({ id: messageId, method, params, sessionId }));
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
  const result = await send("Runtime.evaluate", { expression: `(() => {\n${expression}\n})()`, awaitPromise: true, returnByValue: true }, sessionId);
  if (result.exceptionDetails) throw new Error(JSON.stringify(result.exceptionDetails).slice(0, 1500));
  return result.result.value;
}
async function go(url) {
  await send("Page.navigate", { url }, sessionId);
  for (let i = 0; i < 60; i++) {
    await delay(500);
    const ready = await evaluate(`return document.readyState === "complete" && !!document.querySelector("main, form, h1");`).catch(() => false);
    if (ready) break;
  }
  await delay(1500);
}
async function setViewport(width, height, mobile = false) {
  await send("Emulation.setDeviceMetricsOverride", { width, height, deviceScaleFactor: 1, mobile }, sessionId);
  await delay(400);
}
async function shot(name, width = 1920) {
  await evaluate(`const portal = document.querySelector("nextjs-portal"); if (portal) portal.style.display = "none"; return "ok";`);
  const metrics = await send("Page.getLayoutMetrics", {}, sessionId);
  const height = Math.min(Math.ceil(metrics.cssContentSize.height), 4000);
  const image = await send("Page.captureScreenshot", { format: "png", captureBeyondViewport: true, clip: { x: 0, y: 0, width, height, scale: 1 } }, sessionId);
  await writeFile(`${outDir}\\${name}.png`, Buffer.from(image.data, "base64"));
}
async function checkResponsive(label) {
  const res = await evaluate(`
    return JSON.stringify({
      innerWidth: window.innerWidth,
      docScrollWidth: document.documentElement.scrollWidth,
      overflow: document.documentElement.scrollWidth > window.innerWidth + 1,
      chargeLink: document.querySelector("a[href='/rate-cards/charge-types']")?.getAttribute("href") ?? null,
    });
  `);
  console.log("responsive", label, res);
}
await setViewport(1920, 1080, false);
await go("http://127.0.0.1:3000/login");
for (let attempt = 0; attempt < 6; attempt++) {
await evaluate(`
  const setValue = (el, value) => {
    Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "value").set.call(el, value);
    el.dispatchEvent(new Event("input", { bubbles: true }));
  };
  setValue(document.querySelector("#email"), "admin@cms.local");
  setValue(document.querySelector("#password"), "abc123");
  document.querySelector("button[type=submit]").click();
  return "login";
`);
await delay(4000);
if (!(await evaluate(`return location.pathname;`)).startsWith("/login")) break;
}
await go("http://127.0.0.1:3000/rate-cards/new");
const labels = `return JSON.stringify([...document.querySelectorAll("[aria-label]")].map((el) => el.getAttribute("aria-label")).filter((l) => /Lọc chiều|Nhóm cạnh tranh|trọng lượng|Khoản giá|Trọng lượng/i.test(l)));`;
console.log("basic labels", await evaluate(labels));
await evaluate(`
  const sel = [...document.querySelectorAll("select")].find((el) => /trọng lượng 1/i.test(el.getAttribute("aria-label") ?? ""));
  if (sel) {
    Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype, "value").set.call(sel, "gross");
    sel.dispatchEvent(new Event("change", { bubbles: true }));
  }
  const wrap = document.querySelector(".rate-declaration .table-wrap");
  [...document.querySelectorAll(".section-title")].find((el) => el.textContent.includes("Bảng giá cơ bản"))?.scrollIntoView({ block: "start" });
  return "ok";
`);
await delay(500);
await shot("basic-1920");
await evaluate(`
  const wrap = document.querySelector(".rate-declaration .table-wrap");
  if (wrap) wrap.scrollLeft = wrap.scrollWidth;
  return "ok";
`);
await delay(300);
await shot("basic-1920-scrolled-right");
if (phase !== "before") {
  await evaluate(`
    const setValue = (el, value) => {
      const proto = el instanceof HTMLSelectElement ? HTMLSelectElement.prototype : HTMLInputElement.prototype;
      Object.getOwnPropertyDescriptor(proto, "value").set.call(el, value);
      el.dispatchEvent(new Event(el instanceof HTMLSelectElement ? "change" : "input", { bubbles: true }));
    };
    setValue(document.querySelector("[aria-label='Trọng lượng từ 1']"), "0");
    setValue(document.querySelector("[aria-label='Trọng lượng đến 1']"), "5");
    setValue(document.querySelector("[aria-label='Tên quy tắc 1']"), "Cước chính");
    return "ok";
  `);
  await delay(300);
  await evaluate(`
    const sel = document.querySelector("[aria-label='Điều kiện trọng lượng 1']");
    Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype, "value").set.call(sel, "");
    sel.dispatchEvent(new Event("change", { bubbles: true }));
    const wrap = document.querySelector(".rate-declaration .table-wrap");
    if (wrap) wrap.scrollLeft = 0;
    return "ok";
  `);
  await delay(500);
  await shot("basic-1920-no-band-cleared", 1920);
  await setViewport(768, 1024, false);
  await checkResponsive("basic-768");
  await shot("basic-768", 768);
  await setViewport(375, 812, true);
  await checkResponsive("basic-375");
  await shot("basic-375", 375);
  await setViewport(1920, 1080, false);
}
await evaluate(`document.querySelectorAll("input[name=declarationMode]")[1].click(); return "ok";`);
await delay(500);
await evaluate(`[...document.querySelectorAll("button")].find((b) => b.textContent.trim() === "Đồng ý")?.click(); return "ok";`);
await delay(1000);
console.log("advanced labels", await evaluate(labels));
await shot("advanced-1920", 1920);
if (phase !== "before") {
  await setViewport(768, 1024, false);
  await checkResponsive("advanced-768");
  await shot("advanced-768", 768);
  await setViewport(375, 812, true);
  await checkResponsive("advanced-375");
  await shot("advanced-375", 375);
  await setViewport(1920, 1080, false);
}
if (detailId) {
  await go(`http://127.0.0.1:3000/rate-cards/${detailId}`);
  await shot("detail-1920", 1920);
}
browser.close();
child.kill();
