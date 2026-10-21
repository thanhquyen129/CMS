import { spawn } from "node:child_process";
import { mkdir, writeFile } from "node:fs/promises";
import { setTimeout as delay } from "node:timers/promises";

const chrome = "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe";
const outDir = "c:\\A1\\git\\cms\\docs\\po\\rate-ac-evidence\\ready100";
const profile = process.env.TEMP + "\\lcms-chrome-ready100";
const tenant = "01a13a1b-d203-7c6f-8d40-f257c2083451";
await mkdir(outDir, { recursive: true });

for (const [code, name] of [
  ["GENERAL", "Hàng thường"],
  ["DRY_FOOD", "Thực phẩm khô"],
  ["COSMETICS", "Mỹ phẩm"],
  ["EXPRESS", "Chuyển nhanh"],
]) {
  const res = await fetch("http://127.0.0.1:5158/api/commodities", {
    method: "PUT",
    headers: { "Content-Type": "application/json", Accept: "application/json", "X-Tenant-Id": tenant },
    body: JSON.stringify({ code, name, isActive: true }),
  });
  console.log("commodity", code, res.status);
}

const child = spawn(chrome, ["--headless=new", "--disable-gpu", "--remote-debugging-port=9229", `--user-data-dir=${profile}-b`, "about:blank"], { stdio: "ignore" });
let version;
for (let i = 0; i < 40; i++) {
  try { version = await (await fetch("http://127.0.0.1:9229/json/version")).json(); break; } catch { await delay(250); }
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
  if (result.exceptionDetails) throw new Error(JSON.stringify(result.exceptionDetails).slice(0, 1800));
  return result.result.value;
}
async function go(url) { await send("Page.navigate", { url }, sessionId); await delay(1600); }
async function size(width, height) {
  await send("Emulation.setDeviceMetricsOverride", { width, height, deviceScaleFactor: 1, mobile: false }, sessionId);
  await delay(250);
}
async function shot(name) {
  const issues = await evaluate(`
    const portal = document.querySelector("nextjs-portal");
    const text = portal ? (portal.shadowRoot?.innerText || portal.innerText || "") : "";
    if (portal) portal.style.display = "none";
    return text.replace(/\\s+/g, " ").slice(0, 180);
  `);
  console.log("issues", name, issues || "none");
  const image = await send("Page.captureScreenshot", { format: "png", fromSurface: true }, sessionId);
  await writeFile(`${outDir}\\${name}.png`, Buffer.from(image.data, "base64"));
}
const setHelpers = `
  const setValue = (el, value) => {
    const proto = el instanceof HTMLSelectElement ? HTMLSelectElement.prototype : HTMLInputElement.prototype;
    Object.getOwnPropertyDescriptor(proto, "value").set.call(el, value);
    el.dispatchEvent(new Event("input", { bubbles: true }));
    el.dispatchEvent(new Event("change", { bubbles: true }));
  };
  const pickOption = (el, text) => {
    const opt = [...el.options].find((o) => o.textContent.includes(text));
    if (!opt) return "";
    setValue(el, opt.value);
    return opt.textContent.trim();
  };
`;

