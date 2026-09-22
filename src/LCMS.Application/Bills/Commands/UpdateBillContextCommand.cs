using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.BusinessParties;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Identity;
using LCMS.Application.OperationalReferences;
using LCMS.Application.ReferenceMasters;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Bills.Commands;

/// <summary>
/// Updates Bill financial-ops context for UI-02 (customer/route/ETD/ETA/assignee/notes).
/// Does not mutate ledger amounts or maturity.
/// </summary>
public sealed record UpdateBillContextCommand(
    Guid BillId,
    Guid? CustomerPartyId,
    string? RouteCode,
    DateTimeOffset? EtdAt,
    DateTimeOffset? EtaAt,
    Guid? AssignedUserId,
    string? Description,
    string? InternalNote,
    string? TransportMode = null,
    string? OriginCode = null,
    string? DestinationCode = null,
    string? CustomerReference = null,
    OperationalContextDocument? Context = null,
    bool ApplyExtendedContext = false,
    bool ApplyPartyRoles = false,
    Guid? PayerPartyId = null,
    Guid? ShipperPartyId = null,
    Guid? ConsigneePartyId = null,
    Guid? BillToPartyId = null,
    Guid? RouteId = null) : IRequest;

public sealed class UpdateBillContextCommandValidator : AbstractValidator<UpdateBillContextCommand>
{
    public UpdateBillContextCommandValidator()
    {
        RuleFor(x => x.RouteCode)
            .MaximumLength(128)
            .When(x => x.RouteCode is not null);

        RuleFor(x => x.Description)
            .MaximumLength(2000)
            .When(x => x.Description is not null);

        RuleFor(x => x.InternalNote)
            .MaximumLength(4000)
            .When(x => x.InternalNote is not null);

        RuleFor(x => x.TransportMode).MaximumLength(32).When(x => x.TransportMode is not null);
        RuleFor(x => x.OriginCode).MaximumLength(64).When(x => x.OriginCode is not null);
        RuleFor(x => x.DestinationCode).MaximumLength(64).When(x => x.DestinationCode is not null);
        RuleFor(x => x.CustomerReference).MaximumLength(128).When(x => x.CustomerReference is not null);

        RuleFor(x => x)
            .Must(x => x.EtdAt is null || x.EtaAt is null || x.EtdAt <= x.EtaAt)
            .WithMessage("ETD không được sau ETA.");
    }
}

