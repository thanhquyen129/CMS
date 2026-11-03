import { chromium } from "playwright";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const outDir = path.resolve(
  __dirname,
  "../../../docs/po/03112026/LCMS_Rating_Financial_Dedup_P0P1_v1.0_20261103/screenshots"
);
const base = process.env.LCMS_WEB_URL || "http://127.0.0.1:3000";
const url = `${base}/dev/rating-workspace-preview`;

const shots = [
  ["01_Bill_LSSTEST02112026002_Workspace_Overview_Cost1740k_Profit700k.png", "#shot-01"],
  ["02_Workspace_AreaAB_RatingTryCalculate_And_CandidateCompare.png", "#shot-02"],
  ["03_Workspace_AreaC_PreviewApply_Delta_And_ActualProtectionWarning.png", "#shot-03"],
  ["04_Workspace_AreaD_FinancialBreakdown_EffectiveIncluded_And_ExclusionReason.png", "#workspace-area-d"],
  ["05_Workspace_AreaE_ThreeSegmentedProfitKPIs_And_IncompleteWarning.png", "#workspace-area-e"],
  ["06_Workspace_AreaF_AuditTimeline_And_RecordActualModal.png", "#workspace-area-f"],
];

const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 1440, height: 960 } });
await page.goto(url, { waitUntil: "networkidle", timeout: 120000 });
await page.waitForSelector("#workspace-area-d", { timeout: 60000 });

for (const [name, sel] of shots) {
  const el = page.locator(sel).first();
  await el.scrollIntoViewIfNeeded();
  await el.screenshot({ path: path.join(outDir, name) });
  console.log("wrote", name);
}

await browser.close();