await go("http://127.0.0.1:3000/login");
await evaluate(`
  ${setHelpers}
  setValue(document.querySelector("#email"), "admin@cms.local");
  setValue(document.querySelector("#password"), "abc123");
  document.querySelector("button[type=submit]").click();
  return "login";
`);
await delay(1800);
await go("http://127.0.0.1:3000/rate-cards/new");
const basicFill = await evaluate(`
  ${setHelpers}
  setValue(document.querySelector("#code"), "READY100-SGN-HAN");
  setValue(document.querySelector("#name"), "Cước SGN → HAN");
  const route = pickOption(document.querySelector("[aria-label='Chọn tuyến 1']"), "SGN → HAN");
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
  const headers = [...document.querySelectorAll(".rate-rules th")].map((el) => el.textContent.trim());
  return JSON.stringify({ route, headers });
`);
console.log("basic", basicFill);
let picked = "wait";
for (let i = 0; i < 20 && picked !== "picked"; i++) {
  await delay(200);
  picked = await evaluate(`
    const button = [...document.querySelectorAll(".rate-rules .party-typeahead-list button")].find((el) => el.textContent.includes("Vietnam Airlines"));
    if (!button) return "wait";
    button.click();
    return "picked";
  `);
}
await evaluate(`document.dispatchEvent(new MouseEvent("mousedown", { bubbles: true })); return "closed";`);
console.log("carrier", picked);
async function showBasicTable() {
  return evaluate(`
    const section = [...document.querySelectorAll(".section-title")].find((el) => el.textContent.includes("Bảng giá cơ bản"));
    section?.scrollIntoView({ block: "start" });
    const wrap = document.querySelector(".rate-declaration .table-wrap");
    if (wrap) wrap.scrollLeft = 0;
    const name = document.querySelector("[aria-label='Tên quy tắc 1']");
    const code = document.querySelector("[aria-label='Mã quy tắc 1']");
    const box = (el) => el ? { text: el.value, w: Math.round(el.getBoundingClientRect().width), left: Math.round(el.getBoundingClientRect().left) } : null;
    return JSON.stringify({
      scroll: wrap ? wrap.scrollLeft : null,
      name: box(name),
      code: box(code),
      headers: [...document.querySelectorAll(".rate-rules th")].slice(0, 6).map((el) => el.textContent.trim()),
    });
  `);
}
await size(1366, 768);
console.log("basic1366", await showBasicTable());
await shot("basic-1366x768");
await size(1920, 1080);
console.log("basic1920", await showBasicTable());
await shot("basic-1920x1080");

await evaluate(`[...document.querySelectorAll("button")].find((el) => el.textContent.trim() === "Lưu nháp")?.click(); return "save";`);
let path = "";
for (let i = 0; i < 30; i++) {
  await delay(400);
  path = await evaluate(`return location.pathname;`);
  if (path.startsWith("/rate-cards/") && !path.endsWith("/new") && !path.endsWith("/edit")) break;
}
console.log("saved", path);
if (!path.startsWith("/rate-cards/") || path.endsWith("/new")) {
  const err = await evaluate(`return document.querySelector("[role=alert]")?.textContent || document.body.innerText.slice(0, 400);`);
  throw new Error("save failed " + err);
}
await delay(800);
await size(1366, 768);
await evaluate(`
  const title = document.querySelector("h2");
  title?.scrollIntoView({ block: "start" });
  return document.body.innerText.replace(/\\s+/g, " ").slice(0, 500);
`);
console.log("draft", await evaluate(`return document.body.innerText.replace(/\\s+/g, " ").slice(0, 420);`));
await shot("detail-draft-1366x768");
await size(1920, 1080);
await shot("detail-draft-1920x1080");

await evaluate(`window.confirm = () => true; [...document.querySelectorAll("button")].find((el) => el.textContent.includes("Phát hành phiên bản"))?.click(); return "publish";`);
let published = "";
for (let i = 0; i < 25; i++) {
  await delay(400);
  published = await evaluate(`return document.body.innerText.includes("Đã phát hành") && !document.body.innerText.includes("Chỉnh sửa bảng giá") ? "locked" : "wait";`);
  if (published === "locked") break;
}
console.log("publish", published, await evaluate(`return document.body.innerText.replace(/\\s+/g, " ").slice(0, 420);`));
await size(1366, 768);
await shot("detail-published-1366x768");
await size(1920, 1080);
await shot("detail-published-1920x1080");

