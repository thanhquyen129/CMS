import { spawn } from "node:child_process";
import { mkdir, writeFile } from "node:fs/promises";
import { setTimeout as delay } from "node:timers/promises";

const chrome = "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe";
const outDir = "c:\\A1\\git\\cms\\docs\\po\\rate-ac-evidence\\polish";
const profile = process.env.TEMP + "\\lcms-chrome-polish2";
await mkdir(outDir, { recursive: true });

const child = spawn(chrome, [
  "--headless=new",
  "--disable-gpu",
  "--remote-debugging-port=9226",
  `--user-data-dir=${profile}`,
  "--window-size=1366,768",
  "about:blank",
], { stdio: "ignore" });

let version;
for (let i = 0; i < 40; i++) {
  try {
    version = await (await fetch("http://127.0.0.1:9226/json/version")).json();
    break;
  } catch {
    await delay(250);
  }
}
if (!version) throw new Error("Chrome debugging port did not open.");

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
  const result = await send("Runtime.evaluate", {
    expression: `(() => {\n${expression}\n})()`,
    awaitPromise: true,
    returnByValue: true,
  }, sessionId);
  if (result.exceptionDetails) throw new Error(JSON.stringify(result.exceptionDetails).slice(0, 2000));
  return result.result.value;
}
async function go(url) {
  await send("Page.navigate", { url }, sessionId);
  await delay(1600);
}
async function size(width, height) {
  await send("Emulation.setDeviceMetricsOverride", { width, height, deviceScaleFactor: 1, mobile: false }, sessionId);
  await evaluate(`document.documentElement.style.zoom = "1"; "ok"`);
  await delay(250);
}
async function shot(name) {
  await evaluate(`const portal = document.querySelector("nextjs-portal"); if (portal) portal.style.display = "none"; return "hidden";`);
  const image = await send("Page.captureScreenshot", { format: "png", fromSurface: true }, sessionId);
  await writeFile(`${outDir}\\${name}.png`, Buffer.from(image.data, "base64"));
}

const setHelpers = `
  const setValue = (el, value) => {
    const proto = el instanceof HTMLSelectElement ? HTMLSelectElement.prototype : HTMLInputElement.prototype;
    const setter = Object.getOwnPropertyDescriptor(proto, "value").set;
    setter.call(el, value);
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
`);
await delay(2200);

await go("http://127.0.0.1:3000/rate-cards/new");
await delay(1200);
const basicFill = await evaluate(`
  ${setHelpers}
  setValue(document.querySelector("#code"), "POLISH-SGN-HAN");
  setValue(document.querySelector("#name"), "Cước SGN → HAN");
  const route = document.querySelector("[aria-label='Chọn tuyến 1']");
  const routeText = pickOption(route, "SGN → HAN");
  setValue(document.querySelector("[aria-label='Cách tính 1']"), "unit_rate");
  setValue(document.querySelector("[aria-label='Loại trọng lượng 1']"), "gross");
  setValue(document.querySelector("[aria-label='Trọng lượng từ 1']"), "0");
  setValue(document.querySelector("[aria-label='Trọng lượng đến 1']"), "45");
  const amount = document.querySelector("[aria-label^='Đơn giá'], [aria-label^='Số tiền']");
  setValue(amount, "120000");
  setValue(document.querySelector("[aria-label='Mã quy tắc 1']"), "FREIGHT");
  setValue(document.querySelector("[aria-label='Tên quy tắc 1']"), "Cước chính");
  const carrier = document.querySelector(".rate-rules .party-typeahead input[type=search]");
  carrier.focus();
  setValue(carrier, "Vietnam");
  return routeText;
`);
await delay(900);
const carrierPick = await evaluate(`
  const button = [...document.querySelectorAll(".rate-rules .party-typeahead-list button")].find((el) => el.textContent.includes("Vietnam Airlines"));
  if (!button) return [...document.querySelectorAll(".rate-rules .party-typeahead-list li")].map((el) => el.textContent.trim()).join(" | ") || "no-list";
  button.click();
  return "picked";
`);
console.log("basic", basicFill, carrierPick);
await evaluate(`
  const section = [...document.querySelectorAll(".section-title")].find((el) => el.textContent.includes("Bảng giá cơ bản"));
  section?.scrollIntoView({ block: "start" });
  const wrap = document.querySelector(".rate-declaration .table-wrap");
  const routeHeader = [...document.querySelectorAll(".rate-rules th")].find((el) => el.textContent.trim() === "Tuyến");
  if (wrap && routeHeader) wrap.scrollLeft = routeHeader.offsetLeft;
  return "ok";
`);
await delay(300);

