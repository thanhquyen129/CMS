import sqlite3
import uuid
from datetime import datetime, timezone

con = sqlite3.connect('src/LCMS.Api/lcms_dev.db')
cur = con.cursor()

tenant_id = '01A16CF7-2EF2-7299-9730-0EC040B35EDA'
bill_id = '01A16CF7-30D8-731C-A50F-928FC7AA575B'
card_id = '01A16CF7-3B64-7606-8B4D-2158CEA9D954'
v1_id = '01A16CF7-3B7A-7F90-8A6E-DEDDBF487F62'
v2_id = '01A16DD6-8B84-78AA-A9C8-509E0224A2CF'
now_str = datetime.now(timezone.utc).isoformat()

# 1. Ensure DEMO-01-HUB has SGN-HAN route and FREIGHT service
cur.execute("""
    UPDATE bills
    SET route_code = 'SGN-HAN',
        origin_code = 'SGN',
        destination_code = 'HAN',
        transport_mode = 'air',
        service_type_code = 'FREIGHT'
    WHERE id = ?
""", (bill_id,))

# 2. Operational measurements: CW = 120kg, Gross = 120kg
cur.execute("DELETE FROM operational_measurements WHERE object_id = ?", (bill_id,))
m_cw_id = str(uuid.uuid4()).upper()
cur.execute("""
    INSERT INTO operational_measurements (id, tenant_id, object_type, object_id, measure_code, quantity, uom, source_channel, is_confirmed, row_version, created_at)
    VALUES (?, ?, 'bill', ?, 'chargeable_weight_kg', 120.0, 'kg', 'manual', 1, ?, ?)
""", (m_cw_id, tenant_id, bill_id, uuid.uuid4().bytes, now_str))

m_gw_id = str(uuid.uuid4()).upper()
cur.execute("""
    INSERT INTO operational_measurements (id, tenant_id, object_type, object_id, measure_code, quantity, uom, source_channel, is_confirmed, row_version, created_at)
    VALUES (?, ?, 'bill', ?, 'gross_weight_kg', 120.0, 'kg', 'manual', 1, ?, ?)
""", (m_gw_id, tenant_id, bill_id, uuid.uuid4().bytes, now_str))

# 3. Ensure Route SGN-HAN exists
cur.execute("SELECT id FROM routes WHERE code = 'SGN-HAN'")
r_row = cur.fetchone()
if not r_row:
    route_id = str(uuid.uuid4()).upper()
    cur.execute("""
        INSERT INTO routes (id, tenant_id, code, name, transport_mode_code, service_type_code, is_active, row_version, created_at)
        VALUES (?, ?, 'SGN-HAN', 'SGN-HAN', 'air', 'FREIGHT', 1, ?, ?)
    """, (route_id, tenant_id, uuid.uuid4().bytes, now_str))
else:
    route_id = r_row[0]

# Rule v1: [1, 120) -> 120kg fails
cur.execute("""
    UPDATE pricing_rules
    SET name = 'SGN-HAN v1 Cước', route_code = 'SGN-HAN', transport_mode = 'air',
        service_type_code = 'FREIGHT', weight_min = 1.0, weight_max = 120.0, weight_basis = 'chargeable',
        unit_amount = 12000.0, is_active = 1
    WHERE rate_version_id = ? AND code = 'QT0001'
""", (v1_id,))

# Rule v2: [1, 121) -> 120kg matches!
cur.execute("""
    UPDATE pricing_rules
    SET name = 'SGN-HAN v2 Cước', route_code = 'SGN-HAN', transport_mode = 'air',
        service_type_code = 'FREIGHT', weight_min = 1.0, weight_max = 121.0, weight_basis = 'chargeable',
        unit_amount = 12000.0, is_active = 1
    WHERE rate_version_id = ? AND code = 'QT0001'
""", (v2_id,))

cur.execute("SELECT id FROM pricing_rules WHERE rate_version_id = ? AND code = 'QT0001'", (v2_id,))
v2_rule_id = cur.fetchone()[0]

# 5. Surcharge DONGGOI
cur.execute("SELECT id FROM surcharges WHERE code = 'DONGGOI'")
sc_row = cur.fetchone()
if not sc_row:
    surcharge_id = str(uuid.uuid4()).upper()
    cur.execute("""
        INSERT INTO surcharges (id, tenant_id, code, name, direction, status, row_version, created_at)
        VALUES (?, ?, 'DONGGOI', 'Phụ phí đóng gói hàng hóa', 'buy', 'active', ?, ?)
    """, (surcharge_id, tenant_id, uuid.uuid4().bytes, now_str))
else:
    surcharge_id = sc_row[0]

cur.execute("SELECT id FROM surcharge_versions WHERE surcharge_id = ?", (surcharge_id,))
scv_row = cur.fetchone()
if not scv_row:
    surcharge_version_id = str(uuid.uuid4()).upper()
    cur.execute("""
        INSERT INTO surcharge_versions (id, tenant_id, surcharge_id, version_no, publish_status, vat_rate, row_version, created_at)
        VALUES (?, ?, ?, 1, 'published', 8.0, ?, ?)
    """, (surcharge_version_id, tenant_id, surcharge_id, uuid.uuid4().bytes, now_str))
