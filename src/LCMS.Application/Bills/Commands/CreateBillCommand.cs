using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.BusinessParties;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using LCMS.Application.ReferenceMasters;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Bills.Commands;

/// <summary>Creates Bill financial anchor (TD1). Tenant lấy từ session (C-001).</summary>
public sealed record CreateBillCommand(
    string BillNo,
    string BillType,
    string? SourceSystem,
    string? ExternalId,
    Guid? OrganizationId,
    Guid? CustomerPartyId = null,
    Guid? PayerPartyId = null,
    Guid? ShipperPartyId = null,
    Guid? ConsigneePartyId = null,
    Guid? BillToPartyId = null) : IRequest<Guid>;

public sealed class CreateBillCommandValidator : AbstractValidator<CreateBillCommand>
{
    public CreateBillCommandValidator()
    {
        RuleFor(x => x.BillNo)
            .NotEmpty()
            .WithMessage("Số Bill không được để trống.")
            .MaximumLength(64)
            .WithMessage("Số Bill không được vượt quá 64 ký tự.");

        RuleFor(x => x.BillType)
            .NotEmpty()
            .WithMessage("Loại Bill không được để trống.")
            .MaximumLength(64)
            .WithMessage("Loại Bill không được vượt quá 64 ký tự.");

        RuleFor(x => x.SourceSystem)
            .MaximumLength(64)
            .When(x => x.SourceSystem is not null);

        RuleFor(x => x.ExternalId)
            .MaximumLength(128)
            .When(x => x.ExternalId is not null);
    }
}

public sealed class CreateBillCommandHandler : IRequestHandler<CreateBillCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _userContext;
    private readonly IPermissionService _permissions;
    private readonly IPartyDirectoryService _parties;
    private readonly IPartySnapshotCapture _snapshots;
    private readonly IBillPartyPolicyStore _partyPolicy;

    public CreateBillCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext userContext,
        IPermissionService permissions,
        IPartyDirectoryService parties,
        IPartySnapshotCapture snapshots,
        IBillPartyPolicyStore partyPolicy)
    {
        _db = db;
        _tenantContext = tenantContext;
        _userContext = userContext;
        _permissions = permissions;
        _parties = parties;
        _snapshots = snapshots;
        _partyPolicy = partyPolicy;
    }

    public async Task<Guid> Handle(CreateBillCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(
            PermissionCodes.BillCreate,
            "Bạn không có quyền tạo Bill.",
            cancellationToken);

        var tenantId = _tenantContext.TenantId!.Value;

        var tenantExists = await _db.Tenants.AnyAsync(t => t.Id == tenantId && t.IsActive, cancellationToken);
        if (!tenantExists)
        {
            throw new NotFoundAppException("Không tìm thấy thuê bao hoặc thuê bao không còn hiệu lực.");
        }

        Guid? organizationId = request.OrganizationId;
        if (organizationId is Guid orgId)
        {
            var orgExists = await _db.Organizations.AnyAsync(o => o.Id == orgId, cancellationToken);
            if (!orgExists)
            {
                throw new NotFoundAppException("Không tìm thấy tổ chức.");
            }
        }
        else if (_userContext.HasUser)
        {
            var actor = await _db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == _userContext.UserId, cancellationToken);
            organizationId = actor?.OrganizationId;
        }

        var billNo = request.BillNo.Trim();
        var duplicate = await _db.Bills.AnyAsync(
            b => b.TenantId == tenantId && b.BillNo == billNo,
            cancellationToken);
        if (duplicate)
        {
            throw new ConflictAppException("Số Bill đã tồn tại trong thuê bao này.");
        }

        if (request.CustomerPartyId is Guid customerId)
        {
            await _parties.EnsureUsableAsync(
                customerId,
                [PartyRoleCodes.Customer],
                "gắn khách hàng lên Bill",
                cancellationToken);
        }

        var bill = new Bill
        {
            TenantId = tenantId,
            BillNo = billNo,
            BillType = request.BillType.Trim(),
            SourceSystem = string.IsNullOrWhiteSpace(request.SourceSystem) ? null : request.SourceSystem.Trim(),
            ExternalId = string.IsNullOrWhiteSpace(request.ExternalId) ? null : request.ExternalId.Trim(),
            OperationalStatus = "active",
            IsActive = true,
            OrganizationId = organizationId,
            CustomerPartyId = request.CustomerPartyId,
            PayerPartyId = request.PayerPartyId,
            ShipperPartyId = request.ShipperPartyId,
            ConsigneePartyId = request.ConsigneePartyId,
            BillToPartyId = request.BillToPartyId
        };

        await BillPartyRoles.EnsureRequiredAsync(bill, await _partyPolicy.GetAsync(cancellationToken), cancellationToken);
        _db.Bills.Add(bill);
        await BillPartyRoles.CaptureAsync(bill, _parties, _snapshots, cancellationToken);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ConflictAppException("Số Bill đã tồn tại trong thuê bao này.");
        }

        return bill.Id;
    }
}