async function audit(label) {
  const report = await evaluate(`return (() => {
    const bar = document.querySelector("form.rate-declaration > .cta-row");
    const box = (el) => {
      if (!el) return null;
      const r = el.getBoundingClientRect();
      return { top: Math.round(r.top), left: Math.round(r.left), right: Math.round(r.right), bottom: Math.round(r.bottom), w: Math.round(r.width), h: Math.round(r.height) };
    };
    const overlaps = (a, b) => a && b && !(a.right <= b.left || a.left >= b.right || a.bottom <= b.top || a.top >= b.bottom);
    const barBox = box(bar);
    const covered = [...document.querySelectorAll(".rate-declaration input, .rate-declaration select, .rate-declaration label.panel")]
      .filter((el) => !bar.contains(el))
      .map((el) => ({ label: el.getAttribute("aria-label") || el.id || el.textContent.trim().slice(0, 40), box: box(el) }))
      .filter((item) => item.box && item.box.w > 0 && item.box.h > 0 && overlaps(barBox, item.box))
      .map((item) => item.label);
    const helper = document.querySelector(".col-amount .muted");
    const portal = document.querySelector("nextjs-portal");
    const issue = [...(portal?.shadowRoot?.querySelectorAll("button") ?? [])].map((el) => el.innerText.replace(/\\s+/g, " ").trim()).filter(Boolean);
    const headers = [...document.querySelectorAll(".rate-rules th")].map((el) => el.textContent.trim());
    const wrap = document.querySelector(".rate-declaration .table-wrap");
    return {
      href: location.href,
      issue,
      covered,
      helper: helper ? helper.textContent.trim() : null,
      helperClipped: helper ? helper.scrollWidth > helper.clientWidth + 1 : null,
      route: document.querySelector("[aria-label='Chọn tuyến 1']")?.selectedOptions?.[0]?.textContent?.trim() ?? null,
      groups: [...document.querySelectorAll("h3.section-title")].map((el) => el.textContent.trim()),
      headers,
      wrap: wrap ? { scrollWidth: wrap.scrollWidth, clientWidth: wrap.clientWidth, scrollLeft: wrap.scrollLeft } : null,
      error: document.querySelector(".alert-error")?.textContent?.trim() ?? null,
    };
  })()`);
  console.log(label, JSON.stringify(report));
  return report;
}

async function focusRules() {
  await evaluate(`
    const section = [...document.querySelectorAll(".section-title")].find((el) => el.textContent.includes("Bảng giá cơ bản"));
    section?.scrollIntoView({ block: "start" });
    const wrap = document.querySelector(".rate-declaration .table-wrap");
    const routeHeader = [...document.querySelectorAll(".rate-rules th")].find((el) => el.textContent.trim() === "Tuyến");
    if (wrap && routeHeader) wrap.scrollLeft = routeHeader.offsetLeft;
    const amount = document.querySelector(".col-amount");
    if (wrap && amount) {
      const right = amount.offsetLeft + amount.offsetWidth;
      const visibleRight = wrap.scrollLeft + wrap.clientWidth;
      if (right > visibleRight - 8) wrap.scrollLeft += right - visibleRight + 12;
    }
    return "ok";
  `);
}

await size(1366, 768);
await focusRules();
await audit("basic-1366");
await shot("basic-1366x768");
await size(1920, 1080);
await focusRules();
await audit("basic-1920");
await shot("basic-1920x1080");
await size(1366, 768);
await evaluate(`document.documentElement.style.zoom = "1.25"; "ok"`);
await delay(200);
const zoom = await audit("basic-zoom125");
await writeFile(`${outDir}\\zoom125.json`, JSON.stringify(zoom, null, 2));
await shot("basic-1366-zoom125");
await evaluate(`document.documentElement.style.zoom = "1"; "ok"`);

const submitted = await evaluate(`
  const form = document.querySelector("form.rate-declaration");
  form.requestSubmit();
  return "submitted";
`);
console.log(submitted);
let afterSave = "";
for (let i = 0; i < 20; i++) {
  await delay(400);
  afterSave = await evaluate(`return location.href + " | " + (document.querySelector(".alert-error")?.textContent?.trim() ?? "")`);
  if (!afterSave.includes("/rate-cards/new")) break;
  if (afterSave.includes("|") && afterSave.split("|")[1].trim()) break;
}
console.log("after-save", afterSave);
const detailText = await evaluate(`
  const rules = [...document.querySelectorAll("h2, h3, table")].map((el) => el.textContent.trim().slice(0, 80));
  const table = [...document.querySelectorAll("table")].find((el) => el.textContent.includes("FREIGHT") || el.textContent.includes("Cước"));
  table?.scrollIntoView({ block: "start" });
  return rules.slice(0, 8).join(" || ");
`);
console.log("detail-text", detailText);
await delay(400);
await size(1366, 768);
await shot("detail-1366x768");
await size(1920, 1080);
await shot("detail-1920x1080");
await audit("detail");

