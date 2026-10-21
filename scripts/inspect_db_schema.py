import re

with open('src/LCMS.Infrastructure/Persistence/Migrations/LcmsDbContextModelSnapshot.cs', 'r', encoding='utf-8') as f:
    text = f.read()

tables = re.findall(r'b\.ToTable\((\"[^\"]+\")', text)
print(f"Total tables in ModelSnapshot: {len(set(tables))}")
for t in sorted(set(tables)):
    print(t)

