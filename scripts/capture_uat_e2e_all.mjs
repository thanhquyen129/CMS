import { spawn } from "node:child_process";
import { writeFileSync, mkdirSync } from "node:fs";
import { resolve } from "node:path";
import { setTimeout as delay } from "node:timers/promises";

const chrome = "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe";
const profile = "C:\\Users\\nguyenthanhbinh\\AppData\\Local\\Temp\\chrome-uat-real-session";
const outDir = resolve("docs/po/24102026-LCMS_RATE_VERSION_UI_01_DEV_Fix_v1.1/screenshots_uat");
mkdirSync(outDir, { recursive: true });

console.log("Starting Chrome with window-size 1440,960...");
const child = spawn(chrome, [
  "--headless=new",
  "--disable-gpu",
  "--remote-debugging-port=9444",
  `--user-data-dir=${profile}`,
  "--window-size=1440,960",
  "about:blank"
], { stdio: "ignore" });

let version;
for (let i = 0; i < 40; i++) {
  try {
    version = await (await fetch("http://127.0.0.1:9444/json/version")).json();
    break;
  } catch {
    await delay(250);
  }
}
if (!version) throw new Error("Chrome debugging port 9444 did not open.");

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
  if (message.method === "Page.javascriptDialogOpening") {
    send("Page.handleJavaScriptDialog", { accept: true }, message.sessionId).catch(() => {});
    return;
  }
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
await send("Network.enable", {}, sessionId);

async function evaluate(expression) {
  const result = await send("Runtime.evaluate", {
    expression,
    awaitPromise: true,
    returnByValue: true
  }, sessionId);
  if (result.exceptionDetails) {
    throw new Error("Evaluation error: " + JSON.stringify(result.exceptionDetails));
  }
  return result.result.value;
}

async function go(url) {
  await send("Page.navigate", { url }, sessionId);
  await delay(2500);
}

async function capture(filename) {
  await delay(600);
  const result = await send("Page.captureScreenshot", {
    format: "png",
    captureBeyondViewport: false
  }, sessionId);
  const filePath = resolve(outDir, filename);
  writeFileSync(filePath, Buffer.from(result.data, "base64"));
  console.log(`[Captured] ${filename} (${result.data.length} bytes base64)`);
}

// 1. Authenticate via BFF Login
console.log("=== Step 0: Login to https://cms.lsslogistics.vn ===");
await go("https://cms.lsslogistics.vn/login");
const loginRes = await evaluate(`
  (async () => {
    const res = await fetch("/bff/auth/login", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ email: "ops@cms.local", password: "LssAdmin2026!@#" })
    });
    return { ok: res.ok, status: res.status };
  })()
`);
console.log("BFF login result:", loginRes);

const cardId = "01a13820-4e67-793a-a293-e64636c62dc5";

// 2. Screenshot 01: Rate card detail published v1 with "Tạo phiên bản mới" CTA
console.log("=== Step 1: Capture Rate Card Detail (Published v1) ===");
await go(`https://cms.lsslogistics.vn/rate-cards/${cardId}`);
await delay(1000);
await capture("01_rate_card_detail_published_v1_with_new_version_cta_raw.png");

// 3. Screenshot 02: Appendices list
console.log("=== Step 2: Capture Appendices List ===");
await go("https://cms.lsslogistics.vn/rate-cards/appendices");
await delay(1000);
await capture("02_appendices_list_separated_cta_and_row_actions_raw.png");

// 4. Click "Tạo phiên bản mới" and capture Draft v2 edit form
console.log("=== Step 3: Click 'Tạo phiên bản mới' on rate card ===");
await go(`https://cms.lsslogistics.vn/rate-cards/${cardId}`);
await delay(1000);

const btnText = await evaluate(`
  (() => {
    const btn = document.querySelector('.rate-detail-toolbar button');
    return btn ? btn.innerText.trim() : null;
  })()
`);
console.log("Button found in toolbar:", btnText);

const clickResult = await evaluate(`
  (async () => {
    const buttons = [...document.querySelectorAll('.rate-detail-toolbar button')];
    const target = buttons.find(b => b.innerText.includes("Tạo phiên bản mới"));
    if (!target) return { error: "Button 'Tạo phiên bản mới' not found" };
    target.click();
    return { success: true };
  })()
`);
console.log("Click result:", clickResult);

// Wait for navigation to /edit
console.log("Waiting for navigation to edit form...");
await delay(4000);

const currentUrl = await evaluate("window.location.href");
console.log("Current URL after click:", currentUrl);
await capture("03_rate_card_v2_draft_edit_form_prefilled_raw.png");

// 5. Navigate back to rate card detail to capture v2 draft presence
console.log("=== Step 4: Capture Rate Card Detail showing v2 draft and v1 published ===");
await go(`https://cms.lsslogistics.vn/rate-cards/${cardId}`);
await delay(1500);
await capture("04_rate_card_detail_with_v2_draft_raw.png");

browser.close();
child.kill();
console.log("=== All raw screenshots captured successfully! ===");
