import { spawn } from "node:child_process";
import { writeFile } from "node:fs/promises";
import { setTimeout as delay } from "node:timers/promises";

const chrome = "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe";
const outDir = "c:\\A1\\git\\cms\\docs\\po\\rate-ac-evidence\\ready100";
const publishedId = "01a13aa9-5b75-7798-a027-5c8bd75e55c4";
const child = spawn(chrome, ["--headless=new", "--disable-gpu", "--remote-debugging-port=9236", `--user-data-dir=${process.env.TEMP}\\lcms-chrome-ready100-i`, "about:blank"], { stdio: "ignore" });
let version;
for (let i = 0; i < 40; i++) {
  try { version = await (await fetch("http://127.0.0.1:9236/json/version")).json(); break; } catch { await delay(250); }
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
async function go(url) { await send("Page.navigate", { url }, sessionId); await delay(1500); }
async function size(width, height) {
  await send("Emulation.setDeviceMetricsOverride", { width, height, deviceScaleFactor: 1, mobile: false }, sessionId);
  await delay(250);
}
async function shot(name) {
  await evaluate(`const portal = document.querySelector("nextjs-portal"); if (portal) portal.style.display = "none"; return "ok";`);
  const image = await send("Page.captureScreenshot", { format: "png", fromSurface: true }, sessionId);
  await writeFile(`${outDir}\\${name}.png`, Buffer.from(image.data, "base64"));
}
await go("http://127.0.0.1:3000/login");
await evaluate(`
  const setValue = (el, value) => {
    Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "value").set.call(el, value);
    el.dispatchEvent(new Event("input", { bubbles: true }));
    el.dispatchEvent(new Event("change", { bubbles: true }));
  };
  setValue(document.querySelector("#email"), "admin@cms.local");
  setValue(document.querySelector("#password"), "abc123");
  document.querySelector("button[type=submit]").click();
  return "login";
`);
await delay(1500);
await go("http://127.0.0.1:3000/rate-cards/new");
await evaluate(`
  const setValue = (el, value) => {
    const proto = el instanceof HTMLSelectElement ? HTMLSelectElement.prototype : HTMLInputElement.prototype;
    Object.getOwnPropertyDescriptor(proto, "value").set.call(el, value);
    el.dispatchEvent(new Event("input", { bubbles: true }));
    el.dispatchEvent(new Event("change", { bubbles: true }));
  };
  const pickOption = (el, text) => {
    const opt = [...el.options].find((o) => o.textContent.includes(text));
    setValue(el, opt.value);
    return opt.textContent.trim();
  };
  setValue(document.querySelector("#code"), "READY100-SGN-HAN");
  setValue(document.querySelector("#name"), "Cước SGN → HAN");
  pickOption(document.querySelector("[aria-label='Chọn tuyến 1']"), "SGN → HAN");
  setValue(document.querySelector("[aria-label='Cách tính 1']"), "unit_rate");
  setValue(document.querySelector("[aria-label='Loại trọng lượng 1']"), "gross");
  setValue(document.querySelector("[aria-label='Trọng lượng từ 1']"), "0");
  setValue(document.querySelector("[aria-label='Trọng lượng đến 1']"), "45");
  setValue(document.querySelector(".col-amount input"), "120000");
  setValue(document.querySelector("[aria-label='Mã quy tắc 1']"), "CUOC-CHINH");
  setValue(document.querySelector("[aria-label='Tên quy tắc 1']"), "Cước chính");
  setValue(document.querySelector("[aria-label='Khoản mục 1']"), "Cước chính");
  const carrier = document.querySelector(".rate-rules .party-typeahead input[type=search]");
  carrier.focus();
  setValue(carrier, "Vietnam");
  return "filled";
`);
for (let i = 0; i < 20; i++) {
  await delay(200);
  const picked = await evaluate(`
    const button = [...document.querySelectorAll(".rate-rules .party-typeahead-list button")].find((el) => el.textContent.includes("Vietnam Airlines"));
    if (!button) return "wait";
    button.click();
    return "picked";
  `);
  if (picked === "picked") break;
}
await evaluate(`document.dispatchEvent(new MouseEvent("mousedown", { bubbles: true })); return "closed";`);
async function fit() {
  return evaluate(`
    const section = [...document.querySelectorAll(".section-title")].find((el) => el.textContent.includes("Bảng giá cơ bản"));
    section?.scrollIntoView({ block: "start" });
    const wrap = document.querySelector(".rate-declaration .table-wrap");
    if (wrap) wrap.scrollLeft = 0;
    const carrier = document.querySelector(".col-carrier");
    const name = document.querySelector("[aria-label='Tên quy tắc 1']");
    const route = document.querySelector("[aria-label='Chọn tuyến 1']");
    const carrierInput = document.querySelector(".col-carrier input[type=search]");
    const weight = document.querySelector(".col-weight");
    const edge = (el) => el ? Math.round(el.getBoundingClientRect().right) : null;
    const clip = (el) => el ? el.scrollWidth > el.clientWidth + 1 : null;
    return JSON.stringify({
      wrap: edge(wrap),
      carrier: edge(carrier),
      weight: edge(weight),
      name: name?.value,
      route: route?.selectedOptions?.[0]?.textContent?.trim(),
      carrierValue: carrierInput?.value,
      routeClip: clip(route),
      carrierClip: clip(carrierInput),
      clipped: carrier && wrap ? carrier.getBoundingClientRect().right > wrap.getBoundingClientRect().right + 2 : null,
    });
  `);
}
await size(1366, 768);
console.log("1366", await fit());
await shot("basic-1366x768");
await size(1920, 1080);
console.log("1920", await fit());
await shot("basic-1920x1080");
await go(`http://127.0.0.1:3000/rate-cards/${publishedId}`);
console.log("published", await evaluate(`
  return JSON.stringify({
    buttons: [...document.querySelector(".rate-detail-toolbar").querySelectorAll("a,button,span")].map((el) => el.textContent.trim()),
    editButton: Boolean([...document.querySelectorAll("a")].find((el) => el.textContent.trim() === "Chỉnh sửa bảng giá")),
    note: document.querySelector(".note")?.textContent?.trim(),
  });
`));
await size(1366, 768);
await shot("detail-published-1366x768");
await size(1920, 1080);
await shot("detail-published-1920x1080");
browser.close();
child.kill();
