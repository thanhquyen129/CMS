import { spawn, execSync } from "node:child_process";
import { writeFileSync, mkdirSync } from "node:fs";
import { resolve } from "node:path";
import { setTimeout as delay } from "node:timers/promises";

function insertConflictRule(cardId) {
  execSync(`python scripts/insert_conflict_rule.py ${cardId}`);
}

function removeConflictRule() {
  execSync("python scripts/remove_conflict_rule.py");
}

const chrome = "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe";
const profile = "C:\\Users\\nguyenthanhbinh\\AppData\\Local\\Temp\\chrome-real-e2e-final";
const screenshotsDir = resolve("docs/po/24102026-LCMS_RATE_VERSION_UI_01_DEV_Fix_v1.1/screenshots");
mkdirSync(screenshotsDir, { recursive: true });

console.log("Starting Chrome CDP session for 7 Real Screenshots...");
const child = spawn(chrome, [
  "--headless=new",
  "--disable-gpu",
  "--remote-debugging-port=9234",
  `--user-data-dir=${profile}`,
  "--window-size=1366,768",
  "about:blank"
], { stdio: "ignore" });

let version;
for (let i = 0; i < 40; i++) {
  try {
    version = await (await fetch("http://127.0.0.1:9234/json/version")).json();
    break;
  } catch {
    await delay(250);
  }
}
if (!version) throw new Error("Chrome debugging port 9234 did not open.");

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
    console.log(`[CDP Event] Auto-accepting dialog: "${message.params.message}" (${message.params.type})`);
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

// Override dialogs in browser context
await send("Page.addScriptToEvaluateOnNewDocument", {
  source: `
    window.confirm = () => true;
    window.alert = () => true;
    window.prompt = () => "";
  `
}, sessionId);

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

async function captureScreenshot(filename) {
  const result = await send("Page.captureScreenshot", {
    format: "png",
    captureBeyondViewport: false
  }, sessionId);
  const filePath = resolve(screenshotsDir, filename);
  writeFileSync(filePath, Buffer.from(result.data, "base64"));
  console.log(`>>> SAVED REAL SCREENSHOT: ${filename} (${result.data.length} bytes base64)`);
}

async function waitForSelector(selector, timeoutMs = 15000) {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    const found = await evaluate(`Boolean(document.querySelector(${JSON.stringify(selector)}))`);
    if (found) return true;
    await delay(300);
  }
  throw new Error(`Timeout waiting for selector: ${selector}`);
}

async function waitForText(text, timeoutMs = 15000) {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    const found = await evaluate(`document.body.innerText.includes(${JSON.stringify(text)})`);
    if (found) return true;
    await delay(300);
  }
  throw new Error(`Timeout waiting for text: ${text}`);
}