await go("http://127.0.0.1:3000/rate-cards/new");
await evaluate(`
  const advanced = [...document.querySelectorAll("input[name=declarationMode]")].find((el) => el.parentElement.textContent.includes("nâng cao"));
  advanced?.click();
  return "mode";
`);
await delay(400);
await evaluate(`[...document.querySelectorAll("button")].find((el) => el.textContent.trim() === "Thêm nhóm")?.click(); return "added";`);
await delay(250);
const routesPicked = await evaluate(`
  ${setHelpers}
  const routes = [...document.querySelectorAll("select[id^='group-route-']")];
  const texts = [pickOption(routes[0], "SGN → HAN"), pickOption(routes[1], "HAN → DAD")];
  const carriers = [...document.querySelectorAll(".rate-block-rules .party-typeahead input[type=search]")];
  setValue(carriers[0], "Vietnam");
  setValue(carriers[1], "Vietjet");
  return texts.join(" | ");
`);
console.log("routes", routesPicked);
for (let n = 0; n < 20; n++) {
  await delay(200);
  const left = await evaluate(`
    const lists = [...document.querySelectorAll(".rate-block-rules .party-typeahead")];
    let waiting = 0;
    for (const root of lists) {
      const input = root.querySelector("input[type=search]");
      if (input && input.value.includes("—")) continue;
      const button = [...root.querySelectorAll(".party-typeahead-list button")].find((el) => /Vietnam Airlines|Vietjet/.test(el.textContent));
      if (!button) { waiting += 1; continue; }
      button.click();
    }
    return waiting;
  `);
  if (left === 0) break;
}
for (let step = 0; step < 16; step++) {
  const clicks = await evaluate(`
    let clicks = 0;
    for (const group of document.querySelectorAll("section.rate-block-rules")) {
      if (group.querySelectorAll("tbody tr").length <= 2) continue;
      const button = [...group.querySelectorAll("tbody tr button")].find((el) => el.textContent.trim() === "Xóa");
      if (button) { button.click(); clicks += 1; }
    }
    return clicks;
  `);
  if (!clicks) break;
  await delay(120);
}
const matrix = await evaluate(`
  const setValue = (el, value) => {
    const proto = el instanceof HTMLSelectElement ? HTMLSelectElement.prototype : HTMLInputElement.prototype;
    Object.getOwnPropertyDescriptor(proto, "value").set.call(el, value);
    el.dispatchEvent(new Event("input", { bubbles: true }));
    el.dispatchEvent(new Event("change", { bubbles: true }));
  };
  const labels = [];
  [...document.querySelectorAll("section.rate-block-rules")].forEach((group, groupIndex) => {
    const mins = [...group.querySelectorAll("input[aria-label='Từ']")];
    const maxes = [...group.querySelectorAll("input[aria-label='Đến']")];
    if (mins[0]) setValue(mins[0], "0");
    if (maxes[0]) setValue(maxes[0], "45");
    if (mins[1]) setValue(mins[1], "45");
    if (maxes[1]) setValue(maxes[1], "");
    const prices = [...group.querySelectorAll("tbody input[inputmode=decimal]")].filter((el) => !["Từ", "Đến"].includes(el.getAttribute("aria-label")));
    const values = groupIndex === 0 ? ["11", "12", "13", "14", "21", "22", "23", "24"] : ["31", "32", "33", "34", "41", "42", "43", "44"];
    values.forEach((value, index) => { if (prices[index]) setValue(prices[index], value); });
    labels.push(...[...group.querySelectorAll("select[aria-label^='Loại hàng cột']")].map((el) => el.selectedOptions[0]?.textContent?.trim()));
  });
  document.dispatchEvent(new MouseEvent("mousedown", { bubbles: true }));
  const text = document.querySelector(".rate-declaration")?.innerText || "";
  return JSON.stringify({
    labels,
    hasRaw: /GENERAL|DRY_FOOD|COSMETICS|EXPRESS/.test(text),
    hasGt: />\\s*45/.test(text),
    hasGe: text.includes("≥ 45"),
    placeholder: document.querySelector("input[aria-label='Đến']")?.placeholder,
  });
`);
console.log("matrix", matrix);
await size(1366, 768);
await evaluate(`
  const title = [...document.querySelectorAll("h2")].find((el) => el.textContent.includes("nâng cao"));
  title?.scrollIntoView({ block: "start" });
  return "scrolled";
`);
await shot("advanced-1366x768");
await size(1920, 1080);
await evaluate(`
  const title = [...document.querySelectorAll("h2")].find((el) => el.textContent.includes("nâng cao"));
  title?.scrollIntoView({ block: "start" });
  return "scrolled";
`);
await shot("advanced-1920x1080");
browser.close();
child.kill();