else:
    surcharge_version_id = scv_row[0]

# 6. Rating BUY on DEMO-01-HUB
rating_id = '01A16DD7-R111-78AA-A9C8-509E0224A2CF'
detail_freight_id = '01A16DD7-D111-78AA-A9C8-509E0224A2CF'
detail_packing_id = '01A16DD7-D222-78AA-A9C8-509E0224A2CF'

cur.execute("DELETE FROM rating_details WHERE rating_id = ?", (rating_id,))
cur.execute("DELETE FROM ratings WHERE id = ?", (rating_id,))

cur.execute("""
    INSERT INTO ratings (
        id, tenant_id, bill_id, rate_version_id, rate_card_id, context_type, party_type_code,
        service_type_code, route_code, chargeable_weight_kg, chargeable_basis,
        total_amount, quantity, currency_code, rated_at, status, context_json, row_version, created_at
    ) VALUES (
        ?, ?, ?, ?, ?, 'bill', 'vendor',
        'FREIGHT', 'SGN-HAN', 120.0, 'actual_or_formula',
        1740000.0, 1.0, 'VND', ?, 'rated', '{"rateDateSourceLabel":"Theo ngày khởi hành (ETD) của Bill"}', ?, ?
    )
""", (rating_id, tenant_id, bill_id, v2_id, card_id, now_str, uuid.uuid4().bytes, now_str))

cur.execute("""
    INSERT INTO rating_details (
        id, tenant_id, rating_id, pricing_rule_id, rule_code, component_code, component_name,
        financial_nature, financial_maturity, amount, net_amount, vat_rate, vat_amount, gross_amount,
        currency_code, source_type, formula_text, row_version, created_at
    ) VALUES (
        ?, ?, ?, ?, 'QT0001', 'FREIGHT', 'Cước chính',
        'cost', 'expected', 1440000.0, 1440000.0, 8.0, 115200.0, 1555200.0,
        'VND', 'pricing_rule', '120kg × 12.000đ/kg', ?, ?
    )
""", (detail_freight_id, tenant_id, rating_id, v2_rule_id, uuid.uuid4().bytes, now_str))

cur.execute("""
    INSERT INTO rating_details (
        id, tenant_id, rating_id, rule_code, component_code, component_name,
        financial_nature, financial_maturity, amount, net_amount, vat_rate, vat_amount, gross_amount,
        currency_code, formula_text, source_type, source_id, source_version_id, row_version, created_at
    ) VALUES (
        ?, ?, ?, 'DONGGOI', 'PACKING', 'Phụ phí đóng gói',
        'cost', 'expected', 300000.0, 300000.0, 8.0, 24000.0, 324000.0,
        'VND', 'Phụ phí đóng gói cố định 300.000đ', 'surcharge', ?, ?, ?, ?
    )
""", (detail_packing_id, tenant_id, rating_id, surcharge_id, surcharge_version_id, uuid.uuid4().bytes, now_str))

# 7. Costs: Freight & Surcharge
cost_freight_id = '01A16DD7-C111-78AA-A9C8-509E0224A2CF'
cost_packing_id = '01A16DD7-C222-78AA-A9C8-509E0224A2CF'

cur.execute("DELETE FROM costs WHERE id IN (?, ?)", (cost_freight_id, cost_packing_id))

cur.execute("""
    INSERT INTO costs (
        id, tenant_id, bill_id, cost_type_code, source_type, source_id,
        rate_card_id, rate_version_id, pricing_rule_id, financial_maturity,
        amount, expected_amount, net_amount, vat_rate, vat_amount, gross_amount, currency_code,
        partner_override_requires_rerate, fx_status, effective_date,
        attribution_type, record_status, approval_status, row_version, created_at
    ) VALUES (
        ?, ?, ?, 'FREIGHT', 'rating_detail', ?,
        ?, ?, ?, 'expected',
        1440000.0, 1440000.0, 1440000.0, 8.0, 115200.0, 1555200.0, 'VND',
        0, 'not_applicable', '2026-10-26',
        'direct', 'active', 'pending', ?, ?
    )
""", (cost_freight_id, tenant_id, bill_id, detail_freight_id, card_id, v2_id, v2_rule_id, uuid.uuid4().bytes, now_str))

cur.execute("""
    INSERT INTO costs (
        id, tenant_id, bill_id, cost_type_code, source_type, source_id,
        rate_card_id, rate_version_id, financial_maturity,
        amount, expected_amount, net_amount, vat_rate, vat_amount, gross_amount,
        partner_override_requires_rerate, fx_status, effective_date,
        currency_code, attribution_type, record_status, approval_status, row_version, created_at
    ) VALUES (
        ?, ?, ?, 'DONGGOI', 'rating_detail', ?,
        ?, ?, 'expected',
        300000.0, 300000.0, 300000.0, 8.0, 24000.0, 324000.0,
        0, 'not_applicable', '2026-10-26',
        'VND', 'direct', 'active', 'pending', ?, ?
    )
""", (cost_packing_id, tenant_id, bill_id, detail_packing_id, card_id, v2_id, uuid.uuid4().bytes, now_str))

con.commit()
con.close()
print("UAT screenshot data prepared successfully.")
