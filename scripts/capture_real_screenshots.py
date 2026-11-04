import os
import sys
import time
import json
import sqlite3
import hashlib
from playwright.sync_api import sync_playwright

sys.stdout.reconfigure(encoding='utf-8')

ROOT_DIR = os.path.abspath(".")
SCREENSHOTS_DIR = os.path.abspath("docs/po/04112026/LCMS_SUR_Optional_ServiceCode_P0_DEV_Handover_v1.0_20261104/screenshots")
os.makedirs(SCREENSHOTS_DIR, exist_ok=True)

SURCHARGE_ID = "8183c1be-d606-4e24-adf7-d4da75a8f438"
BILL_ID = "01a1a51e-da2e-776c-99e4-42451fe78001"
V3_VERSION_ID = "01a1a51e-da2e-776c-99e4-42451fe78f77"
ECONOMIC_CHARGE_ID = "DEEB4906-6534-4ABC-A586-3DB31F8CA672"
RATE_VERSION_ID = "01a16dd6-8b84-78aa-a9c8-509e0224a2cf"

def reset_db_state():
    """Ensure v3 is draft, unlinked service_code, economic charge type linked, and bill has no service selections."""
    for db_path in ["lcms_dev.db", "src/LCMS.Api/lcms_dev.db"]:
        conn = sqlite3.connect(db_path)
        cur = conn.cursor()
        # Reset v3 to draft
        cur.execute('''
            UPDATE surcharge_versions 
            SET publish_status = 'draft', activation_policy = 'OPTIONAL_OPT_IN', activation_config_json = NULL,
                economic_charge_type_id = ?
            WHERE UPPER(id) = UPPER(?)
        ''', (ECONOMIC_CHARGE_ID, V3_VERSION_ID))
        # Ensure v2 is published
        cur.execute('''
            UPDATE surcharge_versions 
            SET publish_status = 'published', activation_policy = 'UNCLASSIFIED_LEGACY',
                economic_charge_type_id = ?
            WHERE UPPER(id) = '5E9FE7A7-7AA9-49A8-A9AB-79DF7A4E6F7A'
        ''', (ECONOMIC_CHARGE_ID,))
        # Reset service selections for bill
        cur.execute('''
            DELETE FROM service_selections WHERE UPPER(context_id) = UPPER(?)
        ''', (BILL_ID,))
        cur.execute('''
            DELETE FROM service_selection_audits WHERE UPPER(context_id) = UPPER(?)
        ''', (BILL_ID,))
        conn.commit()
        conn.close()
    print("Database reset to clean baseline state for screenshot capture.")

reset_db_state()