try {
  // 1. AUTHENTICATE VIA BFF LOGIN TO ESTABLISH COOKIES
  console.log("\n==========================================");
  console.log("STEP 0: AUTHENTICATE VIA BFF LOGIN");
  console.log("==========================================");
  await go("http://localhost:3000/login");
  await delay(1000);
  const loginRes = await evaluate(`
    (async () => {
      const res = await fetch("/bff/auth/login", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email: "admin@cms.local", password: "abc123" })
      });
      return { ok: res.ok, status: res.status };
    })()
  `);
  console.log("BFF Login result:", loginRes);

  const cardId = "01a16cf7-3b64-7606-8b4d-2158cea9d954";

  // 2. SCREENSHOT 01: RATE CARD DETAIL (PUBLISHED v1 WITH NEW VERSION CTA)
  console.log("\n==========================================");
  console.log("STEP 1: SCREENSHOT 01 — RATE CARD DETAIL (PUBLISHED v1)");
  console.log("==========================================");
  await go(`http://localhost:3000/rate-cards/${cardId}`);
  await waitForText("TDNT_SGN_HAN");
  await waitForSelector(".rate-detail-toolbar");

  // Verify topbar & CTA
  const topbarText = await evaluate("document.querySelector('header')?.innerText || ''");
  console.log("Topbar preview:", topbarText.replace(/\s+/g, " ").slice(0, 100));
  const toolbarButtons = await evaluate("[...document.querySelectorAll('.rate-detail-toolbar button, .rate-detail-toolbar a')].map(b => b.innerText.trim())");
  console.log("Toolbar buttons:", toolbarButtons);

  await captureScreenshot("01_rate_card_detail_published_v1_with_new_version_cta.png");

  // 3. SCREENSHOT 02: APPENDICES LIST (SEPARATED CTA & ROW ACTIONS)
  console.log("\n==========================================");
  console.log("STEP 2: SCREENSHOT 02 — APPENDICES LIST");
  console.log("==========================================");
  await go("http://localhost:3000/rate-cards/appendices");
  await waitForText("Quản lý phụ lục giá");
  await waitForSelector(".stat-card");

  const kpis = await evaluate("[...document.querySelectorAll('.stat-card')].map(c => ({ label: c.querySelector('.stat-label')?.innerText, value: c.querySelector('.stat-value')?.innerText }))");
  console.log("KPIs on appendices:", kpis);

  const topCta = await evaluate("document.querySelector('.panel-wide .btn-primary')?.innerText");
  console.log("Top right CTA:", topCta);

  await captureScreenshot("02_appendices_list_separated_cta_and_row_actions.png");

  // 4. SCREENSHOT 03: CREATE VERSION & PREFILLED EDIT FORM (v2 DRAFT)
  console.log("\n==========================================");
  console.log("STEP 3: SCREENSHOT 03 — CREATE VERSION & PREFILLED EDIT FORM");
  console.log("==========================================");
  await go(`http://localhost:3000/rate-cards/${cardId}`);
  await waitForSelector(".rate-detail-toolbar");

  console.log("Clicking 'Tạo phiên bản mới' CTA in toolbar...");
  await evaluate(`
    (() => {
      const btn = [...document.querySelectorAll(".rate-detail-toolbar button")].find(b => b.innerText.includes("Tạo phiên bản mới"));
      if (!btn) throw new Error("Could not find 'Tạo phiên bản mới' button");
      btn.click();
    })()
  `);

  // Wait for redirect to /edit
  await delay(4000);
  console.log("Current URL after create version:", await evaluate("window.location.href"));
  await waitForText("Phiên bản v2");

  const editHeader = await evaluate("document.querySelector('h1')?.innerText");
  console.log("Edit page header:", editHeader);

  await captureScreenshot("03_rate_card_v2_draft_edit_form_prefilled.png");

  // 5. SCREENSHOT 04: EDIT WEIGHT BAND TO COVER CW = 120 (e.g. 200) AND SAVE DRAFT
  console.log("\n==========================================");
  console.log("STEP 4: SCREENSHOT 04 — EDIT WEIGHT BAND & SAVE DRAFT");
  console.log("==========================================");
  console.log("Updating weight band max to 200 (expanding [1, 120) -> [1, 200))...");
  await evaluate(`
    (() => {
      const inputMax = document.querySelector("input[aria-label='Trọng lượng đến 1']");
      if (!inputMax) throw new Error("Could not find Trọng lượng đến 1 input");
      const setter = Object.getOwnPropertyDescriptor(Object.getPrototypeOf(inputMax), "value").set;
      setter.call(inputMax, "200");
      inputMax.dispatchEvent(new Event("input", { bubbles: true }));
      inputMax.dispatchEvent(new Event("change", { bubbles: true }));
    })()
  `);

  console.log("Clicking 'Lưu nháp' in declaration form...");
  await evaluate(`
    (() => {
      const submitBtn = [...document.querySelectorAll(".panel-wide form button")].find(b => b.innerText.includes("Lưu nháp"));
      if (!submitBtn) throw new Error("Could not find Lưu nháp button");
      submitBtn.click();
    })()
  `);

  await delay(3000);
  await waitForText("Đã lưu bản nháp v2 thành công!");
  console.log("Verified green saveSuccess banner present!");

  await captureScreenshot("04_rate_card_v2_draft_edit_band_cw120_save_draft.png");

  // 6. SCREENSHOT 05: PUBLISH CONFLICT BLOCKED (RATE_CONFLICT 409)
  console.log("\n==========================================");
  console.log("STEP 5: SCREENSHOT 05 — PUBLISH CONFLICT BLOCKED (RATE_CONFLICT)");
  console.log("==========================================");
  console.log("Inserting overlapping rule QT0002 into draft v2 to trigger RATE_CONFLICT...");
  insertConflictRule(cardId);

  console.log("Clicking publish button to trigger publish conflict...");
  await evaluate(`
    (() => {
      window.confirm = () => true;
      const publishBtn = [...document.querySelectorAll(".panel-wide form button, .rate-detail-toolbar button")].find(b => b.innerText.includes("Phát hành"));
      if (!publishBtn) throw new Error("Could not find publish button");
      publishBtn.click();
    })()
  `);

  await delay(3000);
  await waitForText("RATE_CONFLICT");
  console.log("Verified RATE_CONFLICT banner is displayed on UI!");

  await captureScreenshot("05_rate_card_v2_publish_conflict_blocked.png");

  // 7. SCREENSHOT 06: RESOLVE CONFLICT & PUBLISH v2
  console.log("\n==========================================");
  console.log("STEP 6: SCREENSHOT 06 — PUBLISHED v2 & VERSION HISTORY");
  console.log("==========================================");
  console.log("Removing conflicting rule QT0002 from database...");
  removeConflictRule();

  console.log("Clicking publish button again to publish clean v2...");
  await evaluate(`
    (() => {
      window.confirm = () => true;
      const publishBtn = [...document.querySelectorAll(".panel-wide form button, .rate-detail-toolbar button")].find(b => b.innerText.includes("Phát hành"));
      if (!publishBtn) throw new Error("Could not find publish button");
      publishBtn.click();
    })()
  `);

  await delay(5000);
  console.log("Redirected after publish. URL:", await evaluate("window.location.href"));
  await waitForText("TDNT_SGN_HAN");
  await waitForText("Phiên bản");

  await captureScreenshot("06_rate_card_v2_published_and_version_history.png");

  // 8. SCREENSHOT 07: BILL RATING CW = 120 kg
  console.log("\n==========================================");
  console.log("STEP 7: SCREENSHOT 07 — BILL RATING CW = 120 kg MATCHING v2");
  console.log("==========================================");
  await go("http://localhost:3000/rate-cards/rate");
  await waitForText("Tính giá");
  await waitForSelector("#billId");

  console.log("Filling rating calculator form for CW = 120 kg on v2...");
  await evaluate(`
    (() => {
      const setVal = (el, val) => {
        if (!el) return;
        const setter = Object.getOwnPropertyDescriptor(Object.getPrototypeOf(el), "value")?.set;
        if (setter) setter.call(el, val);
        else el.value = val;
        el.dispatchEvent(new Event("input", { bubbles: true }));
        el.dispatchEvent(new Event("change", { bubbles: true }));
      };

      const billSelect = document.querySelector("#billId");
      if (billSelect.options.length > 1) {
        billSelect.selectedIndex = 1;
        billSelect.dispatchEvent(new Event("change", { bubbles: true }));
      }

      const versionSelect = document.querySelector("#rateVersionId");
      const optV2 = [...versionSelect.options].find(o => o.innerText.includes("TDNT_SGN_HAN") && o.innerText.includes("v2"));
      if (optV2) {
        versionSelect.value = optV2.value;
      } else {
        versionSelect.selectedIndex = 1;
      }
      versionSelect.dispatchEvent(new Event("change", { bubbles: true }));

      const modeSelect = document.querySelector("#transportMode");
      if (modeSelect) {
        modeSelect.value = "air";
        modeSelect.dispatchEvent(new Event("change", { bubbles: true }));
      }

      setVal(document.querySelector("#rateDate"), "2026-10-24");
      setVal(document.querySelector("#originCode"), "SGN");
      setVal(document.querySelector("#destinationCode"), "HAN");
      setVal(document.querySelector("#chargeable"), "120");
      setVal(document.querySelector("#gross"), "120");

      document.querySelector(".panel-wide form button[type=submit]").click();
    })()
  `);

  await delay(5000);
  await waitForText("Kết quả Rating");
  await waitForText("1.440.000");
  const resultText = await evaluate("document.querySelector('aside.group-box')?.innerText");
  console.log("Rating calculated! Result summary:\n", resultText);

  await captureScreenshot("07_bill_rating_cw120_before_and_after_v2.png");

  console.log("\n========================================================");
  console.log(">>> SUCCESS: ALL 7 REAL LIVE SCREENSHOTS CAPTURED! <<<");
  console.log("========================================================");

} catch (err) {
  console.error("FATAL ERROR during E2E capture:", err);
} finally {
  browser.close();
  child.kill();
}

