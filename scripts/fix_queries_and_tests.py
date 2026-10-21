# -*- coding: utf-8 -*-
with open('src/LCMS.Application/Bills/Queries/PrefillBillFromOrdersQuery.cs', 'r', encoding='utf-8') as f:
    text = f.read()
text = text.replace('.OrderBy(o => o.CreatedAt)', '.OrderBy(o => o.Id)')
with open('src/LCMS.Application/Bills/Queries/PrefillBillFromOrdersQuery.cs', 'w', encoding='utf-8', newline='\r\n') as f:
    f.write(text)

with open('src/LCMS.Application/Shipments/Queries/PreviewShipmentRollupQuery.cs', 'r', encoding='utf-8') as f:
    text = f.read()
text = text.replace('.OrderBy(b => b.CreatedAt)', '.OrderBy(b => b.Id)')
with open('src/LCMS.Application/Shipments/Queries/PreviewShipmentRollupQuery.cs', 'w', encoding='utf-8', newline='\r\n') as f:
    f.write(text)

print('Updated PrefillBillFromOrdersQuery and PreviewShipmentRollupQuery to order by Id')