await go("http://127.0.0.1:3000/rate-cards/new");
await delay(1000);
await evaluate(`
  document.querySelector("input[name=declarationMode][value=advanced], input[name=declarationMode]:not(:checked)")?.click();
  const advanced = [...document.querySelectorAll("input[name=declarationMode]")].find((el) => el.parentElement.textContent.includes("nâng cao"));
  advanced?.click();
  "ok";
`);
await delay(400);
await evaluate(`
  const add = [...document.querySelectorAll("button")].find((el) => el.textContent.trim() === "Thêm nhóm");
  add?.click();
  "ok";
`);
await delay(300);
const advancedFill = await evaluate(`
  ${setHelpers}
  const trims = (selector) => [...document.querySelectorAll(selector)];
  const routes = trims("select[id^='group-route-']");
  const routeTexts = [pickOption(routes[0], "SGN → HAN"), pickOption(routes[1], "HAN → DAD")];
  const carriers = trims(".rate-block-rules .party-typeahead input[type=search]");
  setValue(carriers[0], "Vietnam");
  setValue(carriers[1], "Vietjet");
  return JSON.stringify({ routes: routeTexts, carriers: carriers.length, titles: [...document.querySelectorAll("h3")].map((el) => el.textContent.trim()) });
`);
console.log("advanced-fill", advancedFill);
await delay(900);
const advancedPick = await evaluate(`
  const lists = [...document.querySelectorAll(".rate-block-rules .party-typeahead")];
  const clicks = lists.map((root) => {
    const button = [...root.querySelectorAll("button")].find((el) => el.closest(".party-typeahead-list"));
    const text = button?.textContent?.trim() ?? "";
    button?.click();
    return text.slice(0, 80);
  });
  return clicks;
`);
console.log("advanced-pick", advancedPick);
for (let step = 0; step < 16; step++) {
  const clicked = await evaluate(`
    const groups = [...document.querySelectorAll("section.rate-block-rules")];
    let clicks = 0;
    for (const group of groups) {
      if (group.querySelectorAll("tbody tr").length <= 2) continue;
      const button = [...group.querySelectorAll("tbody tr button")].find((el) => el.textContent.trim() === "Xóa");
      if (button) { button.click(); clicks += 1; }
    }
    return clicks;
  `);
  if (!clicked) break;
  await delay(120);
}
const matrixShape = await evaluate(`
  const setValue = (el, value) => {
    const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "value").set;
    setter.call(el, value);
    el.dispatchEvent(new Event("input", { bubbles: true }));
    el.dispatchEvent(new Event("change", { bubbles: true }));
  };
  const groups = [...document.querySelectorAll("section.rate-block-rules")];
  groups.forEach((group, groupIndex) => {
    const mins = [...group.querySelectorAll("input[aria-label='Từ']")];
    const maxes = [...group.querySelectorAll("input[aria-label='Đến']")];
    if (mins[0]) setValue(mins[0], "0");
    if (maxes[0]) setValue(maxes[0], "45");
    if (mins[1]) setValue(mins[1], "45");
    if (maxes[1]) setValue(maxes[1], "");
    const prices = [...group.querySelectorAll("tbody input[inputmode=decimal]")].filter((el) => el.getAttribute("aria-label") !== "Từ" && el.getAttribute("aria-label") !== "Đến");
    const seed = groupIndex === 0 ? ["11", "21"] : ["31", "41"];
    seed.forEach((value, index) => { if (prices[index]) setValue(prices[index], value); });
  });
  const title = [...document.querySelectorAll("h2")].find((el) => el.textContent.includes("nâng cao"));
  title?.scrollIntoView({ block: "start" });
  return JSON.stringify({
    rows: groups.map((el) => el.querySelectorAll("tbody tr").length),
    titles: [...document.querySelectorAll("h3.section-title")].map((el) => el.textContent.trim()),
  });
`);
console.log("matrix", matrixShape);
await delay(300);
await size(1366, 768);
await audit("advanced-1366");
await shot("advanced-1366x768");
await size(1920, 1080);
await evaluate(`
  const title = [...document.querySelectorAll("h2")].find((el) => el.textContent.includes("nâng cao"));
  title?.scrollIntoView({ block: "start" });
  "ok";
`);
await audit("advanced-1920");
await shot("advanced-1920x1080");

browser.close();
child.kill();
