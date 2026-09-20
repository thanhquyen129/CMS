using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Common.Paging;
using LCMS.Application.BusinessParties;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.BusinessParties.Queries;

public sealed record BusinessPartyLookupItemDto(
    Guid Id,
    string Code,
    string Name,
    string? ShortName,
    string? LegalName,
    string? TaxId,
    string? Phone,
    string? Email,
    bool IsActive,
    bool IsBlocked,
    string StatusCode,
    IReadOnlyList<string> RoleCodes,
    string? DefaultCurrencyCode,
    int? PaymentTermDays,
    string? CreditStatus,
    string? CreditMessage);

public sealed record LookupBusinessPartiesQuery(
    string? Q,
    string? RoleCode = null,
    bool UsableOnly = true,
    int Take = 20) : IRequest<IReadOnlyList<BusinessPartyLookupItemDto>>;

public sealed class LookupBusinessPartiesQueryHandler
    : IRequestHandler<LookupBusinessPartiesQuery, IReadOnlyList<BusinessPartyLookupItemDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPartyDirectoryService _directory;

    public LookupBusinessPartiesQueryHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPartyDirectoryService directory)
    {
        _db = db;
        _tenantContext = tenantContext;
        _directory = directory;
    }

    public async Task<IReadOnlyList<BusinessPartyLookupItemDto>> Handle(
        LookupBusinessPartiesQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var take = Math.Clamp(request.Take, 1, 50);
        var query = _db.BusinessParties.AsNoTracking().AsQueryable();
        if (request.UsableOnly)
        {
            query = query.Where(p => p.IsActive && !p.IsBlocked);
        }

        query = PartySearch.Apply(query, request.Q);

        if (!string.IsNullOrWhiteSpace(request.RoleCode))
        {
            var role = request.RoleCode.Trim().ToLowerInvariant();
            var partyIds = _db.PartyRoles.AsNoTracking()
                .Where(r => r.RoleCode == role && r.IsActive)
                .Select(r => r.PartyId);
            query = query.Where(p => partyIds.Contains(p.Id));
        }

        var parties = await query
            .OrderBy(p => p.Code)
            .Take(take)
            .ToListAsync(cancellationToken);

        return await MapLookupAsync(parties, cancellationToken);
    }

    private async Task<IReadOnlyList<BusinessPartyLookupItemDto>> MapLookupAsync(
        List<Domain.Entities.BusinessParty> parties,
        CancellationToken cancellationToken)
    {
        if (parties.Count == 0)
        {
            return [];
        }

        var ids = parties.Select(p => p.Id).ToList();
        var roles = await _db.PartyRoles.AsNoTracking()
            .Where(r => ids.Contains(r.PartyId) && r.IsActive)
            .Select(r => new { r.PartyId, r.RoleCode })
            .ToListAsync(cancellationToken);
        var rolesByParty = roles
            .GroupBy(r => r.PartyId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.RoleCode).OrderBy(c => c).ToList());

        var items = new List<BusinessPartyLookupItemDto>(parties.Count);
        foreach (var p in parties)
        {
            var credit = await _directory.EvaluateCreditAsync(p.Id, 0m, p.CreditLimitCurrencyCode, cancellationToken);
            items.Add(new BusinessPartyLookupItemDto(
                p.Id,
                p.Code,
                p.Name,
                p.ShortName,
                p.LegalName,
                p.TaxId,
                p.Phone,
                p.Email,
                p.IsActive,
                p.IsBlocked,
                PartyStatusCodes.FromFlags(p.IsActive, p.IsBlocked),
                rolesByParty.TryGetValue(p.Id, out var rc) ? rc : Array.Empty<string>(),
                p.DefaultCurrencyCode,
                p.PaymentTermDays,
                credit.Status,
                credit.Message));
        }

        return items;
    }
}

public sealed record BusinessPartyDuplicateHitDto(
    Guid Id,
    string Code,
    string Name,
    string? TaxId,
    string? Phone,
    string? Email,
    string MatchOn);

