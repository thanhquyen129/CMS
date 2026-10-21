from generate_field_inventory_data import get_field_inventory_rows
from generate_matrix_data import get_matrix_rows

s1 = get_field_inventory_rows()
s2 = get_matrix_rows()

# Extract all DB cols in s1
s1_db_cols = set(r[5].strip() for r in s1)

missing_sources = set()
missing_targets = set()

for r in s2:
    src = r[1].strip()
    tgt = r[2].strip()
    if src not in s1_db_cols:
        missing_sources.add(src)
    if tgt not in s1_db_cols:
        missing_targets.add(tgt)

print(f"Missing sources in S1: {len(missing_sources)}")
for m in sorted(missing_sources):
    print("  SRC:", m)

print(f"Missing targets in S1: {len(missing_targets)}")
for m in sorted(missing_targets):
    print("  TGT:", m)

if not missing_sources and not missing_targets:
    print("SUCCESS: 100% Referential Integrity between Sheet 2 and Sheet 1!")

