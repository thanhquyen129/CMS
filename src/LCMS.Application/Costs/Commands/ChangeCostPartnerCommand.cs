using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.BusinessParties;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Costs.Commands;

public sealed record ChangeCostPartnerCommand(Guid CostId, Guid? VendorPartyId, string Reason) : IRequest;

public sealed class ChangeCostPartnerCommandValidator : AbstractValidator<ChangeCostPartnerCommand>
{
    public ChangeCostPartnerCommandValidator()
    {
        RuleFor(x => x.CostId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(512).WithMessage("Cần lý do khi đổi nhà cung cấp thực tế.");
    }
}

public sealed class ChangeCostPartnerCommandHandler : IRequestHandler<ChangeCostPartnerCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IPermissionService _permissions;
    private readonly IPartyDirectoryService _parties;
    private readonly IAuditWriter _audit;

    public ChangeCostPartnerCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenant,
        IPermissionService permissions,
        IPartyDirectoryService parties,
        IAuditWriter audit)
    {
        _db = db;
        _tenant = tenant;
        _permissions = permissions;
        _parties = parties;
        _audit = audit;
    }

    public async Task Handle(ChangeCostPartnerCommand request, CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(PermissionCodes.CostCreate, "Bạn không có quyền đổi nhà cung cấp của chi phí.", cancellationToken);
        var cost = await _db.Costs.FirstOrDefaultAsync(c => c.Id == request.CostId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy chi phí.");
        if (request.VendorPartyId is Guid vendorId)
        {
            await _parties.EnsureUsableAsync(vendorId, PartyRoleCodes.VendorSide, "đổi nhà cung cấp chi phí", cancellationToken);
        }

        var before = cost.VendorPartyId?.ToString();
        cost.VendorPartyId = request.VendorPartyId;
        cost.PartnerOverrideRequiresRerate = cost.PartnerSuggestedId is Guid suggested && suggested != request.VendorPartyId;
        _audit.Append(
            AuditActions.CostPartnerChange,
            AuditObjectTypes.Cost,
            cost.Id,
            before,
            request.VendorPartyId?.ToString(),
            request.Reason.Trim());
        await _db.SaveChangesAsync(cancellationToken);
    }
}