public sealed record GetBusinessPartyDuplicatesQuery(
    string? TaxId,
    string? Phone,
    string? Email,
    Guid? ExcludeId = null) : IRequest<IReadOnlyList<BusinessPartyDuplicateHitDto>>;

public sealed class GetBusinessPartyDuplicatesQueryHandler
    : IRequestHandler<GetBusinessPartyDuplicatesQuery, IReadOnlyList<BusinessPartyDuplicateHitDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public GetBusinessPartyDuplicatesQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<BusinessPartyDuplicateHitDto>> Handle(
        GetBusinessPartyDuplicatesQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var hits = new List<BusinessPartyDuplicateHitDto>();
        var taxId = PartySearch.NormalizeKey(request.TaxId);
        var phone = PartySearch.NormalizeKey(request.Phone);
        var email = PartySearch.NormalizeKey(request.Email)?.ToLowerInvariant();

        if (taxId is not null)
        {
            hits.AddRange(await _db.BusinessParties.AsNoTracking()
                .Where(p => p.TaxId == taxId && (request.ExcludeId == null || p.Id != request.ExcludeId))
                .Select(p => new BusinessPartyDuplicateHitDto(p.Id, p.Code, p.Name, p.TaxId, p.Phone, p.Email, "taxId"))
                .ToListAsync(cancellationToken));
        }

        if (phone is not null)
        {
            hits.AddRange(await _db.BusinessParties.AsNoTracking()
                .Where(p => p.Phone != null && p.Phone.ToLower() == phone && (request.ExcludeId == null || p.Id != request.ExcludeId))
                .Select(p => new BusinessPartyDuplicateHitDto(p.Id, p.Code, p.Name, p.TaxId, p.Phone, p.Email, "phone"))
                .ToListAsync(cancellationToken));
        }

        if (email is not null)
        {
            hits.AddRange(await _db.BusinessParties.AsNoTracking()
                .Where(p => p.Email != null && p.Email.ToLower() == email && (request.ExcludeId == null || p.Id != request.ExcludeId))
                .Select(p => new BusinessPartyDuplicateHitDto(p.Id, p.Code, p.Name, p.TaxId, p.Phone, p.Email, "email"))
                .ToListAsync(cancellationToken));
        }

        return hits
            .GroupBy(h => (h.Id, h.MatchOn))
            .Select(g => g.First())
            .OrderBy(h => h.Code)
            .ToList();
    }
}

public sealed record BusinessPartyDirectorySummaryDto(
    int Total,
    int Active,
    int Inactive,
    int Blocked);

public sealed record GetBusinessPartyDirectorySummaryQuery(
    string? Search = null,
    string? RoleCode = null,
    string? Status = null,
    string? Kind = null,
    string? GroupCode = null) : IRequest<BusinessPartyDirectorySummaryDto>;

public sealed class GetBusinessPartyDirectorySummaryQueryHandler
    : IRequestHandler<GetBusinessPartyDirectorySummaryQuery, BusinessPartyDirectorySummaryDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public GetBusinessPartyDirectorySummaryQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<BusinessPartyDirectorySummaryDto> Handle(
        GetBusinessPartyDirectorySummaryQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var query = PartySearch.ApplyDirectory(
            _db.BusinessParties.AsNoTracking(),
            _db,
            request.Search,
            request.RoleCode,
            status: null,
            request.Kind,
            request.GroupCode);

        var rows = await query
            .Select(p => new { p.IsActive, p.IsBlocked })
            .ToListAsync(cancellationToken);

        return new BusinessPartyDirectorySummaryDto(
            rows.Count,
            rows.Count(r => r.IsActive && !r.IsBlocked),
            rows.Count(r => !r.IsActive && !r.IsBlocked),
            rows.Count(r => r.IsBlocked));
    }
}

public sealed record BusinessPartyDirectoryItemDto(
    Guid Id,
    string Code,
    string Name,
    string? ShortName,
    string? LegalName,
    string? TaxId,
    string? Phone,
    string? Email,
    string PartyKind,
    string? GroupCode,
    bool IsActive,
    bool IsBlocked,
    string StatusCode,
    string? DefaultCurrencyCode,
    int? PaymentTermDays,
    decimal? CreditLimit,
    string? CreditLimitCurrencyCode,
    string CreditControlMode,
    IReadOnlyList<string> RoleCodes,
    decimal? ApOutstanding,
    decimal? ArOutstanding,
    string? CreditStatus,
    DateTimeOffset CreatedAt);

