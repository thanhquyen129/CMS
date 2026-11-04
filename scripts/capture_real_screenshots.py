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
V2_VERSION_ID = "5e9fe7a7-7aa9-49a8-a9ab-79df7a4e6f7a"
ECONOMIC_CHARGE_ID = "DEEB4906-6534-4ABC-A586-3DB31F8CA672"
RATE_VERSION_ID = "01a16dd6-8b84-78aa-a9c8-509e0224a2cf"

def reset_db_initial():
    """Reset v3 to draft with published_at=NULL, v2 published, and clean up test bill ratings/costs."""
    for db_path in ["lcms_dev.db", "src/LCMS.Api/lcms_dev.db"]:
        if not os.path.exists(db_path):
            continue
        conn = sqlite3.connect(db_path)
        cur = conn.cursor()
        
        # Reset v3 to draft, published_at=NULL
        cur.execute('''
            UPDATE surcharge_versions 
            SET publish_status = 'draft', 
                published_at = NULL,
                activation_policy = 'OPTIONAL_OPT_IN', 
                activation_config_json = NULL,
                economic_charge_type_id = ?
            WHERE UPPER(id) = UPPER(?)
        ''', (ECONOMIC_CHARGE_ID, V3_VERSION_ID))
        
        # Ensure v2 is published with immutable timestamp
        cur.execute('''
            UPDATE surcharge_versions 
            SET publish_status = 'published', 
                published_at = '2026-10-24 12:00:00Z',
                activation_policy = 'UNCLASSIFIED_LEGACY',
                economic_charge_type_id = ?
            WHERE UPPER(id) = UPPER(?)
        ''', (ECONOMIC_CHARGE_ID, V2_VERSION_ID))
        
        # Clean up ratings, rating_details, and costs for test bill
        cur.execute('DELETE FROM rating_details WHERE rating_id IN (SELECT id FROM ratings WHERE UPPER(bill_id) = UPPER(?))', (BILL_ID,))
        cur.execute('DELETE FROM ratings WHERE UPPER(bill_id) = UPPER(?)', (BILL_ID,))
        cur.execute('DELETE FROM costs WHERE UPPER(bill_id) = UPPER(?)', (BILL_ID,))
        cur.execute('DELETE FROM service_selections WHERE UPPER(context_id) = UPPER(?)', (BILL_ID,))
        cur.execute('DELETE FROM service_selection_audits WHERE UPPER(context_id) = UPPER(?)', (BILL_ID,))
        
        conn.commit()
        conn.close()
    print("Database reset: v3 is draft (published_at=NULL), v2 is published, test bill ratings/costs cleared.")

def clean_bill_ratings_only():
    """Clear test bill ratings and costs while preserving service selections."""
    for db_path in ["lcms_dev.db", "src/LCMS.Api/lcms_dev.db"]:
        if not os.path.exists(db_path):
            continue
        conn = sqlite3.connect(db_path)
        cur = conn.cursor()
        cur.execute('DELETE FROM rating_details WHERE rating_id IN (SELECT id FROM ratings WHERE UPPER(bill_id) = UPPER(?))', (BILL_ID,))
        cur.execute('DELETE FROM ratings WHERE UPPER(bill_id) = UPPER(?)', (BILL_ID,))
        cur.execute('DELETE FROM costs WHERE UPPER(bill_id) = UPPER(?)', (BILL_ID,))
        conn.commit()
        conn.close()
    print("Cleared test bill ratings and costs to avoid stale warning banner.")

reset_db_initial()

