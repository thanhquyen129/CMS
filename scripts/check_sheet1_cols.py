import re
from generate_field_inventory_data import get_field_inventory_rows

rows = get_field_inventory_rows()
db_cols = set()
for r in rows:
    db_col = r[5].strip() # 5th index is DB col
    db_cols.add(db_col)

print(f"Total unique DB columns in Sheet 1: {len(db_cols)}")
for c in sorted(db_cols)[:20]:
    print(c)