public sealed class UpdateBillContextCommandHandler : IRequestHandler<UpdateBillContextCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _userContext;
    private readonly IPermissionService _permissions;
    private readonly IOrganizationHierarchyService _orgHierarchy;
    private readonly IPartyDirectoryService _parties;
    private readonly IPartySnapshotCapture _snapshots;
    private readonly IBillPartyPolicyStore _partyPolicy;
    private readonly ICanonicalPlaceBinder _places;
    private readonly IOperationalCargoStore _cargo;

    public UpdateBillContextCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext userContext,
        IPermissionService permissions,
        IOrganizationHierarchyService orgHierarchy,
        IPartyDirectoryService parties,
        IPartySnapshotCapture snapshots,
        IBillPartyPolicyStore partyPolicy,
        ICanonicalPlaceBinder places,
        IOperationalCargoStore cargo)
    {
        _db = db;
        _tenantContext = tenantContext;
        _userContext = userContext;
        _permissions = permissions;
        _orgHierarchy = orgHierarchy;
        _parties = parties;
        _snapshots = snapshots;
        _partyPolicy = partyPolicy;
        _places = places;
        _cargo = cargo;
    }

    public async Task Handle(UpdateBillContextCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var canUpdate = await _permissions.HasPermissionAsync(
            PermissionCodes.BillUpdate,
            cancellationToken);
        var canCreate = await _permissions.HasPermissionAsync(
            PermissionCodes.BillCreate,
            cancellationToken);
        if (!canUpdate && !canCreate)
        {
            throw new ForbiddenAppException("Bạn không có quyền cập nhật ngữ cảnh Bill.");
        }

        var scope = await _permissions.EnsureAndResolveDataScopeAsync(
            canUpdate ? PermissionCodes.BillUpdate : PermissionCodes.BillCreate,
            "Bạn không có quyền cập nhật ngữ cảnh Bill.",
            cancellationToken);

        var bill = await _db.Bills.FirstOrDefaultAsync(b => b.Id == request.BillId, cancellationToken);
        if (bill is null)
        {
            throw new NotFoundAppException("Không tìm thấy Bill.");
        }

        Guid? actorOrgId = null;
        IReadOnlySet<Guid> orgSubtree = new HashSet<Guid>();
        if (scope == DataScopes.Organization && _userContext.HasUser)
        {
            var actor = await _db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == _userContext.UserId, cancellationToken);
            actorOrgId = actor?.OrganizationId;
            orgSubtree = await _orgHierarchy.GetSubtreeIdsAsync(actorOrgId, cancellationToken);
        }

        if (!DataScopeAccess.Allows(
                scope,
                _userContext.UserId,
                actorOrgId,
                orgSubtree,
                bill.CreatedBy,
                bill.OrganizationId))
        {
            throw new NotFoundAppException("Không tìm thấy Bill.");
        }

        if (request.CustomerPartyId is Guid partyId)
        {
            await _parties.EnsureUsableAsync(
                partyId,
                [PartyRoleCodes.Customer],
                "gắn khách hàng lên Bill",
                cancellationToken);
        }

        if (request.AssignedUserId is Guid userId)
        {
            var userOk = await _db.Users.AsNoTracking()
                .AnyAsync(u => u.Id == userId, cancellationToken);
            if (!userOk)
            {
                throw new NotFoundAppException("Không tìm thấy người phụ trách.");
            }
        }

        bill.CustomerPartyId = request.CustomerPartyId;
        if (request.ApplyPartyRoles)
        {
            bill.PayerPartyId = request.PayerPartyId;
            bill.ShipperPartyId = request.ShipperPartyId;
            bill.ConsigneePartyId = request.ConsigneePartyId;
            bill.BillToPartyId = request.BillToPartyId;
        }

        var route = OperationalContextJson.TrimOrNull(request.RouteCode);
        if (route is null
            && !string.IsNullOrWhiteSpace(request.OriginCode)
            && !string.IsNullOrWhiteSpace(request.DestinationCode))
        {
            route = $"{request.OriginCode.Trim()} → {request.DestinationCode.Trim()}";
        }

        bill.RouteCode = route;
        bill.EtdAt = request.EtdAt;
        bill.EtaAt = request.EtaAt;
        bill.AssignedUserId = request.AssignedUserId;
        bill.Description = OperationalContextJson.TrimOrNull(request.Description);
        bill.InternalNote = OperationalContextJson.TrimOrNull(request.InternalNote);
        if (request.ApplyExtendedContext || request.RouteId is not null)
        {
            if (request.ApplyExtendedContext)
            {
                bill.TransportMode = OperationalContextJson.TrimOrNull(request.TransportMode);
                bill.OriginCode = OperationalContextJson.TrimOrNull(request.OriginCode);
                bill.DestinationCode = OperationalContextJson.TrimOrNull(request.DestinationCode);
                bill.CustomerReference = OperationalContextJson.TrimOrNull(request.CustomerReference);
                bill.ContextJson = OperationalContextJson.Serialize(request.Context);
            }

            await BindPlacesAsync(bill, request, cancellationToken);
        }

        await BillPartyRoles.EnsureRequiredAsync(bill, await _partyPolicy.GetAsync(cancellationToken), cancellationToken);
        await BillPartyRoles.CaptureAsync(bill, _parties, _snapshots, cancellationToken);
        if (request.ApplyExtendedContext)
        {
            await _cargo.ApplyAsync(
                OperationalObjectTypes.Bill,
                bill.Id,
                request.Context,
                OperationalSourceSystems.LcmsManual,
                request.OriginCode is not null || request.DestinationCode is not null,
                cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task BindPlacesAsync(Bill bill, UpdateBillContextCommand request, CancellationToken cancellationToken)
    {
        if (request.RouteId is Guid routeId)
        {
            var route = await _places.RequireRouteAsync(routeId, cancellationToken);
            var origin = await _db.Locations.AsNoTracking().FirstAsync(l => l.Id == route.OriginLocationId, cancellationToken);
            var destination = await _db.Locations.AsNoTracking().FirstAsync(l => l.Id == route.DestinationLocationId, cancellationToken);
            bill.RouteId = route.Id;
            bill.OriginLocationId = origin.Id;
            bill.DestinationLocationId = destination.Id;
            bill.OriginCode = origin.Code;
            bill.DestinationCode = destination.Code;
            bill.RouteCode = route.Code;
            return;
        }

        var boundOrigin = await _places.BindAsync(bill.OriginCode, "Điểm đi", cancellationToken);
        var boundDestination = await _places.BindAsync(bill.DestinationCode, "Điểm đến", cancellationToken);
        bill.OriginLocationId = boundOrigin.LocationId;
        bill.DestinationLocationId = boundDestination.LocationId;
        if (boundOrigin.Code is not null)
        {
            bill.OriginCode = boundOrigin.Code;
        }

        if (boundDestination.Code is not null)
        {
            bill.DestinationCode = boundDestination.Code;
        }
    }
}
