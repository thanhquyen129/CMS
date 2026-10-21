import { spawn } from "node:child_process";
import { mkdir, writeFile } from "node:fs/promises";
import { setTimeout as delay } from "node:timers/promises";

const chrome = "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe";
const outDir = "c:\\A1\\git\\cms\\docs\\po\\rate-ac-evidence\\polish";
const profile = process.env.TEMP + "\\lcms-chrome-polish3";
const cardId = "01a13a48-c876-7661-8303-ac7ca707d696";
await mkdir(outDir, { recursive: true });
const child = spawn(chrome, ["--headless=new", "--disable-gpu", "--remote-debugging-port=9227", `--user-data-dir=${profile}`, "about:blank"], { stdio: "ignore" });
let version;
for (let i = 0; i < 40; i++) {
  try { version = await (await fetch("http://127.0.0.1:9227/json/version")).json(); break; } catch { await delay(250); }
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
  await delay(200);
}
async function shot(name) {
  await evaluate(`const portal = document.querySelector("nextjs-portal"); if (portal) portal.style.display = "none"; return "ok";`);
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
await evaluate(`
  ${setHelpers}
  setValue(document.querySelector("#code"), "POLISH-VIEW");
  setValue(document.querySelector("#name"), "Cước SGN → HAN");
  const route = pickOption(document.querySelector("[aria-label='Chọn tuyến 1']"), "SGN → HAN");
  setValue(document.querySelector("[aria-label='Cách tính 1']"), "unit_rate");
  setValue(document.querySelector("[aria-label='Loại trọng lượng 1']"), "gross");
  setValue(document.querySelector("[aria-label='Trọng lượng từ 1']"), "0");
  setValue(document.querySelector("[aria-label='Trọng lượng đến 1']"), "45");
  const amount = document.querySelector(".col-amount input");
  setValue(amount, "120000");
  setValue(document.querySelector("[aria-label='Mã quy tắc 1']"), "FREIGHT");
  setValue(document.querySelector("[aria-label='Tên quy tắc 1']"), "Cước chính");
  const carrier = document.querySelector(".rate-rules .party-typeahead input[type=search]");
  carrier.focus();
  setValue(carrier, "Vietnam");
  return route;
`);
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
async function focusRules() {
  await evaluate(`
    const section = [...document.querySelectorAll(".section-title")].find((el) => el.textContent.includes("Bảng giá cơ bản"));
    section?.scrollIntoView({ block: "start" });
    const wrap = document.querySelector(".rate-declaration .table-wrap");
    const routeHeader = [...document.querySelectorAll(".rate-rules th")].find((el) => el.textContent.trim() === "Tuyến");
    if (wrap && routeHeader) wrap.scrollLeft = routeHeader.offsetLeft;
    const amount = document.querySelector(".col-amount");
    const helper = document.querySelector(".col-amount .muted");
    return JSON.stringify({
      scroll: wrap ? wrap.scrollLeft : null,
      client: wrap ? wrap.clientWidth : null,
      route: document.querySelector("[aria-label='Chọn tuyến 1']")?.selectedOptions?.[0]?.textContent?.trim(),
      carrier: document.querySelector(".rate-rules .party-typeahead input[type=search]")?.value,
      helper: helper?.textContent?.trim(),
      helperRight: helper ? Math.round(helper.getBoundingClientRect().right) : null,
      wrapRight: wrap ? Math.round(wrap.getBoundingClientRect().right) : null,
      listOpen: Boolean(document.querySelector(".party-typeahead-list")),
    });
  `);
}
await size(1366, 768);
console.log("fit1366", await focusRules());
await shot("basic-1366x768");
await size(1920, 1080);
console.log("fit1920", await focusRules());
await shot("basic-1920x1080");
if (process.argv.includes("--basic")) {
  browser.close();
  child.kill();
  process.exit(0);
}

await go(`http://127.0.0.1:3000/rate-cards/${cardId}`);
await delay(800);
const detail = await evaluate(`
  const table = [...document.querySelectorAll("table")].find((el) => el.textContent.includes("FREIGHT") || el.textContent.includes("Cước"));
  table?.scrollIntoView({ block: "center" });
  return document.body.innerText.slice(0, 500);
`);
console.log("detail", detail.replace(/\\s+/g, " ").slice(0, 400));
await size(1366, 768);
await shot("detail-1366x768");
await size(1920, 1080);
await shot("detail-1920x1080");

await go("http://127.0.0.1:3000/rate-cards/new");
await evaluate(`
  const advanced = [...document.querySelectorAll("input[name=declarationMode]")].find((el) => el.parentElement.textContent.includes("nâng cao"));
  advanced?.click();
  return "mode";
`);
await delay(300);
await evaluate(`
  [...document.querySelectorAll("button")].find((el) => el.textContent.trim() === "Thêm nhóm")?.click();
  return "added";
`);
await delay(200);
await evaluate(`
  ${setHelpers}
  const routes = [...document.querySelectorAll("select[id^='group-route-']")];
  const texts = [pickOption(routes[0], "SGN → HAN"), pickOption(routes[1], "HAN → DAD")];
  const carriers = [...document.querySelectorAll(".rate-block-rules .party-typeahead input[type=search]")];
  setValue(carriers[0], "Vietnam");
  setValue(carriers[1], "Vietjet");
  return texts.join(" | ");
`);
for (let n = 0; n < 20; n++) {
  await delay(200);
  const left = await evaluate(`
    const lists = [...document.querySelectorAll(".rate-block-rules .party-typeahead")];
    let waiting = 0;
    for (const root of lists) {
      if (root.querySelector("input[type=search]").value.includes("—")) continue;
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
  await delay(100);
}
await evaluate(`
  const setValue = (el, value) => {
    Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "value").set.call(el, value);
    el.dispatchEvent(new Event("input", { bubbles: true }));
    el.dispatchEvent(new Event("change", { bubbles: true }));
  };
  [...document.querySelectorAll("section.rate-block-rules")].forEach((group, groupIndex) => {
    const mins = [...group.querySelectorAll("input[aria-label='Từ']")];
    const maxes = [...group.querySelectorAll("input[aria-label='Đến']")];
    if (mins[0]) setValue(mins[0], "0");
    if (maxes[0]) setValue(maxes[0], "45");
    if (mins[1]) setValue(mins[1], "45");
    if (maxes[1]) setValue(maxes[1], "");
    const prices = [...group.querySelectorAll("tbody input[inputmode=decimal]")].filter((el) => !["Từ", "Đến"].includes(el.getAttribute("aria-label")));
    (groupIndex === 0 ? ["11", "21"] : ["31", "41"]).forEach((value, index) => { if (prices[index]) setValue(prices[index], value); });
  });
  document.dispatchEvent(new MouseEvent("mousedown", { bubbles: true }));
  return "ready";
`);
await size(1366, 768);
await evaluate(`
  const second = [...document.querySelectorAll("h3")].find((el) => el.textContent.includes("2"));
  second?.scrollIntoView({ block: "center" });
  return "scrolled";
`);
await shot("advanced-1366x768");
await size(1920, 1080);
await evaluate(`
  const title = [...document.querySelectorAll("h2")].find((el) => el.textContent.includes("nâng cao"));
  title?.scrollIntoView({ block: "start" });
  return "top";
`);
await shot("advanced-1920x1080");
browser.close();
child.kill();
