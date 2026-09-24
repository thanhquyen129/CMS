using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Search.Queries;

public sealed record GlobalSearchHitDto(
    string EntityType,
    Guid Id,
    string Code,
    string Title,
    string MatchKind);

public sealed record SearchGlobalQuery(string Q) : IRequest<IReadOnlyList<GlobalSearchHitDto>>;

public sealed class SearchGlobalQueryValidator : AbstractValidator<SearchGlobalQuery>
{
    public SearchGlobalQueryValidator()
    {
        RuleFor(x => x.Q)
            .NotEmpty().WithMessage("Từ khóa tìm kiếm không được để trống.")
            .MaximumLength(128).WithMessage("Từ khóa tìm kiếm không được vượt quá 128 ký tự.");
    }
}

/// <summary>
/// Multi-entity search for the top-bar (Bill, operational refs, cost/revenue, document, party,
/// payment/collection, rate card). Each type is gated independently so Cost ≠ Revenue
/// and buy-rate ≠ sell-rate stay intact.
/// </summary>
public sealed class SearchGlobalQueryHandler
    : IRequestHandler<SearchGlobalQuery, IReadOnlyList<GlobalSearchHitDto>>
{
    private const int PerTypeLimit = 8;

    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;

    public SearchGlobalQueryHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
    }

    public async Task<IReadOnlyList<GlobalSearchHitDto>> Handle(
        SearchGlobalQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var pattern = request.Q.Trim().ToLowerInvariant();
        var hits = new List<GlobalSearchHitDto>();

        if (await _permissions.HasPermissionAsync(PermissionCodes.BillRead, cancellationToken))
        {
            var bills = await _db.Bills.AsNoTracking()
                .Where(b =>
                    b.BillNo.ToLower().Contains(pattern)
                    || (b.ExternalId != null && b.ExternalId.ToLower().Contains(pattern)))
                .OrderBy(b => b.BillNo)
                .Take(PerTypeLimit)
                .Select(b => new GlobalSearchHitDto(
                    "bill",
                    b.Id,
                    b.BillNo,
                    b.BillType,
                    b.BillNo.ToLower().Contains(pattern) ? "bill_no" : "bill_external_id"))
                .ToListAsync(cancellationToken);
            hits.AddRange(bills);

            var orders = await _db.Orders.AsNoTracking()
                .Where(o => o.OrderNo.ToLower().Contains(pattern) || o.ExternalId.ToLower().Contains(pattern))
                .OrderBy(o => o.OrderNo)
                .Take(PerTypeLimit)
                .Select(o => new GlobalSearchHitDto("order", o.Id, o.OrderNo, o.SourceSystem, "order_no"))
                .ToListAsync(cancellationToken);
            hits.AddRange(orders);

            var shipments = await _db.Shipments.AsNoTracking()
                .Where(s => s.ShipmentNo.ToLower().Contains(pattern) || s.ExternalId.ToLower().Contains(pattern))
                .OrderBy(s => s.ShipmentNo)
                .Take(PerTypeLimit)
                .Select(s => new GlobalSearchHitDto("shipment", s.Id, s.ShipmentNo, s.SourceSystem, "shipment_no"))
                .ToListAsync(cancellationToken);
            hits.AddRange(shipments);

            var legs = await _db.TransportLegs.AsNoTracking()
                .Where(l => l.LegNo.ToLower().Contains(pattern) || l.ExternalId.ToLower().Contains(pattern))
                .OrderBy(l => l.LegNo)
                .Take(PerTypeLimit)
                .Select(l => new GlobalSearchHitDto("leg", l.Id, l.LegNo, l.SourceSystem, "leg_no"))
                .ToListAsync(cancellationToken);
            hits.AddRange(legs);

            var movements = await _db.TransportMovements.AsNoTracking()
                .Where(m => m.MovementNo.ToLower().Contains(pattern) || m.ExternalId.ToLower().Contains(pattern))
                .OrderBy(m => m.MovementNo)
                .Take(PerTypeLimit)
                .Select(m => new GlobalSearchHitDto("movement", m.Id, m.MovementNo, m.SourceSystem, "movement_no"))
                .ToListAsync(cancellationToken);
            hits.AddRange(movements);

            var docs = await _db.FinancialDocuments.AsNoTracking()
                .Where(d => d.DocumentNo.ToLower().Contains(pattern))
                .OrderBy(d => d.DocumentNo)
                .Take(PerTypeLimit)
                .Select(d => new GlobalSearchHitDto("document", d.Id, d.DocumentNo, d.DocumentType, "document_no"))
                .ToListAsync(cancellationToken);
            hits.AddRange(docs);

            var parties = await _db.BusinessParties.AsNoTracking()
                .Where(p =>
                    p.Code.ToLower().Contains(pattern)
                    || p.Name.ToLower().Contains(pattern)
                    || (p.TaxId != null && p.TaxId.ToLower().Contains(pattern)))
                .OrderBy(p => p.Code)
                .Take(PerTypeLimit)
                .Select(p => new GlobalSearchHitDto("party", p.Id, p.Code, p.Name, "party"))
                .ToListAsync(cancellationToken);
            hits.AddRange(parties);
        }

        if (await _permissions.HasPermissionAsync(PermissionCodes.CostRead, cancellationToken))
        {
            var costsRaw = await _db.Costs.AsNoTracking()
                .Where(c => c.CostTypeCode != null && c.CostTypeCode.ToLower().Contains(pattern))
                .Select(c => new { c.Id, c.CostTypeCode, c.FinancialMaturity, c.CreatedAt })
                .ToListAsync(cancellationToken);
            var costs = costsRaw
                .OrderByDescending(c => c.CreatedAt)
                .Take(PerTypeLimit)
                .Select(c => new GlobalSearchHitDto(
                    "cost",
                    c.Id,
                    c.CostTypeCode ?? c.Id.ToString(),
                    c.FinancialMaturity,
                    "cost_type"))
                .ToList();
            hits.AddRange(costs);

            var payments = await _db.Payments.AsNoTracking()
                .Where(p => p.ReferenceNo != null && p.ReferenceNo.ToLower().Contains(pattern))
                .OrderBy(p => p.ReferenceNo)
                .Take(PerTypeLimit)
                .Select(p => new GlobalSearchHitDto(
                    "payment",
                    p.Id,
                    p.ReferenceNo!,
                    p.Status,
                    "payment_reference"))
                .ToListAsync(cancellationToken);
            hits.AddRange(payments);
        }

        if (await _permissions.HasPermissionAsync(PermissionCodes.RevenueRead, cancellationToken))
        {
            var revenuesRaw = await _db.Revenues.AsNoTracking()
                .Where(r => r.RevenueTypeCode != null && r.RevenueTypeCode.ToLower().Contains(pattern))
                .Select(r => new { r.Id, r.RevenueTypeCode, r.FinancialMaturity, r.CreatedAt })
                .ToListAsync(cancellationToken);
            var revenues = revenuesRaw
                .OrderByDescending(r => r.CreatedAt)
                .Take(PerTypeLimit)
                .Select(r => new GlobalSearchHitDto(
                    "revenue",
                    r.Id,
                    r.RevenueTypeCode ?? r.Id.ToString(),
                    r.FinancialMaturity,
                    "revenue_type"))
                .ToList();
            hits.AddRange(revenues);

            var collections = await _db.Collections.AsNoTracking()
                .Where(c => c.ReferenceNo != null && c.ReferenceNo.ToLower().Contains(pattern))
                .OrderBy(c => c.ReferenceNo)
                .Take(PerTypeLimit)
                .Select(c => new GlobalSearchHitDto(
                    "collection",
                    c.Id,
                    c.ReferenceNo!,
                    c.Status,
                    "collection_reference"))
                .ToListAsync(cancellationToken);
            hits.AddRange(collections);
        }

        var canBuyRate = await _permissions.HasPermissionAsync(PermissionCodes.RateBuyRead, cancellationToken);
        var canSellRate = await _permissions.HasPermissionAsync(PermissionCodes.RateSellRead, cancellationToken);
        if (canBuyRate || canSellRate)
        {
            var cardsQuery = _db.RateCards.AsNoTracking()
                .Where(r =>
                    r.Code.ToLower().Contains(pattern)
                    || r.Name.ToLower().Contains(pattern)
                    || (r.CarrierName != null && r.CarrierName.ToLower().Contains(pattern))
                    || (r.RouteCode != null && r.RouteCode.ToLower().Contains(pattern)));
            if (canBuyRate && !canSellRate)
            {
                cardsQuery = cardsQuery.Where(r => r.PartyType == "vendor");
            }
            else if (canSellRate && !canBuyRate)
            {
                cardsQuery = cardsQuery.Where(r => r.PartyType == "customer");
            }

            var cards = await cardsQuery
                .OrderBy(r => r.Code)
                .Take(PerTypeLimit)
                .Select(r => new GlobalSearchHitDto("rate_card", r.Id, r.Code, r.Name, "rate_card"))
                .ToListAsync(cancellationToken);
            hits.AddRange(cards);
        }

        return hits;
    }
}
