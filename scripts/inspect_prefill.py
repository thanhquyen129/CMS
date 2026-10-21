with open('src/LCMS.Application/Bills/Queries/PrefillBillFromOrdersQuery.cs', 'r', encoding='utf-8') as f:
    text = f.read()

import re
print('FieldStatuses assignments:')
for m in re.finditer(r'fieldStatuses\["([^"]+)"\]\s*=\s*"([^"]+)"', text):
    print(m.group(0))

print('\nNotice Message checks:')
for l in text.splitlines():
    if 'notice' in l.lower() or 'missing' in l.lower() or 'thiếu' in l.lower():
        print(l.strip())

