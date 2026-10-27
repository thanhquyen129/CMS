import { spawn } from "node:child_process";
import { writeFileSync, mkdirSync } from "node:fs";
import { resolve } from "node:path";
import { setTimeout as delay } from "node:timers/promises";

const chrome = "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe";
const profile = "C:\\Users\\nguyenthanhbinh\\AppData\\Local\\Temp\\chrome-real-uat-clean";
const outDir = resolve("docs/po/26102026-LCMS_UAT_Followup_DEV_Package_v1.0_20261026/screenshots");
mkdirSync(outDir, { recursive: true });

console.log("Starting Chrome for real live Next.js screenshots...");
const child = spawn(chrome, [
  "--headless=new",
  "--disable-gpu",
  "--remote-debugging-port=9246",
  `--user-data-dir=${profile}`,
  "--window-size=1440,960",
  "about:blank"
], { stdio: "ignore" });

let version;
for (let i = 0; i < 40; i++) {
  try {
    version = await (await fetch("http://127.0.0.1:9246/json/version")).json();
    break;
  } catch {
    await delay(250);
  }
}
if (!version) throw new Error("Chrome debugging port 9246 did not open.");

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

async function hideDevOverlay() {
  await evaluate(`
    (() => {
      let style = document.getElementById("hide-next-portal");
      if (!style) {
        style = document.createElement("style");
        style.id = "hide-next-portal";
        style.innerHTML = "nextjs-portal { display: none !important; }";
        document.head.appendChild(style);
      }
      document.querySelectorAll("nextjs-portal").forEach(el => el.remove());
    })()
  `);
}

async function go(url) {
  await send("Page.navigate", { url }, sessionId);
  await delay(2500);
  await hideDevOverlay();
}

async function captureScreenshot(filename, clip = null) {
  await hideDevOverlay();
  await delay(400);
  const params = { format: "png", captureBeyondViewport: false };
  if (clip) params.clip = clip;
  const result = await send("Page.captureScreenshot", params, sessionId);
  const filePath = resolve(outDir, filename);
  writeFileSync(filePath, Buffer.from(result.data, "base64"));
  console.log(`>>> CAPTURED REAL SCREENSHOT: ${filename}`);
}

async function captureFullPage(filename) {
  await hideDevOverlay();
  await delay(400);
  const scrollHeight = await evaluate(`Math.max(document.body.scrollHeight, document.documentElement.scrollHeight, 1100)`);
  await send("Emulation.setDeviceMetricsOverride", {
    width: 1440,
    height: Math.min(Math.ceil(scrollHeight), 3000),
    deviceScaleFactor: 1,
    mobile: false
  }, sessionId);
  await delay(500);
  await hideDevOverlay();

  const result = await send("Page.captureScreenshot", {
    format: "png",
    captureBeyondViewport: true
  }, sessionId);
  const filePath = resolve(outDir, filename);
  writeFileSync(filePath, Buffer.from(result.data, "base64"));
  console.log(`>>> CAPTURED FULL-PAGE REAL SCREENSHOT: ${filename}`);

  await send("Emulation.setDeviceMetricsOverride", {
    width: 1440,
    height: 960,
    deviceScaleFactor: 1,
    mobile: false
  }, sessionId);
  await delay(200);
}

