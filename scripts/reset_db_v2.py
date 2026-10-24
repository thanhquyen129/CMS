import sqlite3
import sys

sys.stdout.reconfigure(encoding='utf-8')

con = sqlite3.connect("src/LCMS.Api/lcms_dev.db")
cur = con.cursor()
cur.execute("""
    SELECT bp.id, bp.name, bp.code 
    FROM business_parties bp
    JOIN party_roles pr ON pr.party_id = bp.id
    WHERE pr.role_code = 'vendor'
""")
vendors = cur.fetchall()
print("Vendors found:", vendors)

if vendors:
    vendor_id = vendors[0][0]
    cur.execute("UPDATE rate_cards SET supplier_party_id = ? WHERE code = 'TDNT_SGN_HAN'", (vendor_id,))
    print(f"Set TDNT_SGN_HAN supplier_party_id to {vendor_id} ({vendors[0][1]})")

# Delete any version > 1 (draft or published)
card_id = '01A16CF7-3B64-7606-8B4D-2158CEA9D954'
cur.execute("DELETE FROM pricing_rule_components WHERE pricing_rule_id IN (SELECT id FROM pricing_rules WHERE rate_version_id IN (SELECT id FROM rate_versions WHERE rate_card_id = ? AND version_no > 1))", (card_id,))
cur.execute("DELETE FROM pricing_rules WHERE rate_version_id IN (SELECT id FROM rate_versions WHERE rate_card_id = ? AND version_no > 1)", (card_id,))
cur.execute("DELETE FROM rate_versions WHERE rate_card_id = ? AND version_no > 1", (card_id,))

# Ensure v1 rule allows general rating
cur.execute("""
    UPDATE pricing_rules 
    SET carrier_name = NULL, service_type_code = NULL 
    WHERE rate_version_id IN (SELECT id FROM rate_versions WHERE rate_card_id = ? AND version_no = 1)
""", (card_id,))
con.commit()

cur.execute("SELECT id, version_no, status FROM rate_versions WHERE rate_card_id = ?", (card_id,))
print("Versions after reset:", cur.fetchall())
con.close()

