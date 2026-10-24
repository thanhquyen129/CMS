import sqlite3
import uuid
import sys

sys.stdout.reconfigure(encoding='utf-8')

con = sqlite3.connect("src/LCMS.Api/lcms_dev.db")
cur = con.cursor()
cur.execute("SELECT id FROM tenants")
tids = [r[0] for r in cur.fetchall()]
modes = [
    ("transport_mode", "air", "Đường hàng không", 10),
    ("transport_mode", "sea", "Đường biển", 20),
    ("transport_mode", "road", "Đường bộ", 30),
    ("transport_mode", "rail", "Đường sắt", 40),
]
for tid in tids:
    for kind, code, name, sort in modes:
        row_id = str(uuid.uuid4()).upper()
        # check if exists
        cur.execute("SELECT id FROM master_catalog_items WHERE tenant_id = ? AND kind = ? AND code = ?", (tid, kind, code))
        if not cur.fetchone():
            cur.execute("""
                INSERT INTO master_catalog_items (id, tenant_id, kind, code, name, sort_order, is_active, row_version, created_at)
                VALUES (?, ?, ?, ?, ?, ?, 1, ?, datetime('now'))
            """, (row_id, tid, kind, code, name, sort, uuid.uuid4().bytes))
con.commit()

cur.execute("SELECT kind, code, name FROM master_catalog_items WHERE kind = 'transport_mode'")
print("Transport modes in sqlite:", cur.fetchall())
con.close()
