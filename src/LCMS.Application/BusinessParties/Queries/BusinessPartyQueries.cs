using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.BusinessParties.Queries;

public sealed record BusinessPartyListItemDto(
    Guid Id,
    string Code,
    string Name,
    string? LegalName,
    string? TaxId,
    string? Phone,
    bool IsActive,
    bool IsBlocked,
    string StatusCode,
    string? DefaultCurrencyCode,
    int? PaymentTermDays,
    decimal? CreditLimit,
    string? CreditLimitCurrencyCode,
    IReadOnlyList<string> RoleCodes,
    DateTimeOffset CreatedAt);

public sealed record BusinessPartyDetailDto(
    Guid Id,
    string Code,
    string Name,
    string? LegalName,
    string? TaxId,
    string? Phone,
    string? Email,
    string? Website,
    string? AddressLine1,
    string? AddressLine2,
    string? Ward,
    string? District,
    string? City,
    string? Province,
    string? CountryCode,
    string? PostalCode,
    string? DefaultCurrencyCode,
    int? PaymentTermDays,
    decimal? CreditLimit,
    string? CreditLimitCurrencyCode,
    string? Notes,
    bool IsActive,
    bool IsBlocked,
    string StatusCode,
    string PartyKind,
    string? ShortName,
    string? LegalType,
    string? GroupCode,
    string? ExternalCode,
    string? IndustryCode,
    string? InvoiceEmail,
    bool? VatRegistered,
    Guid? AssignedUserId,
    Guid? ParentPartyId,
    string? ParentPartyCode,
    string? ParentPartyName,
    string CreditControlMode,
    string? BlockedReason,
    DateTimeOffset? BlockedAt,
    IReadOnlyList<string> RoleCodes,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>Backward-compatible alias used by older callers/tests.</summary>
public sealed record BusinessPartyDto(
    Guid Id,
    string Code,
    string Name,
    bool IsActive,
    DateTimeOffset CreatedAt);

public sealed record GetBusinessPartyByIdQuery(Guid Id) : IRequest<BusinessPartyDetailDto>;

public sealed class GetBusinessPartyByIdQueryHandler : IRequestHandler<GetBusinessPartyByIdQuery, BusinessPartyDetailDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public GetBusinessPartyByIdQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<BusinessPartyDetailDto> Handle(GetBusinessPartyByIdQuery request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var party = await _db.BusinessParties
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (party is null)
        {
            throw new NotFoundAppException("Không tìm thấy đối tác.");
        }

        var roles = await _db.PartyRoles.AsNoTracking()
            .Where(r => r.PartyId == party.Id && r.IsActive)
            .OrderBy(r => r.RoleCode)
            .Select(r => r.RoleCode)
            .ToListAsync(cancellationToken);

        string? parentCode = null;
        string? parentName = null;
        if (party.ParentPartyId is Guid parentId)
        {
            var parent = await _db.BusinessParties.AsNoTracking()
                .Where(p => p.Id == parentId)
                .Select(p => new { p.Code, p.Name })
                .FirstOrDefaultAsync(cancellationToken);
            parentCode = parent?.Code;
            parentName = parent?.Name;
        }

        return MapDetail(party, roles, parentCode, parentName);
    }

    internal static BusinessPartyDetailDto MapDetail(
        BusinessParty party,
        IReadOnlyList<string> roles,
        string? parentCode = null,
        string? parentName = null) =>
        new(
            party.Id,
            party.Code,
            party.Name,
            party.LegalName,
            party.TaxId,
            party.Phone,
            party.Email,
            party.Website,
            party.AddressLine1,
            party.AddressLine2,
            party.Ward,
            party.District,
            party.City,
            party.Province,
            party.CountryCode,
            party.PostalCode,
            party.DefaultCurrencyCode,
            party.PaymentTermDays,
            party.CreditLimit,
            party.CreditLimitCurrencyCode,
            party.Notes,
            party.IsActive,
            party.IsBlocked,
            PartyStatusCodes.FromFlags(party.IsActive, party.IsBlocked),
            party.PartyKind,
            party.ShortName,
            party.LegalType,
            party.GroupCode,
            party.ExternalCode,
            party.IndustryCode,
            party.InvoiceEmail,
            party.VatRegistered,
            party.AssignedUserId,
            party.ParentPartyId,
            parentCode,
            parentName,
            party.CreditControlMode,
            party.BlockedReason,
            party.BlockedAt,
            roles,
            party.CreatedAt,
            party.UpdatedAt);
}

public sealed record ListBusinessPartiesQuery(
    string? Search = null,
    string? RoleCode = null,
    bool? IsActive = null) : IRequest<IReadOnlyList<BusinessPartyListItemDto>>;

public sealed class ListBusinessPartiesQueryHandler
    : IRequestHandler<ListBusinessPartiesQuery, IReadOnlyList<BusinessPartyListItemDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ListBusinessPartiesQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<BusinessPartyListItemDto>> Handle(
        ListBusinessPartiesQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var query = _db.BusinessParties.AsNoTracking().AsQueryable();

        if (request.IsActive.HasValue)
        {
            query = query.Where(p => p.IsActive == request.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            query = PartySearch.Apply(query, request.Search);
        }

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
            .ToListAsync(cancellationToken);

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
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<string>)g.Select(x => x.RoleCode).OrderBy(c => c).ToList());

        return parties.Select(p => new BusinessPartyListItemDto(
            p.Id,
            p.Code,
            p.Name,
            p.LegalName,
            p.TaxId,
            p.Phone,
            p.IsActive,
            p.IsBlocked,
            PartyStatusCodes.FromFlags(p.IsActive, p.IsBlocked),
            p.DefaultCurrencyCode,
            p.PaymentTermDays,
            p.CreditLimit,
            p.CreditLimitCurrencyCode,
            rolesByParty.TryGetValue(p.Id, out var rc) ? rc : Array.Empty<string>(),
            p.CreatedAt)).ToList();
    }
}
