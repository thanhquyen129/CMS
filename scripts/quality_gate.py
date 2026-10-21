import sys
from generate_field_inventory_data import get_field_inventory_rows
from generate_matrix_data import get_matrix_rows

sys.stdout.reconfigure(encoding='utf-8')

s1 = get_field_inventory_rows()
s2 = get_matrix_rows()

print(f"Sheet 1 total rows: {len(s1)}")
print(f"Sheet 2 total rows: {len(s2)}")

# 1. Check empty cells in Sheet 1
s1_empty = []
for r_idx, row in enumerate(s1, 1):
    if len(row) != 15:
        s1_empty.append((r_idx, f"Row length {len(row)} != 15"))
    for c_idx, val in enumerate(row):
        if val is None or str(val).strip() == "" or str(val).lower() == "nan":
            s1_empty.append((r_idx, c_idx, f"Empty cell at col {c_idx}"))

print(f"Sheet 1 empty/NaN cells: {len(s1_empty)}")
if s1_empty:
    for err in s1_empty[:10]:
        print("  S1 ERR:", err)

# 2. Check empty cells in Sheet 2
s2_empty = []
for r_idx, row in enumerate(s2, 1):
    if len(row) != 9:
        s2_empty.append((r_idx, f"Row length {len(row)} != 9"))
    for c_idx, val in enumerate(row):
        if val is None or str(val).strip() == "" or str(val).lower() == "nan":
            s2_empty.append((r_idx, c_idx, f"Empty cell at col {c_idx}"))

print(f"Sheet 2 empty/NaN cells: {len(s2_empty)}")
if s2_empty:
    for err in s2_empty[:10]:
        print("  S2 ERR:", err)

# 3. Screen coverage check in Sheet 1
screen_counts = {}
for r in s1:
    scr = r[1]
    screen_counts[scr] = screen_counts.get(scr, 0) + 1

print("\n--- Screen Field Counts in Sheet 1 ---")
low_coverage = []
for scr, count in sorted(screen_counts.items(), key=lambda x: x[1]):
    print(f"  {scr}: {count} fields")
    if count < 4:
        low_coverage.append((scr, count))

if low_coverage:
    print(f"\nWARNING: Screens with < 4 fields: {low_coverage}")
else:
    print("\nSUCCESS: All screens have >= 4 granular fields!")

