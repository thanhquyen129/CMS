# -*- coding: utf-8 -*-
with open('src/LCMS.Api/Endpoints/TenantBillEndpoints.cs', 'r', encoding='utf-8') as f:
    text = f.read()

print("--- PrefillBillFromOrdersRequest ---")
for l in text.splitlines():
    if 'PrefillBillFromOrdersRequest' in l:
        print(l)

print("--- PreviewShipmentRollupRequest ---")
with open('src/LCMS.Api/Endpoints/OperationalReferenceEndpoints.cs', 'r', encoding='utf-8') as f:
    text2 = f.read()
for l in text2.splitlines():
    if 'PreviewShipmentRollupRequest' in l or 'preview-rollup' in l:
        print(l)

print("--- PutOrderRequest ---")
for l in text2.splitlines():
    if 'record UpsertOrderRequest' in l or 'record PutOrderRequest' in l:
        print(l)

