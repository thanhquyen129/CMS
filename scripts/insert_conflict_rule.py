import sqlite3, uuid, sys

card_id = (sys.argv[1] if len(sys.argv) > 1 else '01A16CF7-3B64-7606-8B4D-2158CEA9D954').upper()
con = sqlite3.connect('src/LCMS.Api/lcms_dev.db')
cur = con.cursor()
cur.execute("SELECT id, tenant_id FROM rate_versions WHERE rate_card_id = ? AND status = 'draft'", (card_id,))
row = cur.fetchone()
if not row:
    print('No draft version found!')
    sys.exit(1)
vid, tid = row
cur.execute("SELECT service_type_code, route_id, applies_any_route, carrier_party_id, carrier_name, transport_mode, charge_code, line_role FROM pricing_rules WHERE rate_version_id = ? AND code = 'QT0001'", (vid,))
r1 = cur.fetchone()
svc, route_id, any_route, carrier_id, carrier_name, mode, charge_code, line_role = r1

rid = str(uuid.uuid4()).upper()
cur.execute('''
    INSERT INTO pricing_rules (id, tenant_id, rate_version_id, code, name, calc_method, unit_amount, currency_code, service_type_code, route_id, applies_any_route, carrier_party_id, carrier_name, weight_basis, weight_min, weight_max, charge_code, line_role, transport_mode, sort_order, is_active, created_at, created_by, row_version)
    VALUES (?, ?, ?, 'QT0002', 'Cước trùng lặp', 'unit_rate', '15000', 'VND', ?, ?, ?, ?, ?, 'chargeable', '1.0', '100.0', ?, ?, 'air', 20, 1, '2026-10-24', '01A16CF9-0A27-7EBB-B1D1-525CB8B7D648', X'0102030405060708')
''', (rid, tid, vid, svc, route_id, any_route, carrier_id, carrier_name, charge_code, line_role))
cur.execute('''
    INSERT INTO pricing_rule_components (id, tenant_id, pricing_rule_id, code, name, financial_nature, cost_type_code, amount, currency_code, sort_order, created_at, created_by, row_version)
    VALUES (?, ?, ?, 'FREIGHT', 'Cước trùng lặp', 'cost', 'FREIGHT', '15000', 'VND', 0, '2026-10-24', '01A16CF9-0A27-7EBB-B1D1-525CB8B7D648', X'0102030405060708')
''', (str(uuid.uuid4()).upper(), tid, rid))
con.commit()
con.close()
print('Successfully inserted conflicting rule QT0002')
