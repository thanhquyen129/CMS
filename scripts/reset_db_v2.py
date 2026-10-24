import sqlite3
import sys
import uuid

sys.stdout.reconfigure(encoding='utf-8')

con = sqlite3.connect("src/LCMS.Api/lcms_dev.db")
cur = con.cursor()

card_id = "01A16CF7-3B64-7606-8B4D-2158CEA9D954"

cur.execute("SELECT tenant_id FROM rate_cards WHERE id = ?", (card_id,))
tenant_id = cur.fetchone()[0]
print(f"Using tenant_id: {tenant_id} for card {card_id}")

# Clean existing routes/locations for SGN-HAN to ensure correct tenant_id
cur.execute("DELETE FROM routes WHERE code = 'SGN-HAN'")
cur.execute("DELETE FROM locations WHERE code IN ('SGN', 'HAN')")

# 1. Locations SGN & HAN
sgn_id = str(uuid.uuid4()).upper()
cur.execute("""
    INSERT INTO locations (id, tenant_id, code, name, location_type, country_code, city, iata_code, is_active, row_version, created_at)
    VALUES (?, ?, 'SGN', 'Hồ Chí Minh (Tân Sơn Nhất)', 'airport', 'VN', 'Hồ Chí Minh', 'SGN', 1, ?, datetime('now'))
""", (sgn_id, tenant_id, uuid.uuid4().bytes))

han_id = str(uuid.uuid4()).upper()
cur.execute("""
    INSERT INTO locations (id, tenant_id, code, name, location_type, country_code, city, iata_code, is_active, row_version, created_at)
    VALUES (?, ?, 'HAN', 'Hà Nội (Nội Bài)', 'airport', 'VN', 'Hà Nội', 'HAN', 1, ?, datetime('now'))
""", (han_id, tenant_id, uuid.uuid4().bytes))

# 2. Route SGN-HAN
route_id = str(uuid.uuid4()).upper()
cur.execute("""
    INSERT INTO routes (id, tenant_id, code, name, origin_location_id, destination_location_id, transport_mode_code, service_type_code, is_active, row_version, created_at)
    VALUES (?, ?, 'SGN-HAN', 'SGN-HAN', ?, ?, 'air', 'FREIGHT', 1, ?, datetime('now'))
""", (route_id, tenant_id, sgn_id, han_id, uuid.uuid4().bytes))

print(f"Ensured SGN={sgn_id}, HAN={han_id}, Route SGN-HAN={route_id}")

# 3. Reset TDNT_SGN_HAN Card: supplier_party_id = NULL (Đối tác: — per PO #1)
cur.execute("""
    UPDATE rate_cards 
    SET route_id = ?, route_code = 'SGN-HAN', supplier_party_id = NULL, carrier_name = 'VN Cargo'
    WHERE code = 'TDNT_SGN_HAN'
""", (route_id,))

# 4. Delete versions > 1
cur.execute("DELETE FROM pricing_rule_components WHERE pricing_rule_id IN (SELECT id FROM pricing_rules WHERE rate_version_id IN (SELECT id FROM rate_versions WHERE rate_card_id = ? AND version_no > 1))", (card_id,))
cur.execute("DELETE FROM pricing_rules WHERE rate_version_id IN (SELECT id FROM rate_versions WHERE rate_card_id = ? AND version_no > 1)", (card_id,))
cur.execute("DELETE FROM rate_versions WHERE rate_card_id = ? AND version_no > 1", (card_id,))

# 5. Reset v1 Rule QT0001
cur.execute("""
    UPDATE pricing_rules 
    SET name = 'SGN-HAN',
        route_id = ?,
        route_code = 'SGN-HAN',
        applies_any_route = 0,
        carrier_name = NULL,
        service_type_code = 'FREIGHT',
        transport_mode = 'air',
        weight_min = 1.0,
        weight_max = 120.0,
        unit_amount = 12000.0,
        charge_code = 'FREIGHT',
        is_active = 1
    WHERE rate_version_id IN (SELECT id FROM rate_versions WHERE rate_card_id = ? AND version_no = 1)
""", (route_id, card_id))

# 6. Ensure DEMO-01-HUB has SGN-HAN route
cur.execute("""
    UPDATE bills 
    SET route_id = ?, route_code = 'SGN-HAN', origin_code = 'SGN', destination_code = 'HAN', transport_mode = 'air', service_type_code = 'FREIGHT'
    WHERE bill_no = 'DEMO-01-HUB'
""", (route_id,))

con.commit()

cur.execute("SELECT id, version_no, status FROM rate_versions WHERE rate_card_id = ?", (card_id,))
print("Versions after reset:", cur.fetchall())
cur.execute("SELECT code, name, route_code, route_id, applies_any_route, weight_min, weight_max, unit_amount FROM pricing_rules WHERE rate_version_id IN (SELECT id FROM rate_versions WHERE rate_card_id = ? AND version_no = 1)", (card_id,))
print("v1 Rules:", cur.fetchall())

con.close()
