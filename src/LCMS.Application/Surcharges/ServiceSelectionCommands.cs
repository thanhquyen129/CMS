using System.Text.Json;
using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Surcharges;

public sealed record ServiceSelectionDto(
    Guid Id,
    string ContextType,
    Guid ContextId,
    string ServiceCode,
    string Scope,
    decimal Quantity,
    string? Uom,
    bool IsSelected,
    string? SourceRef,
    Guid? ConfirmedByUserId,
    DateTimeOffset? ConfirmedAt,
    int Revision);

public sealed record ServiceSelectionAuditDto(
    Guid Id,
    string ServiceCode,
    bool OldIsSelected,
    bool NewIsSelected,
    decimal OldQuantity,
    decimal NewQuantity,
    int Revision,
    Guid? ActorUserId,
    DateTimeOffset ActedAt,
    string? Notes);

public sealed record ListServiceSelectionsQuery(string ContextType, Guid ContextId)
    : IRequest<IReadOnlyList<ServiceSelectionDto>>;

public sealed class ListServiceSelectionsQueryHandler
    : IRequestHandler<ListServiceSelectionsQuery, IReadOnlyList<ServiceSelectionDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;

    public ListServiceSelectionsQueryHandler(ILcmsDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<ServiceSelectionDto>> Handle(
        ListServiceSelectionsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var contextType = request.ContextType.Trim().ToUpperInvariant();
        if (!ServiceSelectionContexts.All.Contains(contextType))
        {
            throw ValidationAppException.Coded(
                "Ngữ cảnh dịch vụ phải là BILL, SHIPMENT hoặc LEG.",
                "CONTEXT_TYPE_INVALID");
        }

        var rows = await _db.ServiceSelections.AsNoTracking()
            .Where(s => s.ContextType == contextType && s.ContextId == request.ContextId)
            .OrderBy(s => s.ServiceCode)
            .ToListAsync(cancellationToken);
        return rows.Select(Map).ToList();
    }

    internal static ServiceSelectionDto Map(ServiceSelection s) =>
        new(s.Id, s.ContextType, s.ContextId, s.ServiceCode, s.Scope, s.Quantity, s.Uom, s.IsSelected,
            s.SourceRef, s.ConfirmedByUserId, s.ConfirmedAt, s.Revision);
}

public sealed record UpsertServiceSelectionCommand(
    string ContextType,
    Guid ContextId,
    string ServiceCode,
    bool IsSelected,
    decimal Quantity = 1m,
    string? Scope = null,
    string? Uom = null,
    string? SourceRef = null,
    int? ExpectedRevision = null) : IRequest<ServiceSelectionDto>;

public sealed class UpsertServiceSelectionCommandValidator : AbstractValidator<UpsertServiceSelectionCommand>
{
    public UpsertServiceSelectionCommandValidator()
    {
        RuleFor(x => x.ContextType).NotEmpty();
        RuleFor(x => x.ContextId).NotEmpty();
        RuleFor(x => x.ServiceCode).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}

public sealed class UpsertServiceSelectionCommandHandler
    : IRequestHandler<UpsertServiceSelectionCommand, ServiceSelectionDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IPermissionService _permissions;
    private readonly ICurrentUserContext _user;
    private readonly IAuditWriter _audit;

