import sqlite3
import sys
import uuid

sys.stdout.reconfigure(encoding='utf-8')

for db_path in ['lcms_dev.db', 'src/LCMS.Api/lcms_dev.db']:
    print(f"--- Migrating {db_path} ---")
    con = sqlite3.connect(db_path)
    cur = con.cursor()

    # 1. pricing_rules.is_bidirectional
    cur.execute('PRAGMA table_info(pricing_rules)')
    existing_pr = {r[1] for r in cur.fetchall()}
    if 'is_bidirectional' not in existing_pr:
        print("  Adding is_bidirectional to pricing_rules...")
        cur.execute("ALTER TABLE pricing_rules ADD COLUMN is_bidirectional INTEGER NOT NULL DEFAULT 0")

    # 2. costs columns
    cur.execute('PRAGMA table_info(costs)')
    existing_costs = {r[1] for r in cur.fetchall()}
    for col, col_type in [
        ('economic_identity_key', 'TEXT'),
        ('service_occurrence_key', 'TEXT'),
        ('applied_from_rating_id', 'TEXT')
    ]:
        if col not in existing_costs:
            print(f"  Adding {col} to costs...")
            cur.execute(f"ALTER TABLE costs ADD COLUMN {col} {col_type}")

    # 3. revenues columns
    cur.execute('PRAGMA table_info(revenues)')
    existing_rev = {r[1] for r in cur.fetchall()}
    for col, col_type in [
        ('economic_identity_key', 'TEXT'),
        ('service_occurrence_key', 'TEXT'),
        ('applied_from_rating_id', 'TEXT')
    ]:
        if col not in existing_rev:
            print(f"  Adding {col} to revenues...")
            cur.execute(f"ALTER TABLE revenues ADD COLUMN {col} {col_type}")

    # 4. rating_details columns
    cur.execute('PRAGMA table_info(rating_details)')
    existing_rd = {r[1] for r in cur.fetchall()}
    for col, col_type in [
        ('application_bucket', 'TEXT'),
        ('activation_policy_snapshot', 'TEXT'),
        ('activation_evidence_ref', 'TEXT'),
        ('reason_code', 'TEXT'),
        ('reason_message_vi', 'TEXT'),
        ('calculation_basis', 'TEXT'),
        ('applied_quantity', 'NUMERIC'),
        ('source_rule_id', 'TEXT'),
    ]:
        if col not in existing_rd:
            print(f"  Adding {col} to rating_details...")
            cur.execute(f"ALTER TABLE rating_details ADD COLUMN {col} {col_type}")

    # 5. service_selections columns
    cur.execute('PRAGMA table_info(service_selections)')
    existing_ss = {r[1] for r in cur.fetchall()}
    for col, col_type in [
        ('uom', 'TEXT'),
        ('source_ref', 'TEXT'),
        ('deleted_by', 'TEXT'),
        ('row_version', 'BLOB')
    ]:
        if col not in existing_ss:
            print(f"  Adding {col} to service_selections...")
            cur.execute(f"ALTER TABLE service_selections ADD COLUMN {col} {col_type}")

    # 6. service_selection_audits columns
    cur.execute('PRAGMA table_info(service_selection_audits)')
    existing_ssa = {r[1] for r in cur.fetchall()}
    for col, col_type in [
        ('service_selection_id', 'TEXT'),
        ('row_version', 'BLOB')
    ]:
        if col not in existing_ssa:
            print(f"  Adding {col} to service_selection_audits...")
            cur.execute(f"ALTER TABLE service_selection_audits ADD COLUMN {col} {col_type}")
    if 'selection_id' in existing_ssa:
        cur.execute("UPDATE service_selection_audits SET service_selection_id = selection_id WHERE service_selection_id IS NULL")

    # 7. EconomicChargeType DONGGO
    TENANT_ID = '01A16CF7-2EF2-7299-9730-0EC040B35EDA'
    cur.execute("SELECT id FROM economic_charge_types WHERE code='DONGGO' AND tenant_id=?", (TENANT_ID,))
    row = cur.fetchone()
    if not row:
        donggo_id = str(uuid.uuid4()).upper()
        print(f"  Inserting DONGGO economic charge type with id {donggo_id}...")
        cur.execute("""
            INSERT INTO economic_charge_types (id, tenant_id, code, name, is_active, created_at, row_version)
            VALUES (?, ?, 'DONGGO', 'Đóng gói hàng hóa', 1, datetime('now'), ?)
        """, (donggo_id, TENANT_ID, uuid.uuid4().bytes))
    else:
        donggo_id = row[0]
        print(f"  DONGGO already exists with id {donggo_id}")

    # 8. Surcharge versions link to DONGGO
    cur.execute("""
        UPDATE surcharge_versions 
        SET economic_charge_type_id = ?
        WHERE surcharge_id = '8183C1BE-D606-4E24-ADF7-D4DA75A8F438'
    """, (donggo_id,))
    print(f"  Updated {cur.rowcount} surcharge versions to link to DONGGO.")

    con.commit()
    con.close()
    print("  Done.")

print("All migrations completed!")
