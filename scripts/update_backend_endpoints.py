# -*- coding: utf-8 -*-
"""
Script cập nhật các endpoint Minimal API của Backend:
1. TenantBillEndpoints.cs -> POST /api/bills/prefill-from-orders
2. OperationalReferenceEndpoints.cs ->
   - POST /api/shipments/preview-rollup
   - GET /api/shipments/{id}/next-leg-context
   - POST /api/transport-movements/prefill-from-legs
3. SurchargeCommands.cs -> rate card validation
4. EconomicChargeEndpoints.cs -> GET /api/economic-charge-types/{id}/accounting-mapping
5. CostEndpoints.cs -> GET /api/costs/shared/allocation-context
"""

# 1. TenantBillEndpoints.cs
bill_file = 'src/LCMS.Api/Endpoints/TenantBillEndpoints.cs'
with open(bill_file, 'r', encoding='utf-8') as f:
    text = f.read()

target1 = """        var bills = app.MapGroup("/api/bills").WithTags("Bills");
        bills.MapPost("/", async (CreateBillRequest body, ISender sender, CancellationToken ct) =>"""
repl1 = """        var bills = app.MapGroup("/api/bills").WithTags("Bills");
        bills.MapPost("/prefill-from-orders", async (PrefillBillFromOrdersRequest body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new PrefillBillFromOrdersQuery(body.OrderIds), ct);
            return Results.Ok(result);
        });
        bills.MapPost("/", async (CreateBillRequest body, ISender sender, CancellationToken ct) =>"""

req_target = "public sealed record CreateBillRequest("
req_repl = "public sealed record PrefillBillFromOrdersRequest(IReadOnlyList<Guid> OrderIds);\n\npublic sealed record CreateBillRequest("

if "prefill-from-orders" not in text:
    text = text.replace(target1, repl1, 1).replace(req_target, req_repl, 1)
    with open(bill_file, 'w', encoding='utf-8', newline='\r\n') as f:
        f.write(text)
    print("Updated TenantBillEndpoints.cs")
else:
    print("TenantBillEndpoints.cs already has prefill-from-orders")

# 2. OperationalReferenceEndpoints.cs
ops_file = 'src/LCMS.Api/Endpoints/OperationalReferenceEndpoints.cs'
with open(ops_file, 'r', encoding='utf-8') as f:
    text = f.read()

target_shp = """        var shipments = app.MapGroup("/api/shipments").WithTags("Shipments");
        shipments.MapPut("/", async (UpsertShipmentRequest body, ISender sender, CancellationToken ct) =>"""

repl_shp = """        var shipments = app.MapGroup("/api/shipments").WithTags("Shipments");
        shipments.MapPost("/preview-rollup", async (PreviewShipmentRollupRequest body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new PreviewShipmentRollupQuery(body.BillIds), ct);
            return Results.Ok(result);
        });
        shipments.MapGet("/{id:guid}/next-leg-context", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetNextLegContextQuery(id), ct);
            return Results.Ok(result);
        });
        shipments.MapPut("/", async (UpsertShipmentRequest body, ISender sender, CancellationToken ct) =>"""

target_mov = """        var movements = app.MapGroup("/api/transport-movements").WithTags("TransportMovements");
        movements.MapPut("/", async (UpsertTransportMovementRequest body, ISender sender, CancellationToken ct) =>"""

repl_mov = """        var movements = app.MapGroup("/api/transport-movements").WithTags("TransportMovements");
        movements.MapPost("/prefill-from-legs", async (PrefillMovementFromLegsRequest body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new PrefillMovementFromLegsQuery(body.LegIds), ct);
            return Results.Ok(result);
        });
        movements.MapPut("/", async (UpsertTransportMovementRequest body, ISender sender, CancellationToken ct) =>"""

req_target_ops = "public sealed record UpsertShipmentRequest("
req_repl_ops = """public sealed record PreviewShipmentRollupRequest(IReadOnlyList<Guid> BillIds);
public sealed record PrefillMovementFromLegsRequest(IReadOnlyList<Guid> LegIds);

public sealed record UpsertShipmentRequest("""