try {
  // 1. AUTHENTICATE
  console.log("Logging into Next.js app as admin@cms.local...");
  await go("http://localhost:3000/login");
  await evaluate(`
    (async () => {
      const res = await fetch("/bff/auth/login", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email: "admin@cms.local", password: "abc123" })
      });
      return res.status;
    })()
  `);
  await delay(1000);

  const billId = "01a16cf7-30d8-731c-a50f-928fc7aa575b";
  const costFreightId = "01a16dd7-c111-78aa-a9c8-509e0224a2cf";
  const costSurchargeId = "01a16dd7-c222-78aa-a9c8-509e0224a2cf";

  // SCREENSHOT 01: Service Type Dropdown in Bill Edit
  console.log("Navigating to Bill details for Screenshot 01...");
  await go(`http://localhost:3000/bills/${billId}`);
  await delay(1500);

  // Poll until the "Sửa thông tin Bill" button arrives via client-side fetch, then click it
  const editOpened = await evaluate(`
    (async () => {
      for (let i = 0; i < 40; i++) {
        const btn = [...document.querySelectorAll("button")].find(b => b.innerText.includes("Sửa thông tin"));
        if (btn && !btn.disabled) {
          btn.click();
          return true;
        }
        await new Promise(r => setTimeout(r, 250));
      }
      return false;
    })()
  `);
  console.log("Edit button clicked:", editOpened);
  await delay(1200);

  // Wait for the form inputs to appear and scroll into view
  await evaluate(`
    (() => {
      const sel = document.querySelector("select[name='service_type_code']") || document.querySelector(".opref-group") || document.querySelector(".opref-editor");
      if (sel) sel.scrollIntoView({ behavior: "instant", block: "center" });
    })()
  `);
  await captureScreenshot("01_service_type_dropdown_bill_edit.png");

  // SCREENSHOT 02: Rating v2 Eligible vs v1 Weight Mismatch
  console.log("Navigating to Bill Rating tab for Screenshot 02...");
  await go(`http://localhost:3000/bills/${billId}?tab=rating`);
  await delay(1500);
  // Wait for the rating form
  await evaluate(`
    (async () => {
      for (let i = 0; i < 40; i++) {
        const form = document.querySelector("#bill-rating");
        if (form) {
          form.scrollIntoView({ behavior: "instant", block: "start" });
          return true;
        }
        await new Promise(r => setTimeout(r, 250));
      }
      return false;
    })()
  `);
  await captureScreenshot("02_rating_v2_eligible_v1_weight_mismatch.png");

  // SCREENSHOT 03: BUY Rating history with itemized VAT breakdown and ONLY Seed Cost button
  console.log("Capturing Rating Breakdown and History for Screenshot 03...");
  await evaluate(`
    (() => {
      const vatHeading = [...document.querySelectorAll("h3")].find(h => h.innerText.includes("VAT Disclosure") || h.innerText.includes("Bóc tách Thuế"));
      if (vatHeading) {
        vatHeading.scrollIntoView({ behavior: "instant", block: "start" });
      } else {
        const table = document.querySelector(".data-table");
        if (table) table.scrollIntoView({ behavior: "instant", block: "center" });
      }
    })()
  `);
  await captureScreenshot("03_rating_buy_history_seed_cost_only_with_vat_breakdown.png");

  // SCREENSHOT 04: Cost Detail Freight Rating Trace and VAT Disclosure
  console.log("Navigating to Freight Cost Detail for Screenshot 04...");
  await go(`http://localhost:3000/costs/${costFreightId}`);
  await delay(1500);
  await captureFullPage("04_cost_detail_freight_rating_trace_and_vat_disclosure.png");

  // SCREENSHOT 05: Cost Detail Surcharge Independent Trace
  console.log("Navigating to Surcharge Cost Detail for Screenshot 05...");
  await go(`http://localhost:3000/costs/${costSurchargeId}`);
  await delay(1500);
  await captureFullPage("05_cost_detail_surcharge_independent_trace.png");

  // SCREENSHOT 06: Rate Card Declaration Form DD/MM/YYYY Date Input
  console.log("Navigating to Rate Card Declaration for Screenshot 06...");
  await go("http://localhost:3000/rate-cards/declare");
  await delay(1500);
  await evaluate(`
    (() => {
      const dateInp = document.querySelector("[name='effectiveFrom']") || document.querySelector("input[placeholder='DD/MM/YYYY']");
      if (dateInp) {
        dateInp.value = "20/10/2026";
        dateInp.dispatchEvent(new Event("input", { bubbles: true }));
        dateInp.scrollIntoView({ behavior: "instant", block: "center" });
      }
    })()
  `);
  await captureScreenshot("06_rate_card_declaration_form_dd_mm_yyyy_date_input.png");

  // SCREENSHOT 07: Rating BUY After Seed Idempotent (No Seed Revenue)
  console.log("Navigating to Bill Rating tab after seed for Screenshot 07...");
  await go(`http://localhost:3000/bills/${billId}?tab=rating`);
  await delay(1500);
  // Click Seed Cost button
  const seedClicked = await evaluate(`
    (async () => {
      for (let i = 0; i < 40; i++) {
        const btn = [...document.querySelectorAll("button")].find(b => b.innerText.includes("Seed chi phí"));
        if (btn) {
          btn.click();
          return true;
        }
        await new Promise(r => setTimeout(r, 250));
      }
      return false;
    })()
  `);
  console.log("Seed button clicked:", seedClicked);
  await delay(2000);
  await evaluate(`
    (() => {
      const table = document.querySelector(".data-table") || document.querySelector("#bill-rating");
      if (table) table.scrollIntoView({ behavior: "instant", block: "center" });
    })()
  `);
  await captureScreenshot("07_rating_buy_after_seed_idempotent_no_seed_revenue.png");

  console.log("\n>>> ALL 7 REAL SCREENSHOTS CAPTURED DIRECTLY FROM REAL NEXT.JS APP!");
} catch (err) {
  console.error("Error during real screenshot capture:", err);
  process.exitCode = 1;
} finally {
  browser.close();
  child.kill();
}
