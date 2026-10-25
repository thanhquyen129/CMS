import { spawn } from "node:child_process";
import { writeFileSync, mkdirSync } from "node:fs";
import { resolve } from "node:path";
import { setTimeout as delay } from "node:timers/promises";

const chrome = "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe";
const profile = "C:\\Users\\nguyenthanhbinh\\AppData\\Local\\Temp\\chrome-uat-real-session";

console.log("Starting Chrome...");
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
  await delay(2000);
}

// Login
console.log("Navigating to login...");
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

// Rate card detail
console.log("Navigating to rate card...");
await go("https://cms.lsslogistics.vn/rate-cards/01a13820-4e67-793a-a293-e64636c62dc5");
await delay(2000);

const pageInfo = await evaluate(`
  (() => {
    const buttons = [...document.querySelectorAll('button, a.btn')].map(b => b.innerText.trim());
    const sidebarLinks = [...document.querySelectorAll('.sidebar a')].map(a => a.innerText.trim());
    return {
      title: document.title,
      buttons,
      hasCreateVersion: buttons.includes("Tạo phiên bản mới"),
      sidebarLinks: sidebarLinks.filter(l => l.includes("phụ lục") || l.includes("Lịch sử"))
    };
  })()
`);
console.log("Page info:", pageInfo);

// Screenshot
const { data } = await send("Page.captureScreenshot", { format: "png" }, sessionId);
writeFileSync("test_uat_capture.png", Buffer.from(data, "base64"));
console.log("Saved test_uat_capture.png successfully!");

browser.close();
child.kill();