if "preview-rollup" not in text:
    text = text.replace(target_shp, repl_shp, 1).replace(target_mov, repl_mov, 1).replace(req_target_ops, req_repl_ops, 1)
    with open(ops_file, 'w', encoding='utf-8', newline='\r\n') as f:
        f.write(text)
    print("Updated OperationalReferenceEndpoints.cs")
else:
    print("OperationalReferenceEndpoints.cs already has preview-rollup")

# 3. SurchargeCommands.cs rate card validation
sur_file = 'src/LCMS.Application/Surcharges/SurchargeCommands.cs'
with open(sur_file, 'r', encoding='utf-8') as f:
    text = f.read()

sur_target = """        var code = request.Code.Trim();
        if (await _db.Surcharges.AnyAsync(s => s.Code == code, cancellationToken))
        {
            throw new ConflictAppException("Mã phụ phí đã tồn tại trong thuê bao này.");
        }"""

sur_repl = """        if (request.Rule.RateCardId.HasValue)
        {
            var rateCard = await _db.RateCards.AsNoTracking()
                .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == request.Rule.RateCardId.Value, cancellationToken);
            if (rateCard != null)
            {
                if (string.Equals(rateCard.PartyType, "vendor", StringComparison.OrdinalIgnoreCase) && direction == SurchargeDirections.Sell)
                {
                    throw new ConflictAppException("Phụ phí gắn với Bảng giá MUA (vendor) không thể khai báo chiều SELL.");
                }
                if (string.Equals(rateCard.PartyType, "customer", StringComparison.OrdinalIgnoreCase) && direction == SurchargeDirections.Buy)
                {
                    throw new ConflictAppException("Phụ phí gắn với Bảng giá BÁN (customer) không thể khai báo chiều BUY.");
                }
                if (!string.Equals(rateCard.CurrencyCode, request.Rule.CurrencyCode, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ConflictAppException($"Đơn vị tiền tệ của Phụ phí ({request.Rule.CurrencyCode}) phải trùng với Bảng giá ({rateCard.CurrencyCode}).");
                }
            }
        }

        var code = request.Code.Trim();
        if (await _db.Surcharges.AnyAsync(s => s.Code == code, cancellationToken))
        {
            throw new ConflictAppException("Mã phụ phí đã tồn tại trong thuê bao này.");
        }"""

if "Phụ phí gắn với Bảng giá MUA" not in text:
    text = text.replace(sur_target, sur_repl, 1)
    with open(sur_file, 'w', encoding='utf-8', newline='\r\n') as f:
        f.write(text)
    print("Updated SurchargeCommands.cs with Rate Card validation")
else:
    print("SurchargeCommands.cs already has Rate Card validation")

# 4. EconomicChargeEndpoints.cs & EconomicChargeCommands.cs
eco_cmd_file = 'src/LCMS.Application/EconomicCharges/EconomicChargeCommands.cs'
with open(eco_cmd_file, 'r', encoding='utf-8') as f:
    text = f.read()

eco_append = '''

public sealed record EconomicChargeAccountingMappingDto(
    Guid EconomicChargeTypeId,
    string Code,
    string Name,
    string? CostTypeCode,
    string? RevenueTypeCode,
    string NoticeMessage
);

public sealed record GetEconomicChargeAccountingMappingQuery(Guid Id) : IRequest<EconomicChargeAccountingMappingDto>;

public sealed class GetEconomicChargeAccountingMappingQueryHandler : IRequestHandler<GetEconomicChargeAccountingMappingQuery, EconomicChargeAccountingMappingDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;

    public GetEconomicChargeAccountingMappingQueryHandler(ILcmsDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<EconomicChargeAccountingMappingDto> Handle(GetEconomicChargeAccountingMappingQuery request, CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var chargeType = await _db.EconomicChargeTypes.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy loại phí kinh tế.");

        var mappings = await _db.ChargeTypeMappings.AsNoTracking()
            .Where(m => m.EconomicChargeTypeId == request.Id)
            .ToListAsync(cancellationToken);

        var costType = mappings.FirstOrDefault(m => string.Equals(m.SourceKind, ChargeTypeMappingKinds.CostType, StringComparison.OrdinalIgnoreCase))?.SourceCode;
        var revType = mappings.FirstOrDefault(m => string.Equals(m.SourceKind, ChargeTypeMappingKinds.RevenueType, StringComparison.OrdinalIgnoreCase))?.SourceCode;
        var comp = mappings.FirstOrDefault(m => string.Equals(m.SourceKind, ChargeTypeMappingKinds.Component, StringComparison.OrdinalIgnoreCase))?.SourceCode;
        costType ??= comp;
        revType ??= comp;

        return new EconomicChargeAccountingMappingDto(
            chargeType.Id,
            chargeType.Code,
            chargeType.Name,
            costType,
            revType,
            $"Tự động ánh xạ: BUY -> {costType ?? "chưa ánh xạ"}, SELL -> {revType ?? "chưa ánh xạ"}."
        );
    }
}
'''