    public UpsertServiceSelectionCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenant,
        IPermissionService permissions,
        ICurrentUserContext user,
        IAuditWriter audit)
    {
        _db = db;
        _tenant = tenant;
        _permissions = permissions;
        _user = user;
        _audit = audit;
    }

    public async Task<ServiceSelectionDto> Handle(
        UpsertServiceSelectionCommand request,
        CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await SurchargeActivationPermissions.EnsureServiceSelectAsync(_permissions, cancellationToken);
        var contextType = request.ContextType.Trim().ToUpperInvariant();
        if (!ServiceSelectionContexts.All.Contains(contextType))
        {
            throw ValidationAppException.Coded(
                "Ngữ cảnh dịch vụ phải là BILL, SHIPMENT hoặc LEG.",
                "CONTEXT_TYPE_INVALID");
        }

        var serviceCode = request.ServiceCode.Trim().ToUpperInvariant();
        var scope = string.IsNullOrWhiteSpace(request.Scope)
            ? contextType
            : request.Scope.Trim().ToUpperInvariant();
        var existing = await _db.ServiceSelections.FirstOrDefaultAsync(
            s => s.ContextType == contextType
                 && s.ContextId == request.ContextId
                 && s.ServiceCode == serviceCode,
            cancellationToken);

        var now = DateTimeOffset.UtcNow;
        if (existing is null)
        {
            existing = new ServiceSelection
            {
                TenantId = _tenant.TenantId!.Value,
                ContextType = contextType,
                ContextId = request.ContextId,
                ServiceCode = serviceCode,
                Scope = scope,
                Quantity = request.Quantity,
                Uom = request.Uom,
                IsSelected = request.IsSelected,
                SourceRef = request.SourceRef,
                ConfirmedByUserId = _user.UserId,
                ConfirmedAt = now,
                Revision = 1
            };
            _db.ServiceSelections.Add(existing);
            _db.ServiceSelectionAudits.Add(new ServiceSelectionAudit
            {
                TenantId = existing.TenantId,
                ServiceSelectionId = existing.Id,
                ContextType = contextType,
                ContextId = request.ContextId,
                ServiceCode = serviceCode,
                OldIsSelected = false,
                NewIsSelected = request.IsSelected,
                OldQuantity = 0m,
                NewQuantity = request.Quantity,
                Revision = 1,
                ActorUserId = _user.UserId,
                ActedAt = now,
                Notes = "create"
            });
        }
        else
        {
            if (request.ExpectedRevision is int expected && expected != existing.Revision)
            {
                throw new ConflictAppException(
                    "SERVICE_SELECTION_REVISION_CONFLICT: Lựa chọn dịch vụ đã được người khác cập nhật. Hãy tải lại.");
            }

            var oldSelected = existing.IsSelected;
            var oldQty = existing.Quantity;
            existing.IsSelected = request.IsSelected;
            existing.Quantity = request.Quantity;
            existing.Scope = scope;
            existing.Uom = request.Uom;
            existing.SourceRef = request.SourceRef;
            existing.ConfirmedByUserId = _user.UserId;
            existing.ConfirmedAt = now;
            existing.Revision += 1;
            _db.ServiceSelectionAudits.Add(new ServiceSelectionAudit
            {
                TenantId = existing.TenantId,
                ServiceSelectionId = existing.Id,
                ContextType = contextType,
                ContextId = request.ContextId,
                ServiceCode = serviceCode,
                OldIsSelected = oldSelected,
                NewIsSelected = request.IsSelected,
                OldQuantity = oldQty,
                NewQuantity = request.Quantity,
                Revision = existing.Revision,
                ActorUserId = _user.UserId,
                ActedAt = now,
                Notes = request.IsSelected ? "select" : "unselect"
            });
        }

        await MarkRatingsStaleAsync(contextType, request.ContextId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return ListServiceSelectionsQueryHandler.Map(existing);
    }

    private async Task MarkRatingsStaleAsync(string contextType, Guid contextId, CancellationToken cancellationToken)
    {
        List<Guid> billIds = contextType switch
        {
            "BILL" => [contextId],
            "SHIPMENT" => await _db.BillShipmentLinks.AsNoTracking()
                .Where(l => l.ShipmentId == contextId)
                .Select(l => l.BillId)
                .Distinct()
                .ToListAsync(cancellationToken),
            "LEG" => await _db.BillLegLinks.AsNoTracking()
                .Where(l => l.TransportLegId == contextId)
                .Select(l => l.BillId)
                .Distinct()
                .ToListAsync(cancellationToken),
            _ => []
        };

        if (billIds.Count == 0)
        {
            return;
        }

        var ratings = await _db.Ratings
            .Where(r => billIds.Contains(r.BillId) && r.StaleAt == null && r.Status == RatingStatuses.Completed)
            .ToListAsync(cancellationToken);
        var reason = "SERVICE_SELECTION_CHANGED: Lựa chọn dịch vụ phụ phí đã đổi — cần tính lại giá.";
        foreach (var rating in ratings)
        {
            rating.StaleAt = DateTimeOffset.UtcNow;
            rating.StaleReason = reason;
            _audit.Append(
                AuditActions.RatingMarkStale,
                "rating",
                rating.Id,
                afterJson: JsonSerializer.Serialize(new { billId = rating.BillId, reason }),
                reason: reason);
        }
    }
}

public sealed record ListServiceSelectionAuditsQuery(string ContextType, Guid ContextId, string? ServiceCode = null)
    : IRequest<IReadOnlyList<ServiceSelectionAuditDto>>;

public sealed class ListServiceSelectionAuditsQueryHandler
    : IRequestHandler<ListServiceSelectionAuditsQuery, IReadOnlyList<ServiceSelectionAuditDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;

    public ListServiceSelectionAuditsQueryHandler(ILcmsDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<ServiceSelectionAuditDto>> Handle(
        ListServiceSelectionAuditsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var contextType = request.ContextType.Trim().ToUpperInvariant();
        var query = _db.ServiceSelectionAudits.AsNoTracking()
            .Where(a => a.ContextType == contextType && a.ContextId == request.ContextId);
        if (!string.IsNullOrWhiteSpace(request.ServiceCode))
        {
            var code = request.ServiceCode.Trim().ToUpperInvariant();
            query = query.Where(a => a.ServiceCode == code);
        }

        // Order by Revision in SQL (SQLite rejects DateTimeOffset ORDER BY); refine by ActedAt in-memory.
        var rows = await query.OrderByDescending(a => a.Revision)
            .Take(200)
            .ToListAsync(cancellationToken);
        return rows
            .OrderByDescending(a => a.ActedAt)
            .ThenByDescending(a => a.Revision)
            .Select(a => new ServiceSelectionAuditDto(
                a.Id, a.ServiceCode, a.OldIsSelected, a.NewIsSelected, a.OldQuantity, a.NewQuantity,
                a.Revision, a.ActorUserId, a.ActedAt, a.Notes))
            .ToList();
    }
}