public sealed record ListBusinessPartyDirectoryQuery(
    string? Search = null,
    string? RoleCode = null,
    string? Status = null,
    string? Kind = null,
    string? GroupCode = null,
    int? Page = null,
    int? PageSize = null) : IRequest<PagedResult<BusinessPartyDirectoryItemDto>>;

public sealed class ListBusinessPartyDirectoryQueryHandler
    : IRequestHandler<ListBusinessPartyDirectoryQuery, PagedResult<BusinessPartyDirectoryItemDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;

    public ListBusinessPartyDirectoryQueryHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
    }

    public async Task<PagedResult<BusinessPartyDirectoryItemDto>> Handle(
        ListBusinessPartyDirectoryQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var (page, pageSize, applyPaging) = PagingNormalize.Normalize(request.Page ?? 1, request.PageSize ?? 20);
        var query = PartySearch.ApplyDirectory(
            _db.BusinessParties.AsNoTracking(),
            _db,
            request.Search,
            request.RoleCode,
            request.Status,
            request.Kind,
            request.GroupCode);

        var total = await query.CountAsync(cancellationToken);
        var parties = await query
            .OrderBy(p => p.Code)
            .Skip((page - 1) * pageSize)
            .Take(applyPaging ? pageSize : Math.Max(pageSize, 1))
            .ToListAsync(cancellationToken);

        if (parties.Count == 0)
        {
            return new PagedResult<BusinessPartyDirectoryItemDto>([], page, pageSize, total);
        }

        var ids = parties.Select(p => p.Id).ToList();
        var roles = await _db.PartyRoles.AsNoTracking()
            .Where(r => ids.Contains(r.PartyId) && r.IsActive)
            .Select(r => new { r.PartyId, r.RoleCode })
            .ToListAsync(cancellationToken);
        var rolesByParty = roles
            .GroupBy(r => r.PartyId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.RoleCode).OrderBy(c => c).ToList());

        var canCost = await _permissions.HasPermissionAsync(PermissionCodes.CostRead, cancellationToken);
        var canRevenue = await _permissions.HasPermissionAsync(PermissionCodes.RevenueRead, cancellationToken);

        Dictionary<Guid, decimal> apByParty = [];
        Dictionary<Guid, decimal> arByParty = [];
        if (canCost)
        {
            var apRows = await _db.AccountsPayable.AsNoTracking()
                .Where(a => a.CounterpartyId != null && ids.Contains(a.CounterpartyId.Value) && a.RecordStatus == ApArRecordStatuses.Active)
                .Select(a => new { a.CounterpartyId, a.RecognizedAmount, a.AdjustmentAmount, a.FinalizedSettledAmount })
                .ToListAsync(cancellationToken);
            apByParty = apRows
                .GroupBy(a => a.CounterpartyId!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => decimal.Round(
                        g.Sum(x => x.RecognizedAmount + x.AdjustmentAmount - x.FinalizedSettledAmount),
                        4,
                        MidpointRounding.AwayFromZero));
        }

        if (canRevenue)
        {
            var arRows = await _db.AccountsReceivable.AsNoTracking()
                .Where(a => a.CounterpartyId != null && ids.Contains(a.CounterpartyId.Value) && a.RecordStatus == ApArRecordStatuses.Active)
                .Select(a => new { a.CounterpartyId, a.RecognizedAmount, a.AdjustmentAmount, a.FinalizedSettledAmount })
                .ToListAsync(cancellationToken);
            arByParty = arRows
                .GroupBy(a => a.CounterpartyId!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => decimal.Round(
                        g.Sum(x => x.RecognizedAmount + x.AdjustmentAmount - x.FinalizedSettledAmount),
                        4,
                        MidpointRounding.AwayFromZero));
        }

        var items = parties.Select(p =>
        {
            decimal? ar = canRevenue && arByParty.TryGetValue(p.Id, out var arVal) ? arVal : null;
            var creditStatus = CreditStatus(p, ar);
            return new BusinessPartyDirectoryItemDto(
                p.Id,
                p.Code,
                p.Name,
                p.ShortName,
                p.LegalName,
                p.TaxId,
                p.Phone,
                p.Email,
                p.PartyKind,
                p.GroupCode,
                p.IsActive,
                p.IsBlocked,
                PartyStatusCodes.FromFlags(p.IsActive, p.IsBlocked),
                p.DefaultCurrencyCode,
                p.PaymentTermDays,
                p.CreditLimit,
                p.CreditLimitCurrencyCode,
                p.CreditControlMode,
                rolesByParty.TryGetValue(p.Id, out var rc) ? rc : Array.Empty<string>(),
                canCost && apByParty.TryGetValue(p.Id, out var apVal) ? apVal : canCost ? 0m : null,
                ar ?? (canRevenue ? 0m : null),
                creditStatus,
                p.CreatedAt);
        }).ToList();

        return new PagedResult<BusinessPartyDirectoryItemDto>(items, page, pageSize, total);
    }

    private static string? CreditStatus(Domain.Entities.BusinessParty p, decimal? arOutstanding)
    {
        if (p.IsBlocked)
        {
            return "blocked";
        }

        if (!p.IsActive)
        {
            return "inactive";
        }

        if (p.CreditLimit is null or <= 0 || arOutstanding is null)
        {
            return "none";
        }

        if (arOutstanding > p.CreditLimit)
        {
            return "over";
        }

        if (arOutstanding >= p.CreditLimit * PartyDirectoryService.WatchRatio)
        {
            return "watch";
        }

        return "ok";
    }
}