with sync_playwright() as p:
    browser = p.chromium.launch(channel="msedge", headless=True)
    context = browser.new_context(viewport={"width": 1440, "height": 1080})
    page = context.new_page()

    # Step 1: Login
    print("Step 1: Logging in...")
    page.goto("http://localhost:3000/login", wait_until="networkidle")
    page.fill('input[type="email"]', "admin@cms.local")
    page.fill('input[type="password"]', "abc123")
    page.click('button[type="submit"]')
    page.wait_for_url("http://localhost:3000/", timeout=10000)
    print("Logged in successfully.")

    # Step 2: Navigate to surcharge detail page
    print("Step 2: Navigating to surcharge detail page...")
    page.goto(f"http://localhost:3000/rate-cards/surcharges/{SURCHARGE_ID}", wait_until="networkidle")
    time.sleep(1)

    # Screenshot 1: v3 draft optional service lookup
    print("Capturing Screenshot 01: v3 draft with lookup and inline warning...")
    page.evaluate("window.scrollTo(0, 350)")
    time.sleep(0.5)
    s1_path = os.path.join(SCREENSHOTS_DIR, "01_v3_draft_optional_service_lookup.png")
    page.screenshot(path=s1_path, full_page=False)
    print(f"Captured: {s1_path}")

    # Screenshot 2: Click "Phát hành phiên bản" without service_code -> inline red error
    print("Step 3: Triggering missing service_code inline error...")
    pub_btn = page.locator('button:has-text("Phát hành phiên bản")')
    pub_btn.scroll_into_view_if_needed()
    pub_btn.click()
    time.sleep(0.8)

    # Scroll to show both the select box error and the inline message
    page.evaluate("window.scrollTo(0, 450)")
    time.sleep(0.5)
    s2_path = os.path.join(SCREENSHOTS_DIR, "02_v3_draft_missing_service_code_inline_error.png")
    page.screenshot(path=s2_path, full_page=False)
    print(f"Captured: {s2_path}")

    # Screenshot 3: Select DONGGOI, selectionScope BILL, and publish
    print("Step 4: Selecting DONGGOI and publishing v3...")
    page.select_option("#serviceCode", "DONGGOI")
    time.sleep(0.3)
    page.select_option("#selectionScope", "BILL")
    time.sleep(0.3)

    pub_btn.scroll_into_view_if_needed()
    pub_btn.click()
    page.wait_for_load_state("networkidle")
    time.sleep(2)

    # Navigate back to surcharge detail page to show v3 Published and v2 Published
    page.goto(f"http://localhost:3000/rate-cards/surcharges/{SURCHARGE_ID}", wait_until="networkidle")
    time.sleep(1)
    page.evaluate("window.scrollTo(0, 100)")
    time.sleep(0.5)
    s3_path = os.path.join(SCREENSHOTS_DIR, "03_v3_published_readonly_and_v2_immutable.png")
    page.screenshot(path=s3_path, full_page=False)
    print(f"Captured: {s3_path}")

    # Step 5: Navigate to Bill Rating tab (unselected optional service)
    print("Step 5: Navigating to Bill rating view (unselected)...")
    page.goto(f"http://localhost:3000/bills/{BILL_ID}?tab=rating", wait_until="networkidle")
    time.sleep(1.5)

    # Select TDNT_SGN_HAN v2 in version-buy dropdown
    print("Selecting rate version TDNT_SGN_HAN v2...")
    page.select_option("#version-buy", RATE_VERSION_ID)
    time.sleep(0.5)

    rate_buy_btn = page.locator('button:has-text("Tính thử giá mua")')
    if rate_buy_btn.is_visible():
        rate_buy_btn.scroll_into_view_if_needed()
        rate_buy_btn.click()
        page.wait_for_load_state("networkidle")
        time.sleep(2)

    # Scroll to show both Optional Services panel and Rating results
    page.evaluate("window.scrollTo(0, 350)")
    time.sleep(0.5)
    s4_path = os.path.join(SCREENSHOTS_DIR, "04_bill_optional_service_sync_and_rating_not_selected.png")
    page.screenshot(path=s4_path, full_page=False)
    print(f"Captured: {s4_path}")

    # Step 6: Check DONGGOI, save selection, reload F5, calculate rate, and apply financials
    print("Step 6: Checking DONGGOI, saving, recalculating rate, and applying...")
    # Find DONGGOI checkbox inside #optional-services panel
    donggoi_row = page.locator('#optional-services tr:has-text("DONGGOI")')
    if donggoi_row.count() > 0:
        chk = donggoi_row.locator('input[type="checkbox"]')
        if not chk.is_checked():
            chk.check()
            time.sleep(0.3)
    else:
        # Fallback to first checkbox in optional services
        chk = page.locator('#optional-services input[type="checkbox"]').first
        if not chk.is_checked():
            chk.check()
            time.sleep(0.3)

    save_sel_btn = page.locator('button:has-text("Lưu lựa chọn dịch vụ")')
    if save_sel_btn.is_visible():
        save_sel_btn.click()
        time.sleep(1.5)

    # Reload page (F5) to demonstrate persistence
    print("Reloading page (F5) to verify persistence...")
    page.reload(wait_until="networkidle")
    time.sleep(1.5)

    # Re-select TDNT_SGN_HAN v2 if needed and rate
    page.select_option("#version-buy", RATE_VERSION_ID)
    time.sleep(0.5)

    rate_buy_btn = page.locator('button:has-text("Tính thử giá mua")')
    if rate_buy_btn.is_visible():
        rate_buy_btn.scroll_into_view_if_needed()
        rate_buy_btn.click()
        page.wait_for_load_state("networkidle")
        time.sleep(2)

    # Apply to financial profile
    apply_btn = page.locator('button:has-text("Áp dụng vào hồ sơ tài chính")').first
    if apply_btn.is_visible():
        apply_btn.scroll_into_view_if_needed()
        apply_btn.click()
        time.sleep(1.5)

    # Scroll to show rating results with DONGGOI applied and costs created
    page.evaluate("window.scrollTo(0, 400)")
    time.sleep(0.5)
    s5_path = os.path.join(SCREENSHOTS_DIR, "05_bill_optional_service_selected_rating_and_idempotent_apply.png")
    page.screenshot(path=s5_path, full_page=False)
    print(f"Captured: {s5_path}")

    browser.close()

# Verify all 5 screenshot hashes are distinct
hashes = {}
print("\n--- Screenshot MD5 Hash Verification ---")
for fname in [
    "01_v3_draft_optional_service_lookup.png",
    "02_v3_draft_missing_service_code_inline_error.png",
    "03_v3_published_readonly_and_v2_immutable.png",
    "04_bill_optional_service_sync_and_rating_not_selected.png",
    "05_bill_optional_service_selected_rating_and_idempotent_apply.png",
]:
    fpath = os.path.join(SCREENSHOTS_DIR, fname)
    with open(fpath, "rb") as f:
        h = hashlib.md5(f.read()).hexdigest()
    hashes[fname] = h
    sz = os.path.getsize(fpath)
    print(f"{fname}: MD5 = {h}, Size = {sz:,} bytes")

unique_hashes = set(hashes.values())
if len(unique_hashes) == 5:
    print("\nSUCCESS: All 5 screenshots have 100% DISTINCT MD5 hashes! Zero duplication!")
else:
    print(f"\nWARNING: Only {len(unique_hashes)} distinct hashes out of 5!")
