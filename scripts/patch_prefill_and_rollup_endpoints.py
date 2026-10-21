# -*- coding: utf-8 -*-
# 1. Patch TenantBillEndpoints.cs
with open('src/LCMS.Api/Endpoints/TenantBillEndpoints.cs', 'rb') as f:
    text = f.read().decode('utf-8')

t1 = """        bills.MapPost("/prefill-from-orders", async (PrefillBillFromOrdersRequest body, ISender sender, CancellationToken ct) =>
        {
            var prefill = await sender.Send(new PrefillBillFromOrdersQuery(body.OrderIds), ct);
            return Results.Ok(prefill);
        });"""

r1 = """        bills.MapPost("/prefill-from-orders", async (PrefillBillFromOrdersRequest body, ISender sender, CancellationToken ct) =>
        {
            var prefill = await sender.Send(new PrefillBillFromOrdersQuery(body.OrderIds, body.UserOverriddenFields), ct);
            return Results.Ok(prefill);
        });"""

t2 = """public sealed record PrefillBillFromOrdersRequest(IReadOnlyList<Guid> OrderIds);"""
r2 = """public sealed record PrefillBillFromOrdersRequest(
    IReadOnlyList<Guid> OrderIds,
    IReadOnlyDictionary<string, string>? UserOverriddenFields = null);"""

if t1 not in text:
    t1 = t1.replace('\n', '\r\n')
    r1 = r1.replace('\n', '\r\n')
if t2 not in text:
    t2 = t2.replace('\n', '\r\n')
    r2 = r2.replace('\n', '\r\n')

text = text.replace(t1, r1).replace(t2, r2)
with open('src/LCMS.Api/Endpoints/TenantBillEndpoints.cs', 'wb') as f:
    f.write(text.encode('utf-8'))
print("Patched TenantBillEndpoints.cs")

# 2. Patch OperationalReferenceEndpoints.cs
with open('src/LCMS.Api/Endpoints/OperationalReferenceEndpoints.cs', 'rb') as f:
    text2 = f.read().decode('utf-8')

t3 = """        shipments.MapPost("/preview-rollup", async (PreviewShipmentRollupRequest body, ISender sender, CancellationToken ct) =>
        {
            var rollup = await sender.Send(new PreviewShipmentRollupQuery(body.BillIds), ct);
            return Results.Ok(rollup);
        });"""

r3 = """        shipments.MapPost("/preview-rollup", async (PreviewShipmentRollupRequest body, ISender sender, CancellationToken ct) =>
        {
            var rollup = await sender.Send(new PreviewShipmentRollupQuery(body.BillIds, body.AllocationItems), ct);
            return Results.Ok(rollup);
        });"""

t4 = """public sealed record PreviewShipmentRollupRequest(IReadOnlyList<Guid> BillIds);"""
r4 = """public sealed record PreviewShipmentRollupRequest(
    IReadOnlyList<Guid>? BillIds = null,
    IReadOnlyList<LCMS.Application.Shipments.Queries.BillRollupAllocationItem>? AllocationItems = null);"""

if t3 not in text2:
    t3 = t3.replace('\n', '\r\n')
    r3 = r3.replace('\n', '\r\n')
if t4 not in text2:
    t4 = t4.replace('\n', '\r\n')
    r4 = r4.replace('\n', '\r\n')

text2 = text2.replace(t3, r3).replace(t4, r4)
with open('src/LCMS.Api/Endpoints/OperationalReferenceEndpoints.cs', 'wb') as f:
    f.write(text2.encode('utf-8'))
print("Patched OperationalReferenceEndpoints.cs")