internal static class PartySearch
{
    public static string? NormalizeKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    public static IQueryable<Domain.Entities.BusinessParty> Apply(
        IQueryable<Domain.Entities.BusinessParty> query,
        string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return query;
        }

        var q = search.Trim().ToLowerInvariant();
        return query.Where(p =>
            p.Code.ToLower().Contains(q)
            || p.Name.ToLower().Contains(q)
            || (p.ShortName != null && p.ShortName.ToLower().Contains(q))
            || (p.LegalName != null && p.LegalName.ToLower().Contains(q))
            || (p.TaxId != null && p.TaxId.ToLower().Contains(q))
            || (p.Phone != null && p.Phone.ToLower().Contains(q))
            || (p.Email != null && p.Email.ToLower().Contains(q))
            || (p.ExternalCode != null && p.ExternalCode.ToLower().Contains(q)));
    }

    public static IQueryable<Domain.Entities.BusinessParty> ApplyDirectory(
        IQueryable<Domain.Entities.BusinessParty> query,
        ILcmsDbContext db,
        string? search,
        string? roleCode,
        string? status,
        string? kind,
        string? groupCode)
    {
        query = Apply(query, search);

        if (!string.IsNullOrWhiteSpace(roleCode))
        {
            var role = roleCode.Trim().ToLowerInvariant();
            var partyIds = db.PartyRoles.AsNoTracking()
                .Where(r => r.RoleCode == role && r.IsActive)
                .Select(r => r.PartyId);
            query = query.Where(p => partyIds.Contains(p.Id));
        }

        if (!string.IsNullOrWhiteSpace(kind))
        {
            var k = kind.Trim().ToLowerInvariant();
            query = query.Where(p => p.PartyKind == k);
        }

        if (!string.IsNullOrWhiteSpace(groupCode))
        {
            var g = groupCode.Trim();
            query = query.Where(p => p.GroupCode == g);
        }

        var st = status?.Trim().ToLowerInvariant();
        query = st switch
        {
            PartyStatusCodes.Active => query.Where(p => p.IsActive && !p.IsBlocked),
            PartyStatusCodes.Inactive => query.Where(p => !p.IsActive && !p.IsBlocked),
            PartyStatusCodes.Blocked => query.Where(p => p.IsBlocked),
            _ => query
        };

        return query;
    }
}
