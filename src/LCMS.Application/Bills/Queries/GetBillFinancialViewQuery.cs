using LCMS.Application.Abstractions;
using LCMS.Application.Bills.Waybills;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Costs.Queries;
using LCMS.Application.FinancialDocuments.Queries;
using LCMS.Application.Revenues.Queries;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Bills.Queries;

public sealed record BillProgressStepDto(
    string Id,
    string LabelVi,
    string State);

public sealed record BillFinancialViewDto(
    BillDto Bill,
    BillFinancialProfileDto Profile,
    BillGraphDto Graph,
    IReadOnlyList<BillProgressStepDto> Progress,
    IReadOnlyList<CostListItemDto> Costs,
    IReadOnlyList<RevenueListItemDto> Revenues,
    IReadOnlyList<FinancialDocumentListItemDto> Documents,
    int CostCount,
    int RevenueCount,
    int DocumentCount,
    BillWaybillDto? Waybill = null);

public sealed record GetBillFinancialViewQuery(Guid BillId) : IRequest<BillFinancialViewDto>;

public sealed class GetBillFinancialViewQueryHandler
    : IRequestHandler<GetBillFinancialViewQuery, BillFinancialViewDto>
{
    private readonly ISender _sender;
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;

    public GetBillFinancialViewQueryHandler(
        ISender sender,
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions)
    {
        _sender = sender;
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
    }

    public async Task<BillFinancialViewDto> Handle(
        GetBillFinancialViewQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var bill = await _sender.Send(new GetBillByIdQuery(request.BillId), cancellationToken);
        var profile = await _sender.Send(new GetBillFinancialProfileQuery(request.BillId), cancellationToken);
        var graph = await _sender.Send(new GetBillGraphQuery(request.BillId), cancellationToken);

        // Scope already enforced on GetBillById; still gate money tabs by permission.
        IReadOnlyList<CostListItemDto> costs = [];
        IReadOnlyList<RevenueListItemDto> revenues = [];
        if (await _permissions.HasPermissionAsync(PermissionCodes.CostRead, cancellationToken))
        {
            costs = await _db.Costs.AsNoTracking()
                .Where(c => c.BillId == request.BillId && c.RecordStatus == "active")
                .OrderByDescending(c => c.EffectiveDate)
                .ThenBy(c => c.Id)
                .Select(c => new CostListItemDto(
                    c.Id,
                    c.BillId,
                    c.AttributionType,
                    c.FinancialMaturity,
                    c.Amount,
                    c.CurrencyCode,
                    c.CostTypeCode,
                    c.RecordStatus,
                    c.OrganizationId,
                    c.CreatedBy,
                    c.EffectiveDate,
                    c.VendorPartyId,
                    c.ExpectedAmount,
                    c.ConfirmedAmount,
                    c.ActualAmount,
                    c.RowVersion))
                .ToListAsync(cancellationToken);
        }

        if (await _permissions.HasPermissionAsync(PermissionCodes.RevenueRead, cancellationToken))
        {
            revenues = await _db.Revenues.AsNoTracking()
                .Where(r => r.BillId == request.BillId && r.RecordStatus == "active")
                .OrderByDescending(r => r.EffectiveDate)
                .ThenBy(r => r.Id)
                .Select(r => new RevenueListItemDto(
                    r.Id,
                    r.BillId,
                    r.FinancialMaturity,
                    r.Amount,
                    r.CurrencyCode,
                    r.RevenueTypeCode,
                    r.RecordStatus,
                    r.EffectiveDate,
                    r.RowVersion))
                .ToListAsync(cancellationToken);
        }

        var documents = await _db.FinancialDocuments.AsNoTracking()
            .Where(d => d.BillId == request.BillId)
            .OrderByDescending(d => d.DocumentDate)
            .ThenBy(d => d.Id)
            .Select(d => new FinancialDocumentListItemDto(
                d.Id,
                d.DocumentType,
                d.DocumentNo,
                d.Direction,
                d.TotalAmount,
                d.CurrencyCode,
                d.BillId,
                d.ReceiptStatus,
                d.AcceptanceStatus,
                d.MatchingStatus,
                d.DocumentDate))
            .ToListAsync(cancellationToken);

        // Enrich display names when stored context empty but derived available.
        var enrichedBill = bill;
        if (string.IsNullOrWhiteSpace(bill.CustomerName))
        {
            var partyId = await _db.Revenues.AsNoTracking()
                .Where(r => r.BillId == request.BillId && r.CustomerPartyId != null && r.RecordStatus == "active")
                .Select(r => r.CustomerPartyId)
                .FirstOrDefaultAsync(cancellationToken);
            if (partyId is Guid pid)
            {
                var name = await _db.BusinessParties.AsNoTracking()
                    .Where(p => p.Id == pid)
                    .Select(p => p.Name)
                    .FirstOrDefaultAsync(cancellationToken);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    enrichedBill = bill with { CustomerName = name, CustomerPartyId = bill.CustomerPartyId ?? pid };
                }
            }
        }

        if (string.IsNullOrWhiteSpace(enrichedBill.RouteCode))
        {
            // Order CreatedAt in-memory — SQLite rejects DateTimeOffset in SQL ORDER BY.
            var ratingRoutes = await _db.Ratings.AsNoTracking()
                .Where(r => r.BillId == request.BillId && r.RouteCode != null && r.RouteCode != "")
                .Select(r => new { r.RouteCode, r.CreatedAt })
                .ToListAsync(cancellationToken);
            var route = ratingRoutes
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => r.RouteCode)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(route))
            {
                enrichedBill = enrichedBill with { RouteCode = route };
            }
        }

        if (string.IsNullOrWhiteSpace(enrichedBill.AssignedUserName) && enrichedBill.CreatedBy is Guid createdBy)
        {
            var creator = await _db.Users.AsNoTracking()
                .Where(u => u.Id == createdBy)
                .Select(u => u.DisplayName)
                .FirstOrDefaultAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(creator))
            {
                enrichedBill = enrichedBill with
                {
                    AssignedUserId = enrichedBill.AssignedUserId ?? createdBy,
                    AssignedUserName = creator
                };
            }
        }

        var progress = BuildProgress(
            enrichedBill.OperationalStatus,
            graph.Shipments.Count + graph.Orders.Count,
            revenues.Any(r => string.Equals(r.FinancialMaturity, "confirmed", StringComparison.OrdinalIgnoreCase)
                              || string.Equals(r.FinancialMaturity, "actual", StringComparison.OrdinalIgnoreCase))
            || costs.Any(c => string.Equals(c.FinancialMaturity, "confirmed", StringComparison.OrdinalIgnoreCase)
                              || string.Equals(c.FinancialMaturity, "actual", StringComparison.OrdinalIgnoreCase)),
            costs.Count > 0,
            string.Equals(enrichedBill.OperationalStatus, "completed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(enrichedBill.OperationalStatus, "closed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(enrichedBill.OperationalStatus, "delivered", StringComparison.OrdinalIgnoreCase));

        BillWaybillDto? waybillDto = null;
        var waybill = await _db.BillWaybills.AsNoTracking()
            .FirstOrDefaultAsync(w => w.BillId == request.BillId, cancellationToken);
        if (waybill is not null)
        {
            var canSee = await BillWaybillMapper.CanSeeChargesAsync(
                _permissions,
                waybill.ChargeEconomicRole,
                cancellationToken);
            waybillDto = BillWaybillMapper.ToDto(waybill, enrichedBill.BillNo, canSee);
            if (string.IsNullOrWhiteSpace(enrichedBill.CustomerName)
                && !string.IsNullOrWhiteSpace(waybill.SenderName))
            {
                enrichedBill = enrichedBill with { CustomerName = waybill.SenderName };
            }
        }

        return new BillFinancialViewDto(
            enrichedBill,
            profile,
            graph,
            progress,
            costs,
            revenues,
            documents,
            costs.Count,
            revenues.Count,
            documents.Count,
            waybillDto);
    }

    private static IReadOnlyList<BillProgressStepDto> BuildProgress(
        string operationalStatus,
        int linkedOpsCount,
        bool hasConfirmedFinancials,
        bool hasCosts,
        bool isComplete)
    {
        // Mockup UI-02: Tạo Bill → Gán Shipment → Xác nhận thông tin → Ghi nhận chi phí → Hoàn tất
        var steps = new (string Id, string Label, bool Done)[]
        {
            ("create", "Tạo Bill", true),
            ("assign_shipment", "Gán Shipment", linkedOpsCount > 0),
            ("confirm_info", "Xác nhận thông tin", hasConfirmedFinancials
                || string.Equals(operationalStatus, "confirmed", StringComparison.OrdinalIgnoreCase)),
            ("record_costs", "Ghi nhận chi phí", hasCosts),
            ("complete", "Hoàn tất", isComplete)
        };

        var currentSet = false;
        var result = new List<BillProgressStepDto>(steps.Length);
        foreach (var (id, label, done) in steps)
        {
            string state;
            if (done)
            {
                state = "done";
            }
            else if (!currentSet)
            {
                state = "current";
                currentSet = true;
            }
            else
            {
                state = "pending";
            }

            result.Add(new BillProgressStepDto(id, label, state));
        }

        if (!currentSet && result.Count > 0)
        {
            var last = result[^1];
            result[^1] = last with { State = "current" };
        }

        return result;
    }
}
