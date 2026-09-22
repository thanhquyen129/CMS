using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.BusinessParties;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.OperationalReferences;
using LCMS.Application.ReferenceMasters;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Orders.Commands;

/// <summary>
/// Idempotent upsert by (tenant_id, source_system, external_id) — C-002.
/// Optional operational context is applied only when at least one context field is present,
/// so TMS/API identity upserts do not wipe LCMS manual context.
/// </summary>
public sealed record UpsertOrderCommand(
    string OrderNo,
    string SourceSystem,
    string ExternalId,
    string? ExternalVersion,
    string? OperationalStatus,
    bool IsActive = true,
    Guid? CustomerPartyId = null,
    Guid? AssignedUserId = null,
    string? TransportMode = null,
    string? OriginCode = null,
    string? DestinationCode = null,
    string? RouteCode = null,
    DateTimeOffset? EtdAt = null,
    DateTimeOffset? EtaAt = null,
    string? CustomerReference = null,
    string? Description = null,
    OperationalContextDocument? Context = null,
    bool ApplyContext = false,
    Guid? RouteId = null) : IRequest<Guid>;

public sealed class UpsertOrderCommandValidator : AbstractValidator<UpsertOrderCommand>
{
    public UpsertOrderCommandValidator()
    {
        RuleFor(x => x.OrderNo)
            .NotEmpty().WithMessage("Số đơn hàng không được để trống.")
            .MaximumLength(64).WithMessage("Số đơn hàng không được vượt quá 64 ký tự.");

        RuleFor(x => x.SourceSystem)
            .NotEmpty().WithMessage("Hệ thống nguồn không được để trống.")
            .MaximumLength(64).WithMessage("Hệ thống nguồn không được vượt quá 64 ký tự.");

        RuleFor(x => x.ExternalId)
            .NotEmpty().WithMessage("Mã tham chiếu ngoài không được để trống.")
            .MaximumLength(128).WithMessage("Mã tham chiếu ngoài không được vượt quá 128 ký tự.");

        RuleFor(x => x.ExternalVersion)
            .MaximumLength(64)
            .When(x => x.ExternalVersion is not null);

        RuleFor(x => x.OperationalStatus)
            .MaximumLength(64)
            .When(x => x.OperationalStatus is not null);

        RuleFor(x => x.TransportMode).MaximumLength(32).When(x => x.TransportMode is not null);
        RuleFor(x => x.OriginCode).MaximumLength(64).When(x => x.OriginCode is not null);
        RuleFor(x => x.DestinationCode).MaximumLength(64).When(x => x.DestinationCode is not null);
        RuleFor(x => x.RouteCode).MaximumLength(128).When(x => x.RouteCode is not null);
        RuleFor(x => x.CustomerReference).MaximumLength(128).When(x => x.CustomerReference is not null);
        RuleFor(x => x.Description).MaximumLength(2000).When(x => x.Description is not null);
        RuleFor(x => x)
            .Must(x => x.EtdAt is null || x.EtaAt is null || x.EtdAt <= x.EtaAt)
            .WithMessage("ETD không được sau ETA.");
    }
}

