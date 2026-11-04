import sqlite3

con = sqlite3.connect('src/LCMS.Api/lcms_dev.db')
cur = con.cursor()

# Check surcharge_versions columns
cur.execute('PRAGMA table_info(surcharge_versions)')
existing_cols = {r[1] for r in cur.fetchall()}

cols_to_add = [
    ('activation_policy', 'TEXT NOT NULL DEFAULT "UNCLASSIFIED_LEGACY"'),
    ('activation_config_json', 'TEXT'),
    ('approval_ref', 'TEXT'),
    ('approved_by_user_id', 'TEXT'),
    ('approved_at', 'TEXT'),
]

for col_name, col_def in cols_to_add:
    if col_name not in existing_cols:
        print(f"Adding column {col_name} to surcharge_versions...")
        cur.execute(f"ALTER TABLE surcharge_versions ADD COLUMN {col_name} {col_def}")

# Create service_selections table if not exists
cur.execute("""
CREATE TABLE IF NOT EXISTS service_selections (
    id TEXT PRIMARY KEY,
    tenant_id TEXT NOT NULL,
    context_type TEXT NOT NULL,
    context_id TEXT NOT NULL,
    service_code TEXT NOT NULL,
    scope TEXT NOT NULL,
    quantity NUMERIC NOT NULL DEFAULT 1,
    is_selected INTEGER NOT NULL DEFAULT 0,
    revision INTEGER NOT NULL DEFAULT 1,
    confirmed_by_user_id TEXT,
    confirmed_at TEXT,
    notes TEXT,
    created_at TEXT NOT NULL,
    created_by TEXT,
    updated_at TEXT,
    updated_by TEXT,
    deleted_at TEXT,
    deleted_by TEXT
)
""")

# Create service_selection_audits table if not exists
cur.execute("""
CREATE TABLE IF NOT EXISTS service_selection_audits (
    id TEXT PRIMARY KEY,
    tenant_id TEXT NOT NULL,
    selection_id TEXT NOT NULL,
    context_type TEXT NOT NULL,
    context_id TEXT NOT NULL,
    service_code TEXT NOT NULL,
    old_is_selected INTEGER NOT NULL,
    new_is_selected INTEGER NOT NULL,
    old_quantity NUMERIC NOT NULL,
    new_quantity NUMERIC NOT NULL,
    revision INTEGER NOT NULL,
    actor_user_id TEXT,
    acted_at TEXT NOT NULL,
    notes TEXT,
    created_at TEXT NOT NULL,
    created_by TEXT,
    updated_at TEXT,
    updated_by TEXT,
    deleted_at TEXT,
    deleted_by TEXT
)
""")

con.commit()
con.close()
print("lcms_dev.db schema updated successfully")

