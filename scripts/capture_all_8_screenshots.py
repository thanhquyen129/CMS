import os
import sys
import io
import time
from playwright.sync_api import sync_playwright

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

target_dirs = [
    "docs/po/27102026-LCMS_UAT_Correction_DEV_Package_v1.0_20261027/screenshots",
    "Docs/PO/27102026-LCMS_UAT_Correction_DEV_Package_v1.0_20261027/screenshots"
]

for d in target_dirs:
    os.makedirs(d, exist_ok=True)

def save_both(page, filename, element=None):
    for d in target_dirs:
        path = os.path.join(d, filename)
        if element:
            element.screenshot(path=path)
        else:
            page.screenshot(path=path)
    print(f"Saved: {filename}")

with sync_playwright() as p:
    browser = p.chromium.launch(
        channel="msedge",
        headless=True,
        args=["--no-sandbox", "--disable-gpu"]
    )
    # 1530x920 gives full visibility of sidebar, topbar, and main content
    context = browser.new_context(viewport={"width": 1530, "height": 920})
    page = context.new_page()

    # Login
    page.goto("http://localhost:3000/login", wait_until="networkidle")
    page.fill("input[name='email']", "admin@cms.local")
    page.fill("input[name='password']", "abc123")
    page.click("button[type='submit']")
    page.wait_for_url("**/dashboard")
    print("Logged in successfully.")

    # -------------------------------------------------------------
    # E01: Catalog Transport Modes
    # -------------------------------------------------------------
    page.goto("http://localhost:3000/admin/catalog?kind=transport_mode", wait_until="networkidle")
    time.sleep(1)
    save_both(page, "E01_Remediation_Transport_Mode_Master_Admin.png")

    # -------------------------------------------------------------
    # E02: Bill TESTLSS002 Edit -> Transport Mode Dropdown Synced
    # -------------------------------------------------------------
    page.goto("http://localhost:3000/bills/01A16CF7-30D8-731C-A50F-928FC7AA575B?tab=overview", wait_until="networkidle")
    time.sleep(1)
    # Click "Sửa thông tin Bill" button
    edit_btn = page.locator("button:has-text('Sửa thông tin Bill')")
    if edit_btn.count() > 0:
        edit_btn.first.click()
        time.sleep(0.5)
    
    # Expand select so options are visible in screenshot
    page.evaluate("""() => {
        const sel = document.getElementById('opref-transport_mode');
        if (sel) {
            sel.setAttribute('size', '2');
            sel.focus();
            sel.scrollIntoView({ behavior: 'instant', block: 'center' });
        }
    }""")
    time.sleep(0.5)
    save_both(page, "E02_Remediation_Transport_Mode_Dropdown_Synced.png")

    # -------------------------------------------------------------
    # E03: Service Type Dropdown Separation
    # -------------------------------------------------------------
    # In the same edit form, expand service_type_code select
    page.evaluate("""() => {
        const modeSel = document.getElementById('opref-transport_mode');
        if (modeSel) modeSel.removeAttribute('size');
        const sel = document.getElementById('opref-service_type_code');
        if (sel) {
            sel.setAttribute('size', '4');
            sel.focus();
            sel.scrollIntoView({ behavior: 'instant', block: 'center' });
        }
    }""")
    time.sleep(0.5)
    save_both(page, "E03_Remediation_Service_Type_Dropdown_Separation.png")

    # -------------------------------------------------------------
    # E04: Legacy Service Warning Banner
    # -------------------------------------------------------------
    page.goto("http://localhost:3000/bills/01A16CF7-30D8-732D-8B9F-76B2DC649F67?tab=overview", wait_until="networkidle")
    time.sleep(1)
    # In overview, there is an alert-warning banner for legacy AIR
    # Let's also click "Sửa thông tin Bill" to show the inline message under the dropdown
    edit_btn_legacy = page.locator("button:has-text('Sửa thông tin Bill')")
    if edit_btn_legacy.count() > 0:
        edit_btn_legacy.first.click()
        time.sleep(0.5)
    
    page.evaluate("""() => {
        const alert = document.querySelector('.alert-warning');
        if (alert) alert.scrollIntoView({ behavior: 'instant', block: 'start' });
    }""")
    time.sleep(0.5)
    save_both(page, "E04_Remediation_Legacy_Service_Warning_Banner.png")

    # -------------------------------------------------------------
    # E05: Bill Overview Freight Synchronized
    # -------------------------------------------------------------
    page.goto("http://localhost:3000/bills/01A16CF7-30D8-731C-A50F-928FC7AA575B?tab=overview", wait_until="networkidle")
    time.sleep(1)
    save_both(page, "E05_Remediation_Bill_Overview_Freight_Synchronized.png")

    # -------------------------------------------------------------
    # E06: Rating History No False Stale
    # -------------------------------------------------------------
    page.goto("http://localhost:3000/bills/01A16CF7-30D8-731C-A50F-928FC7AA575B?tab=rating", wait_until="networkidle")
    time.sleep(1)
    save_both(page, "E06_Remediation_Rating_History_No_False_Stale.png")

    # -------------------------------------------------------------
    # E07: Cost Freight Base Rate Trace
    # -------------------------------------------------------------
    page.goto("http://localhost:3000/costs/01A16DD7-C111-78AA-A9C8-509E0224A2CF", wait_until="networkidle")
    time.sleep(1)
    save_both(page, "E07_Remediation_Cost_Freight_Base_Rate_Trace.png")

    # -------------------------------------------------------------
    # E08: Cost Donggoi Fixed Surcharge Trace
    # -------------------------------------------------------------
    page.goto("http://localhost:3000/costs/01A16DD7-C222-78AA-A9C8-509E0224A2CF", wait_until="networkidle")
    time.sleep(1)
    save_both(page, "E08_Remediation_Cost_Donggoi_Fixed_Surcharge_Trace.png")

    browser.close()
    print("All 8 screenshots successfully captured and saved to both lower and upper case directories!")
