using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.BusinessParties;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.BusinessParties.Queries;

public sealed record PartyMoneyBucketDto(string CurrencyCode, decimal Outstanding, int OpenCount);

public sealed record PartyRecentBillDto(Guid Id, string BillNo, DateTimeOffset CreatedAt, string? OperationalStatus);

public sealed record PartyRecentDocumentDto(
    Guid Id,
    string DocumentNo,
    string DocumentType,
    string Direction,
    decimal TotalAmount,
    string CurrencyCode,
    DateTimeOffset? ReceivedAt);

public sealed record BusinessPartyFinancialDto(
    Guid PartyId,
    string Code,
    string Name,
    bool CanViewAp,
    bool CanViewAr,
    bool CanViewBills,
    IReadOnlyList<PartyMoneyBucketDto>? ApByCurrency,
    IReadOnlyList<PartyMoneyBucketDto>? ArByCurrency,
    decimal? ApOutstandingTotal,
    decimal? ArOutstandingTotal,
    int? ApOverdueCount,
    int? ArOverdueCount,
    PartyCreditEvaluation Credit,
    int BillCount,
    int CostCount,
    int RevenueCount,
    int DocumentCount,
    IReadOnlyList<PartyRecentBillDto> RecentBills,
    IReadOnlyList<PartyRecentDocumentDto> RecentDocuments);

public sealed record GetBusinessPartyFinancialQuery(Guid PartyId) : IRequest<BusinessPartyFinancialDto>;

