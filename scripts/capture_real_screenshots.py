import os
import sys
import time
from playwright.sync_api import sync_playwright

sys.stdout.reconfigure(encoding='utf-8')

SCREENSHOTS_DIR = os.path.abspath("docs/po/04112026/LCMS_SUR_Optional_ServiceCode_P0_DEV_Handover_v1.0_20261104/screenshots")
os.makedirs(SCREENSHOTS_DIR, exist_ok=True)

SURCHARGE_ID = "8183c1be-d606-4e24-adf7-d4da75a8f438"
BILL_ID = "01a16cf7-30d8-731c-a50f-928fc7aa575b"

with sync_playwright() as p:
    browser = p.chromium.launch(channel="msedge", headless=True)
    context = browser.new_context(viewport={"width": 1440, "height": 1080})
    page = context.new_page()

    print("Step 1: Logging in...")
    page.goto("http://localhost:3000/login", wait_until="networkidle")
    page.fill('input[type="email"]', "admin@cms.local")
    page.fill('input[type="password"]', "abc123")
    page.click('button[type="submit"]')
    page.wait_for_url("http://localhost:3000/", timeout=10000)
    print("Logged in successfully.")

    print("Step 2: Navigating to surcharge detail page...")
    page.goto(f"http://localhost:3000/rate-cards/surcharges/{SURCHARGE_ID}", wait_until="networkidle")
    time.sleep(1)

    # Scroll to draft form
    draft_title = page.locator('text=Sửa phiên bản nháp v3')
    draft_title.scroll_into_view_if_needed()
    time.sleep(0.5)

    # Screenshot 1: Lookup & options with inline warning
    s1_path = os.path.join(SCREENSHOTS_DIR, "01_v3_draft_optional_service_lookup.png")
    page.screenshot(path=s1_path, full_page=False)
    print(f"Captured: {s1_path}")

    # Now click "Phát hành bản này" while serviceCode is empty to trigger inline validation error
    pub_btn = page.locator('button:has-text("Phát hành bản này")')
    if pub_btn.is_visible():
        pub_btn.scroll_into_view_if_needed()
        pub_btn.click()
        time.sleep(1)

    # Screenshot 2: Missing service_code inline error
    # Scroll back up to the field to show the inline error
    draft_title.scroll_into_view_if_needed()
    time.sleep(0.5)
    s2_path = os.path.join(SCREENSHOTS_DIR, "02_v3_draft_missing_service_code_inline_error.png")
    page.screenshot(path=s2_path, full_page=False)
    print(f"Captured: {s2_path}")

    # Now select DONGGOI and select economic charge type if needed
    charge_type_select = page.locator('select[name="economicChargeTypeId"]')
    if charge_type_select.is_visible() and not charge_type_select.input_value():
        # Select first available option
        charge_type_select.select_option(index=1)

    svc_select = page.locator('select[name="serviceCode"]')
    if svc_select.is_visible():
        svc_select.select_option("DONGGOI")
        time.sleep(0.5)

    # Click Publish
    pub_btn.scroll_into_view_if_needed()
    pub_btn.click()
    page.wait_for_load_state("networkidle")
    time.sleep(2)

    # Navigate to version detail of v3 or surcharge page
    page.goto(f"http://localhost:3000/rate-cards/surcharges/{SURCHARGE_ID}", wait_until="networkidle")
    time.sleep(1)

    # Screenshot 3: Published v3 readonly & v2 immutable
    s3_path = os.path.join(SCREENSHOTS_DIR, "03_v3_published_readonly_and_v2_immutable.png")
    page.screenshot(path=s3_path, full_page=False)
    print(f"Captured: {s3_path}")

    # Step 3: Go to Bill rating tab
    print("Step 4: Navigating to Bill rating view...")
    page.goto(f"http://localhost:3000/bills/{BILL_ID}?tab=rating", wait_until="networkidle")
    time.sleep(1.5)

    # Scroll to Dịch vụ tùy chọn panel
    svc_panel = page.locator('text=Dịch vụ / Phụ phí tùy chọn, text=Dịch vụ bổ sung').first
    if svc_panel.is_visible():
        svc_panel.scroll_into_view_if_needed()
        time.sleep(0.5)

    # Screenshot 4: Bill rating with optional service unselected
    s4_path = os.path.join(SCREENSHOTS_DIR, "04_bill_optional_service_sync_and_rating_not_selected.png")
    page.screenshot(path=s4_path, full_page=False)
    print(f"Captured: {s4_path}")

    # Select DONGGOI checkbox and save
    checkboxes = page.locator('input[type="checkbox"]')
    if checkboxes.count() > 0:
        first_chk = checkboxes.first
        if not first_chk.is_checked():
            first_chk.check()
            time.sleep(0.5)
        save_btn = page.locator('button:has-text("Lưu lựa chọn")')
        if save_btn.is_visible():
            save_btn.click()
            time.sleep(1)

    # Rate bill with selection active
    rate_btn = page.locator('button:has-text("Tính giá lại"), button:has-text("Tính giá")').first
    if rate_btn.is_visible():
        rate_btn.scroll_into_view_if_needed()
        rate_btn.click()
        page.wait_for_load_state("networkidle")
        time.sleep(1.5)

    # Screenshot 5: Optional service selected, rating calculated and applied
    s5_path = os.path.join(SCREENSHOTS_DIR, "05_bill_optional_service_selected_rating_and_idempotent_apply.png")
    page.screenshot(path=s5_path, full_page=False)
    print(f"Captured: {s5_path}")

    browser.close()
    print("All 5 real screenshots generated successfully!")
