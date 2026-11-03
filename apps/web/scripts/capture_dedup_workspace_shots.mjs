import { chromium } from "playwright";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const dir = path.resolve(
  __dirname,
  "../../../docs/po/03112026/LCMS_Rating_Financial_Dedup_P0P1_v1.0_20261103/screenshots"
);
const html = path.join(dir, "workspace_uat_fixture.html");
const fileUrl = "file:///" + html.replace(/\\/g, "/");

const shots = [
  ["01_Bill_LSSTEST02112026002_Workspace_Overview_Cost1740k_Profit700k.png", "#shot-01"],
  ["02_Workspace_AreaAB_RatingTryCalculate_And_CandidateCompare.png", "#shot-02"],
  ["03_Workspace_AreaC_PreviewApply_Delta_And_ActualProtectionWarning.png", "#shot-03"],
  ["04_Workspace_AreaD_FinancialBreakdown_EffectiveIncluded_And_ExclusionReason.png", "#shot-04"],
  ["05_Workspace_AreaE_ThreeSegmentedProfitKPIs_And_IncompleteWarning.png", "#shot-05"],
  ["06_Workspace_AreaF_AuditTimeline_And_RecordActualModal.png", "#shot-06"],
];

const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 1280, height: 900 } });
await page.goto(fileUrl);
for (const [name, sel] of shots) {
  const el = page.locator(sel);
  await el.scrollIntoViewIfNeeded();
  await el.screenshot({ path: path.join(dir, name) });
  console.log("wrote", name);
}
await browser.close();