public sealed class GetBusinessPartyFinancialQueryHandler
    : IRequestHandler<GetBusinessPartyFinancialQuery, BusinessPartyFinancialDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;
    private readonly IPartyDirectoryService _directory;

    public GetBusinessPartyFinancialQueryHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions,
        IPartyDirectoryService directory)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
        _directory = directory;
    }

    public async Task<BusinessPartyFinancialDto> Handle(
        GetBusinessPartyFinancialQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var party = await _db.BusinessParties.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.PartyId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy đối tác.");

        var canAp = await _permissions.HasPermissionAsync(PermissionCodes.CostRead, cancellationToken);
        var canAr = await _permissions.HasPermissionAsync(PermissionCodes.RevenueRead, cancellationToken);
        var canBills = await _permissions.HasPermissionAsync(PermissionCodes.BillRead, cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        IReadOnlyList<PartyMoneyBucketDto>? apBuckets = null;
        IReadOnlyList<PartyMoneyBucketDto>? arBuckets = null;
        decimal? apTotal = null;
        decimal? arTotal = null;
        int? apOverdue = null;
        int? arOverdue = null;

        if (canAp)
        {
            var apRows = await _db.AccountsPayable.AsNoTracking()
                .Where(a => a.CounterpartyId == party.Id && a.RecordStatus == ApArRecordStatuses.Active)
                .Select(a => new { a.CurrencyCode, a.RecognizedAmount, a.AdjustmentAmount, a.FinalizedSettledAmount, a.DueDate })
                .ToListAsync(cancellationToken);
            var open = apRows
                .Select(a => new
                {
                    a.CurrencyCode,
                    Outstanding = a.RecognizedAmount + a.AdjustmentAmount - a.FinalizedSettledAmount,
                    a.DueDate
                })
                .Where(a => a.Outstanding > 0)
                .ToList();
            apBuckets = open
                .GroupBy(a => a.CurrencyCode)
                .Select(g => new PartyMoneyBucketDto(
                    g.Key,
                    decimal.Round(g.Sum(x => x.Outstanding), 4, MidpointRounding.AwayFromZero),
                    g.Count()))
                .OrderBy(b => b.CurrencyCode)
                .ToList();
            apTotal = decimal.Round(open.Sum(a => a.Outstanding), 4, MidpointRounding.AwayFromZero);
            apOverdue = open.Count(a => a.DueDate is DateOnly d && d < today);
        }

        if (canAr)
        {
            var arRows = await _db.AccountsReceivable.AsNoTracking()
                .Where(a => a.CounterpartyId == party.Id && a.RecordStatus == ApArRecordStatuses.Active)
                .Select(a => new { a.CurrencyCode, a.RecognizedAmount, a.AdjustmentAmount, a.FinalizedSettledAmount, a.DueDate })
                .ToListAsync(cancellationToken);
            var open = arRows
                .Select(a => new
                {
                    a.CurrencyCode,
                    Outstanding = a.RecognizedAmount + a.AdjustmentAmount - a.FinalizedSettledAmount,
                    a.DueDate
                })
                .Where(a => a.Outstanding > 0)
                .ToList();
            arBuckets = open
                .GroupBy(a => a.CurrencyCode)
                .Select(g => new PartyMoneyBucketDto(
                    g.Key,
                    decimal.Round(g.Sum(x => x.Outstanding), 4, MidpointRounding.AwayFromZero),
                    g.Count()))
                .OrderBy(b => b.CurrencyCode)
                .ToList();
            arTotal = decimal.Round(open.Sum(a => a.Outstanding), 4, MidpointRounding.AwayFromZero);
            arOverdue = open.Count(a => a.DueDate is DateOnly d && d < today);
        }

        var credit = await _directory.EvaluateCreditAsync(
            party.Id,
            0m,
            party.CreditLimitCurrencyCode,
            cancellationToken);

        var billCount = canBills
            ? await _db.Bills.AsNoTracking().CountAsync(b => b.CustomerPartyId == party.Id, cancellationToken)
            : 0;
        var costCount = canAp
            ? await _db.Costs.AsNoTracking().CountAsync(c => c.VendorPartyId == party.Id, cancellationToken)
            : 0;
        var revenueCount = canAr
            ? await _db.Revenues.AsNoTracking().CountAsync(r => r.CustomerPartyId == party.Id, cancellationToken)
            : 0;
        var documentCount = await _db.FinancialDocuments.AsNoTracking()
            .CountAsync(d => d.CounterpartyId == party.Id, cancellationToken);

        // SQLite tests cannot ORDER BY DateTimeOffset; take then sort in memory (Postgres still fine).
        var recentBillRows = canBills
            ? await _db.Bills.AsNoTracking()
                .Where(b => b.CustomerPartyId == party.Id)
                .Select(b => new { b.Id, b.BillNo, b.CreatedAt, b.OperationalStatus })
                .ToListAsync(cancellationToken)
            : [];
        var recentBills = recentBillRows
            .OrderByDescending(b => b.CreatedAt)
            .Take(8)
            .Select(b => new PartyRecentBillDto(b.Id, b.BillNo, b.CreatedAt, b.OperationalStatus))
            .ToList();

        var recentDocRows = await _db.FinancialDocuments.AsNoTracking()
            .Where(d => d.CounterpartyId == party.Id)
            .Select(d => new
            {
                d.Id,
                d.DocumentNo,
                d.DocumentType,
                d.Direction,
                d.TotalAmount,
                d.CurrencyCode,
                d.ReceivedAt,
                d.CreatedAt
            })
            .ToListAsync(cancellationToken);
        var recentDocs = recentDocRows
            .OrderByDescending(d => d.ReceivedAt ?? d.CreatedAt)
            .Take(8)
            .Select(d => new PartyRecentDocumentDto(
                d.Id,
                d.DocumentNo,
                d.DocumentType,
                d.Direction,
                d.TotalAmount,
                d.CurrencyCode,
                d.ReceivedAt))
            .ToList();

        return new BusinessPartyFinancialDto(
            party.Id,
            party.Code,
            party.Name,
            canAp,
            canAr,
            canBills,
            apBuckets,
            arBuckets,
            apTotal,
            arTotal,
            apOverdue,
            arOverdue,
            credit,
            billCount,
            costCount,
            revenueCount,
            documentCount,
            recentBills,
            recentDocs);
    }
}
