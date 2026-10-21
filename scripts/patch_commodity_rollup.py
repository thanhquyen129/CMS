# -*- coding: utf-8 -*-
with open('src/LCMS.Application/Shipments/Queries/PreviewShipmentRollupQuery.cs', 'rb') as f:
    text = f.read().decode('utf-8')

target = """        // 4. Commodities (UT-10, GAP-07)
        var distinctCommodities = bills.Where(b => b.CommodityTypeId.HasValue)
            .Select(b => b.CommodityTypeId!.Value)
            .Distinct().ToList();"""

replacement = """        // 4. Commodities (UT-10, GAP-07)
        var distinctCommodities = bills
            .Select(b => b.CommodityTypeId ?? OperationalContextJson.Deserialize(b.ContextJson)?.CommodityTypeId)
            .Where(c => c.HasValue)
            .Select(c => c!.Value)
            .Distinct().ToList();"""

if target not in text:
    target = target.replace('\n', '\r\n')
    replacement = replacement.replace('\n', '\r\n')

if target in text:
    text = text.replace(target, replacement)
    with open('src/LCMS.Application/Shipments/Queries/PreviewShipmentRollupQuery.cs', 'wb') as f:
        f.write(text.encode('utf-8'))
    print("Patched PreviewShipmentRollupQuery successfully")
else:
    print("Target not found in PreviewShipmentRollupQuery")

