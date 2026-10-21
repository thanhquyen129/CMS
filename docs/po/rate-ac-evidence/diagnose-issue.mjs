import { spawn } from "node:child_process";
import { setTimeout as delay } from "node:timers/promises";

const chrome = "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe";
const profile = process.env.TEMP + "\\lcms-chrome-polish";
const child = spawn(chrome, [
  "--headless=new",
  "--disable-gpu",
  "--remote-debugging-port=9225",
  `--user-data-dir=${profile}`,
  "--window-size=1366,768",
  "about:blank",
], { stdio: "ignore" });

let version;
for (let i = 0; i < 40; i++) {
  try {
    version = await (await fetch("http://127.0.0.1:9225/json/version")).json();
    break;
  } catch {
    await delay(250);
  }
}
if (!version) throw new Error("Chrome debugging port did not open.");

const browser = new WebSocket(version.webSocketDebuggerUrl);
const pending = new Map();
const logs = [];
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
  if (message.method === "Runtime.consoleAPICalled" || message.method === "Runtime.exceptionThrown" || message.method === "Log.entryAdded") {
    logs.push(message);
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
await send("Log.enable", {}, sessionId);

async function evaluate(expression) {
  const result = await send("Runtime.evaluate", { expression, awaitPromise: true, returnByValue: true }, sessionId);
  if (result.exceptionDetails) throw new Error(JSON.stringify(result.exceptionDetails));
  return result.result.value;
}
async function go(url) {
  await send("Page.navigate", { url }, sessionId);
  await delay(1800);
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
await delay(2500);
await go("http://127.0.0.1:3000/rate-cards/new");
await delay(2000);

const probe = await evaluate(`(() => {
  const portal = document.querySelector("nextjs-portal");
  const text = portal?.shadowRoot ? portal.shadowRoot.textContent : "";
  const nodes = [...(portal?.shadowRoot?.querySelectorAll("button, a, [role='status'], [data-next-badge]") ?? [])]
    .map((el) => el.innerText.trim())
    .filter(Boolean)
    .slice(0, 20);
  return { href: location.href, portal: Boolean(portal), nodes, textLen: (text || "").length };
})()`);
console.log(JSON.stringify(probe, null, 2));
const interesting = logs.filter((entry) => entry.method !== "Runtime.consoleAPICalled" || !String(entry.params?.args?.[0]?.value ?? "").includes("Fast Refresh"));
console.log(JSON.stringify(interesting.map((entry) => ({
  method: entry.method,
  type: entry.params?.type,
  text: entry.params?.exceptionDetails?.exception?.description
    ?? entry.params?.exceptionDetails?.text
    ?? entry.params?.entry?.text
    ?? entry.params?.entry?.url
    ?? entry.params?.args?.map((a) => a.value ?? a.description).join(" | "),
  url: entry.params?.entry?.url,
  stack: entry.params?.exceptionDetails?.stackTrace?.callFrames?.slice(0, 8).map((f) => `${f.functionName} ${f.url}:${f.lineNumber}`),
})), null, 2));
browser.close();
child.kill();
