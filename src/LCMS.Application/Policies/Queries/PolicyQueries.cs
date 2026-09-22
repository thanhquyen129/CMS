using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Policies.Queries;

public sealed record PolicyListItemDto(
    Guid Id,
    string PolicyKey,
    string Title,
    Guid? OwnerUserId,
    int Version,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    string Status,
    string? BodyJson,
    string? Notes);

public sealed record ListPoliciesQuery(string? PolicyKey = null, bool LatestOnly = true)
    : IRequest<IReadOnlyList<PolicyListItemDto>>;

public sealed class ListPoliciesQueryHandler : IRequestHandler<ListPoliciesQuery, IReadOnlyList<PolicyListItemDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;

    public ListPoliciesQueryHandler(ILcmsDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<PolicyListItemDto>> Handle(
        ListPoliciesQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var q = _db.Policies.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.PolicyKey))
        {
            var key = request.PolicyKey.Trim().ToUpperInvariant();
            q = q.Where(p => p.PolicyKey == key);
        }

        var rows = await q
            .OrderBy(p => p.PolicyKey)
            .ThenByDescending(p => p.Version)
            .ToListAsync(cancellationToken);

        if (request.LatestOnly)
        {
            rows = rows
                .GroupBy(p => p.PolicyKey, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(p => p.PolicyKey)
                .ToList();
        }

        return rows.Select(p => new PolicyListItemDto(
            p.Id,
            p.PolicyKey,
            p.Title,
            p.OwnerUserId,
            p.Version,
            p.EffectiveFrom,
            p.EffectiveTo,
            p.Status,
            p.BodyJson,
            p.Notes)).ToList();
    }
}

public sealed record GetActivePolicyByKeyQuery(string PolicyKey, DateOnly? AsOf)
    : IRequest<PolicyListItemDto?>;

public sealed class GetActivePolicyByKeyQueryHandler
    : IRequestHandler<GetActivePolicyByKeyQuery, PolicyListItemDto?>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;

    public GetActivePolicyByKeyQueryHandler(ILcmsDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<PolicyListItemDto?> Handle(
        GetActivePolicyByKeyQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var key = request.PolicyKey.Trim().ToUpperInvariant();
        var asOf = request.AsOf ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var row = await _db.Policies.AsNoTracking()
            .Where(p =>
                p.PolicyKey == key
                && p.Status == PolicyStatuses.Active
                && p.EffectiveFrom <= asOf
                && (p.EffectiveTo == null || p.EffectiveTo >= asOf))
            .OrderByDescending(p => p.Version)
            .FirstOrDefaultAsync(cancellationToken);

        return row is null
            ? null
            : new PolicyListItemDto(
                row.Id,
                row.PolicyKey,
                row.Title,
                row.OwnerUserId,
                row.Version,
                row.EffectiveFrom,
                row.EffectiveTo,
                row.Status,
                row.BodyJson,
                row.Notes);
    }
}