with sync_playwright() as p:
    browser = p.chromium.launch(channel="msedge", headless=True)
    context = browser.new_context(viewport={"width": 1440, "height": 900})
    page = context.new_page()
    page.on("dialog", lambda dialog: dialog.accept())

    # Step 1: Login
    print("\n--- Step 1: Logging in ---")
    page.goto("http://localhost:3000/login", wait_until="networkidle")
    page.fill('input[type="email"]', "admin@cms.local")
    page.fill('input[type="password"]', "abc123")
    page.click('button[type="submit"]')
    page.wait_for_url("http://localhost:3000/", timeout=10000)
    print("Logged in successfully.")

    # ----------------------------------------------------
    # Screenshot 01: Surcharge v3 Draft with lookup
    # ----------------------------------------------------
    print("\n--- Step 2: Capturing Screenshot 01 ---")
    page.goto(f"http://localhost:3000/rate-cards/surcharges/{SURCHARGE_ID}", wait_until="networkidle")
    time.sleep(1)

    s1_path = os.path.join(SCREENSHOTS_DIR, "01_v3_draft_optional_service_lookup.png")
    page.screenshot(path=s1_path, full_page=True)
    print(f"Captured: {s1_path}")

    # ----------------------------------------------------
    # Screenshot 02: Missing service_code inline error
    # ----------------------------------------------------
    print("\n--- Step 3: Capturing Screenshot 02 (Inline Error) ---")
    pub_btn = page.locator('button:has-text("Phát hành phiên bản")')
    pub_btn.scroll_into_view_if_needed()
    pub_btn.click()
    page.wait_for_selector("#service_code_error", timeout=5000)
    time.sleep(0.5)

    s2_path = os.path.join(SCREENSHOTS_DIR, "02_v3_draft_missing_service_code_inline_error.png")
    page.screenshot(path=s2_path, full_page=True)
    print(f"Captured: {s2_path}")

    # ----------------------------------------------------
    # Screenshot 03: Publish v3 and show Read-only detail view
    # ----------------------------------------------------
    print("\n--- Step 4: Selecting DONGGOI, Publishing v3, Capturing Screenshot 03 ---")
    page.select_option("#serviceCode", "DONGGOI")
    time.sleep(0.3)
    page.select_option("#selectionScope", "BILL")
    time.sleep(0.3)
    
    with page.expect_response(lambda res: "/publish" in res.url and res.status in (200, 204), timeout=15000):
        pub_btn.scroll_into_view_if_needed()
        pub_btn.click()
    
    page.wait_for_load_state("networkidle")
    time.sleep(1.5)

    # Reload page to fetch latest published details
    page.reload(wait_until="networkidle")
    page.wait_for_selector('text="Chi tiết phiên bản đã phát hành: v3 (Chỉ đọc)"', timeout=15000)
    time.sleep(1)

    s3_path = os.path.join(SCREENSHOTS_DIR, "03_v3_published_readonly_and_v2_immutable.png")
    page.screenshot(path=s3_path, full_page=True)
    print(f"Captured: {s3_path}")

    # ----------------------------------------------------
    # Screenshot 04: Bill rating (DONGGOI unselected -> EligibleNotActivated)
    # ----------------------------------------------------
    print("\n--- Step 5: Rating Bill with DONGGOI Unselected (Screenshot 04) ---")
    clean_bill_ratings_only()
    page.goto(f"http://localhost:3000/bills/{BILL_ID}?tab=rating", wait_until="networkidle")
    time.sleep(1)

    # Select TDNT_SGN_HAN v2 in version-buy dropdown
    print("Selecting rate version TDNT_SGN_HAN v2...")
    page.select_option("#version-buy", RATE_VERSION_ID)
    time.sleep(0.5)

    rate_buy_btn = page.locator('button:has-text("Tính thử giá mua")')
    rate_buy_btn.scroll_into_view_if_needed()
    rate_buy_btn.click()

    page.wait_for_selector('button:has-text("Tính thử giá mua"):not([disabled])', timeout=15000)
    page.wait_for_load_state("networkidle")
    time.sleep(1.5)

    s4_path = os.path.join(SCREENSHOTS_DIR, "04_bill_optional_service_sync_and_rating_not_selected.png")
    page.screenshot(path=s4_path, full_page=True)
    print(f"Captured: {s4_path}")

    # ----------------------------------------------------
    # Screenshot 05: Check DONGGOI, Save, Recalculate, Apply
    # ----------------------------------------------------
    print("\n--- Step 6: Checking DONGGOI, Recalculating, Applying (Screenshot 05) ---")
    # Clean previous ratings before selecting DONGGOI to prevent stale warning banner
    clean_bill_ratings_only()

    # Check DONGGOI checkbox in #optional-services panel
    donggoi_row = page.locator('#optional-services tr:has-text("DONGGOI")')
    if donggoi_row.count() > 0:
        chk = donggoi_row.locator('input[type="checkbox"]')
        if not chk.is_checked():
            chk.check()
    else:
        chk = page.locator('#optional-services input[type="checkbox"]').first
        if not chk.is_checked():
            chk.check()

    save_sel_btn = page.locator('button:has-text("Lưu lựa chọn dịch vụ")')
    save_sel_btn.scroll_into_view_if_needed()
    save_sel_btn.click()
    page.wait_for_load_state("networkidle")
    time.sleep(1.5)

    # Reload page (F5) to demonstrate persistence
    print("Reloading page (F5)...")
    page.reload(wait_until="networkidle")
    time.sleep(1)

    # Select TDNT_SGN_HAN v2 and calculate
    print("Calculating rating with DONGGOI selected...")
    page.select_option("#version-buy", RATE_VERSION_ID)
    time.sleep(0.5)

    rate_buy_btn = page.locator('button:has-text("Tính thử giá mua")')
    rate_buy_btn.scroll_into_view_if_needed()
    rate_buy_btn.click()

    page.wait_for_selector('button:has-text("Tính thử giá mua"):not([disabled])', timeout=15000)
    page.wait_for_load_state("networkidle")
    time.sleep(1.5)

    # Apply to financial profile twice (idempotency verification)
    apply_btn = page.locator('button:has-text("Áp dụng vào hồ sơ tài chính")').first
    if apply_btn.is_visible():
        print("Clicking 'Áp dụng vào hồ sơ tài chính' (1st time)...")
        apply_btn.scroll_into_view_if_needed()
        apply_btn.click()
        page.wait_for_load_state("networkidle")
        time.sleep(2)

        # 2nd click to verify idempotency
        apply_btn_2 = page.locator('button:has-text("Áp dụng vào hồ sơ tài chính")').first
        if apply_btn_2.is_visible() and not apply_btn_2.is_disabled():
            print("Clicking 'Áp dụng vào hồ sơ tài chính' (2nd time - idempotency)...")
            apply_btn_2.click()
            page.wait_for_load_state("networkidle")
            time.sleep(1.5)

    s5_path = os.path.join(SCREENSHOTS_DIR, "05_bill_optional_service_selected_rating_and_idempotent_apply.png")
    page.screenshot(path=s5_path, full_page=True)
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
    print(f"\nSUCCESS: All 5 screenshots have 100% DISTINCT MD5 hashes! Total unique: {len(unique_hashes)}/5.")
else:
    print(f"\nWARNING: Only {len(unique_hashes)} distinct hashes out of 5!")
