import sys
import sqlite3
import uuid
from datetime import datetime, timezone

sys.stdout.reconfigure(encoding='utf-8')
con = sqlite3.connect('src/LCMS.Api/lcms_dev.db')
cur = con.cursor()

tenant_id = '01A16CF7-2EF2-7299-9730-0EC040B35EDA'
now_iso = datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")

# Baseline optional services
services = [
    ("DONGGOI", "Đóng gói hàng hóa", "Dịch vụ đóng gói và bao bọc hàng hóa bổ sung", 1, '{"allowedContextTypes":["BILL","SHIPMENT","LEG"],"defaultUom":"PACKAGE"}'),
    ("KIEMDEM", "Kiểm đếm tại kho", "Dịch vụ kiểm đếm số lượng, quy cách hàng hóa tại kho", 1, '{"allowedContextTypes":["BILL","SHIPMENT","LEG"],"defaultUom":"BILL"}'),
    ("NANGHA", "Nâng hạ bốc xếp", "Dịch vụ nâng hạ, bốc xếp kiện hàng nặng hoặc đặc biệt", 1, '{"allowedContextTypes":["BILL","SHIPMENT","LEG"],"defaultUom":"SHIPMENT"}'),
    ("BAOHIEM", "Bảo hiểm hàng hóa giá trị cao", "Dịch vụ bảo hiểm bổ sung theo giá trị khai báo", 0, '{"allowedContextTypes":["BILL"],"defaultUom":"BILL"}')
]

for code, name, desc, active, attrs in services:
    cur.execute('SELECT id FROM master_catalog_items WHERE tenant_id = ? AND kind = "optional_service" AND code = ?', (tenant_id, code))
    row = cur.fetchone()
    if not row:
        item_id = str(uuid.uuid4()).upper()
        cur.execute("""
            INSERT INTO master_catalog_items (id, tenant_id, kind, code, name, description, is_active, attributes_json, sort_order, created_at, created_by, row_version)
            VALUES (?, ?, 'optional_service', ?, ?, ?, ?, ?, 1, ?, 'system', x'0000000000000001')
        """, (item_id, tenant_id, code, name, desc, active, attrs, now_iso))
        print(f"Seeded master catalog item {code}")

# Check if v3 draft already exists for DONGGOI_SGN_HAN_MUA
surcharge_id = '8183C1BE-D606-4E24-ADF7-D4DA75A8F438'
cur.execute('SELECT id, version_no, publish_status FROM surcharge_versions WHERE surcharge_id = ? AND version_no = 3', (surcharge_id,))
v3_row = cur.fetchone()
if not v3_row:
    # Get v2 to copy economic_charge_type_id
    cur.execute('SELECT economic_charge_type_id FROM surcharge_versions WHERE surcharge_id = ? AND version_no = 2', (surcharge_id,))
    charge_type_id = cur.fetchone()[0]

    v3_id = str(uuid.uuid4()).upper()
    cur.execute("""
        INSERT INTO surcharge_versions (
            id, tenant_id, surcharge_id, version_no, publish_status,
            activation_policy, activation_config_json, approval_ref,
            valid_from, economic_charge_type_id, created_at, created_by, row_version
        ) VALUES (
            ?, ?, ?, 3, 'draft',
            'OPTIONAL_OPT_IN', NULL, 'CR-SURCHARGE-01',
            ?, ?, ?, 'system', x'0000000000000001'
        )
    """, (v3_id, tenant_id, surcharge_id, now_iso, charge_type_id, now_iso))

    # Add a rule for v3 (fixed_rate 300.000 VND)
    rule_id = str(uuid.uuid4()).upper()
    cur.execute("""
        INSERT INTO surcharge_rules (
            id, tenant_id, surcharge_version_id, calculation_mode, basis,
            currency_code, rate_amount_percent, priority, created_at, created_by, row_version
        ) VALUES (
            ?, ?, ?, 'fixed_rate', 'per_bill',
            'VND', 300000, 1, ?, 'system', x'0000000000000001'
        )
    """, (rule_id, tenant_id, v3_id, now_iso))
    print(f"Seeded draft v3 for DONGGOI_SGN_HAN_MUA (id: {v3_id})")
else:
    print(f"v3 already exists: {v3_row}")

con.commit()
con.close()
print("Dev database seeding finished successfully")