if "GetEconomicChargeAccountingMappingQuery" not in text:
    with open(eco_cmd_file, 'w', encoding='utf-8', newline='\r\n') as f:
        f.write(text + eco_append)
    print("Updated EconomicChargeCommands.cs with AccountingMapping query")
else:
    print("EconomicChargeCommands.cs already has AccountingMapping query")

eco_ep_file = 'src/LCMS.Api/Endpoints/EconomicChargeEndpoints.cs'
with open(eco_ep_file, 'r', encoding='utf-8') as f:
    text = f.read()

eco_target = """        group.MapPost("/mappings", async (MapChargeTypeRequest body, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(new MapChargeTypeCommand(body.SourceKind, body.SourceCode, body.EconomicChargeTypeId), ct);
            return Results.Created($"/api/economic-charge-types/mappings/{id}", new { id });
        });"""

eco_repl = """        group.MapGet("/{id:guid}/accounting-mapping", async (Guid id, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetEconomicChargeAccountingMappingQuery(id), ct)));
        group.MapPost("/mappings", async (MapChargeTypeRequest body, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(new MapChargeTypeCommand(body.SourceKind, body.SourceCode, body.EconomicChargeTypeId), ct);
            return Results.Created($"/api/economic-charge-types/mappings/{id}", new { id });
        });"""

if "accounting-mapping" not in text:
    text = text.replace(eco_target, eco_repl, 1)
    with open(eco_ep_file, 'w', encoding='utf-8', newline='\r\n') as f:
        f.write(text)
    print("Updated EconomicChargeEndpoints.cs with accounting-mapping endpoint")
else:
    print("EconomicChargeEndpoints.cs already has accounting-mapping endpoint")

# 5. CostEndpoints.cs
cost_file = 'src/LCMS.Api/Endpoints/CostEndpoints.cs'
with open(cost_file, 'r', encoding='utf-8') as f:
    text = f.read()

cost_target = """        var allocations = app.MapGroup("/api/cost-allocations").WithTags("CostAllocations");"""
cost_repl = """        costs.MapGet("/shared/allocation-context", async (
            Guid? shipmentId,
            Guid? transportLegId,
            Guid? transportMovementId,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetSharedCostAllocationContextQuery(shipmentId, transportLegId, transportMovementId), ct);
            return Results.Ok(result);
        });

        var allocations = app.MapGroup("/api/cost-allocations").WithTags("CostAllocations");
        allocations.MapGet("/context", async (
            Guid? shipmentId,
            Guid? transportLegId,
            Guid? transportMovementId,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetSharedCostAllocationContextQuery(shipmentId, transportLegId, transportMovementId), ct);
            return Results.Ok(result);
        });"""

if "/shared/allocation-context" not in text:
    text = text.replace(cost_target, cost_repl, 1)
    with open(cost_file, 'w', encoding='utf-8', newline='\r\n') as f:
        f.write(text)
    print("Updated CostEndpoints.cs with shared allocation-context endpoint")
else:
    print("CostEndpoints.cs already has shared allocation-context endpoint")

