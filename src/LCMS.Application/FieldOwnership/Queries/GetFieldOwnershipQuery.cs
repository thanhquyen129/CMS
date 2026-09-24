using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Ownership.Queries;

public sealed record FieldOwnershipItemDto(
    string FieldName,
    string OwnerSystem,
    string? OverrideReason,
    DateTimeOffset? OverriddenAt);

public sealed record GetFieldOwnershipQuery(string ObjectType, Guid ObjectId)
    : IRequest<IReadOnlyList<FieldOwnershipItemDto>>;

public sealed class GetFieldOwnershipQueryValidator : AbstractValidator<GetFieldOwnershipQuery>
{
    private static readonly string[] Allowed =
        ["bill", "order", "shipment", "leg", "movement", "revenue"];

    public GetFieldOwnershipQueryValidator()
    {
        RuleFor(x => x.ObjectType)
            .NotEmpty().WithMessage("Loại đối tượng không được để trống.")
            .Must(t => Allowed.Contains(t.Trim().ToLowerInvariant()))
            .WithMessage("Loại đối tượng không được hỗ trợ.");
        RuleFor(x => x.ObjectId)
            .NotEmpty().WithMessage("Thiếu mã đối tượng.");
    }
}

/// <summary>
/// Ownership rows for one object, plus the latest field.override reason.
/// Revenue rows require revenue.read. Other types require bill.read.
/// A caller without that permission gets an empty list.
/// </summary>
public sealed class GetFieldOwnershipQueryHandler
    : IRequestHandler<GetFieldOwnershipQuery, IReadOnlyList<FieldOwnershipItemDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;

    public GetFieldOwnershipQueryHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
    }

    public async Task<IReadOnlyList<FieldOwnershipItemDto>> Handle(
        GetFieldOwnershipQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var type = request.ObjectType.Trim().ToLowerInvariant();
        var allowed = type == "revenue"
            ? await _permissions.HasPermissionAsync(PermissionCodes.RevenueRead, cancellationToken)
            : await _permissions.HasPermissionAsync(PermissionCodes.BillRead, cancellationToken);
        if (!allowed)
        {
            return [];
        }

        var rows = await _db.FieldOwnerships.AsNoTracking()
            .Where(f => f.ObjectType == type && f.ObjectId == request.ObjectId)
            .OrderBy(f => f.FieldName)
            .Select(f => new { f.FieldName, f.OwnerSystem })
            .ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return [];
        }

        var overrides = (await _db.AuditEvents.AsNoTracking()
            .Where(e =>
                e.ObjectType == type
                && e.ObjectId == request.ObjectId
                && e.Action == AuditActions.FieldOverride)
            .Select(e => new { e.AfterJson, e.Reason, e.OccurredAt })
            .ToListAsync(cancellationToken))
            .OrderByDescending(e => e.OccurredAt)
            .Take(40)
            .ToList();

        return rows
            .Select(row =>
            {
                var latest = overrides.FirstOrDefault(o =>
                    string.Equals(o.AfterJson, row.FieldName, StringComparison.OrdinalIgnoreCase));
                return new FieldOwnershipItemDto(
                    row.FieldName,
                    row.OwnerSystem,
                    string.IsNullOrWhiteSpace(latest?.Reason) ? null : latest!.Reason,
                    latest?.OccurredAt);
            })
            .ToList();
    }
}