public sealed class UpsertOrderCommandHandler : IRequestHandler<UpsertOrderCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPartyDirectoryService _parties;
    private readonly ICanonicalPlaceBinder _places;
    private readonly IPartySnapshotCapture _snapshots;

    public UpsertOrderCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPartyDirectoryService parties,
        ICanonicalPlaceBinder places,
        IPartySnapshotCapture snapshots)
    {
        _db = db;
        _tenantContext = tenantContext;
        _parties = parties;
        _places = places;
        _snapshots = snapshots;
    }

    public async Task<Guid> Handle(UpsertOrderCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;
        var sourceSystem = request.SourceSystem.Trim();
        var externalId = request.ExternalId.Trim();
        var orderNo = request.OrderNo.Trim();
        var status = string.IsNullOrWhiteSpace(request.OperationalStatus)
            ? "active"
            : request.OperationalStatus.Trim();
        var externalVersion = string.IsNullOrWhiteSpace(request.ExternalVersion)
            ? null
            : request.ExternalVersion.Trim();

        if (request.ApplyContext && request.CustomerPartyId is Guid customerId)
        {
            await _parties.EnsureUsableAsync(
                customerId,
                [PartyRoleCodes.Customer],
                "gắn khách hàng lên đơn hàng",
                cancellationToken);
        }

        var existing = await _db.Orders.FirstOrDefaultAsync(
            o => o.TenantId == tenantId
                 && o.SourceSystem == sourceSystem
                 && o.ExternalId == externalId,
            cancellationToken);

        if (existing is null)
        {
            var order = new Order
            {
                TenantId = tenantId,
                OrderNo = orderNo,
                SourceSystem = sourceSystem,
                ExternalId = externalId,
                ExternalVersion = externalVersion,
                OperationalStatus = status,
                IsActive = request.IsActive
            };
            ApplyContext(order, request);
            await BindPlacesAsync(order, request, cancellationToken);
            _db.Orders.Add(order);
            if (request.ApplyContext && order.CustomerPartyId is Guid newCustomer)
            {
                await _snapshots.CapturePartyAsync(PartySnapshotObjectTypes.Order, order.Id, PartyRoleCodes.Customer, newCustomer, cancellationToken);
            }
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                existing = await _db.Orders.FirstOrDefaultAsync(
                    o => o.TenantId == tenantId
                         && o.SourceSystem == sourceSystem
                         && o.ExternalId == externalId,
                    cancellationToken);
                if (existing is null)
                {
                    throw new ConflictAppException(
                        "Đơn hàng với mã tham chiếu ngoài này đã tồn tại trong thuê bao.");
                }
            }

            if (existing is null)
            {
                return order.Id;
            }
        }

        existing.OrderNo = orderNo;
        existing.ExternalVersion = externalVersion;
        existing.OperationalStatus = status;
        existing.IsActive = request.IsActive;
        ApplyContext(existing, request);
        await BindPlacesAsync(existing, request, cancellationToken);
        if (request.ApplyContext && existing.CustomerPartyId is Guid customer)
        {
            await _snapshots.CapturePartyAsync(PartySnapshotObjectTypes.Order, existing.Id, PartyRoleCodes.Customer, customer, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return existing.Id;
    }

    private static void ApplyContext(Order order, UpsertOrderCommand request)
    {
        if (!request.ApplyContext)
        {
            return;
        }

        order.CustomerPartyId = request.CustomerPartyId;
        order.AssignedUserId = request.AssignedUserId;
        order.TransportMode = OperationalContextJson.TrimOrNull(request.TransportMode);
        order.OriginCode = OperationalContextJson.TrimOrNull(request.OriginCode);
        order.DestinationCode = OperationalContextJson.TrimOrNull(request.DestinationCode);
        var route = OperationalContextJson.TrimOrNull(request.RouteCode);
        if (route is null
            && !string.IsNullOrWhiteSpace(request.OriginCode)
            && !string.IsNullOrWhiteSpace(request.DestinationCode))
        {
            route = $"{request.OriginCode.Trim()} → {request.DestinationCode.Trim()}";
        }

        order.RouteCode = route;
        order.EtdAt = request.EtdAt;
        order.EtaAt = request.EtaAt;
        order.CustomerReference = OperationalContextJson.TrimOrNull(request.CustomerReference);
        order.Description = OperationalContextJson.TrimOrNull(request.Description);
        order.ContextJson = OperationalContextJson.Serialize(request.Context);
    }

    private async Task BindPlacesAsync(Order order, UpsertOrderCommand request, CancellationToken cancellationToken)
    {
        if (!request.ApplyContext && request.RouteId is null)
        {
            return;
        }

        if (request.RouteId is Guid routeId)
        {
            var route = await _places.RequireRouteAsync(routeId, cancellationToken);
            var origin = await _db.Locations.AsNoTracking().FirstAsync(l => l.Id == route.OriginLocationId, cancellationToken);
            var destination = await _db.Locations.AsNoTracking().FirstAsync(l => l.Id == route.DestinationLocationId, cancellationToken);
            order.RouteId = route.Id;
            order.OriginLocationId = origin.Id;
            order.DestinationLocationId = destination.Id;
            order.OriginCode = origin.Code;
            order.DestinationCode = destination.Code;
            order.RouteCode = route.Code;
            return;
        }

        var boundOrigin = await _places.BindAsync(order.OriginCode, "Điểm đi", cancellationToken);
        var boundDestination = await _places.BindAsync(order.DestinationCode, "Điểm đến", cancellationToken);
        order.OriginLocationId = boundOrigin.LocationId;
        order.DestinationLocationId = boundDestination.LocationId;
        if (boundOrigin.Code is not null)
        {
            order.OriginCode = boundOrigin.Code;
        }

        if (boundDestination.Code is not null)
        {
            order.DestinationCode = boundDestination.Code;
        }
    }
}
