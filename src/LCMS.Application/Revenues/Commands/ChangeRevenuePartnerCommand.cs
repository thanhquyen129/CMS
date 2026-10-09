using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.BusinessParties;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Revenues.Commands;

public sealed record ChangeRevenuePartnerCommand(Guid RevenueId, Guid? CustomerPartyId, string Reason) : IRequest;

public sealed class ChangeRevenuePartnerCommandValidator : AbstractValidator<ChangeRevenuePartnerCommand>
{
    public ChangeRevenuePartnerCommandValidator()
    {
        RuleFor(x => x.RevenueId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(512).WithMessage("Cần lý do khi đổi khách hàng thực tế.");
    }
}

public sealed class ChangeRevenuePartnerCommandHandler : IRequestHandler<ChangeRevenuePartnerCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IPermissionService _permissions;
    private readonly IPartyDirectoryService _parties;
    private readonly IAuditWriter _audit;

    public ChangeRevenuePartnerCommandHandler(
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

    public async Task Handle(ChangeRevenuePartnerCommand request, CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(PermissionCodes.RevenueCreate, "Bạn không có quyền đổi khách hàng của doanh thu.", cancellationToken);
        var revenue = await _db.Revenues.FirstOrDefaultAsync(r => r.Id == request.RevenueId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy doanh thu.");
        if (request.CustomerPartyId is Guid customerId)
        {
            await _parties.EnsureUsableAsync(customerId, PartyRoleCodes.CustomerSide, "đổi khách hàng doanh thu", cancellationToken);
        }

        var before = revenue.CustomerPartyId?.ToString();
        revenue.CustomerPartyId = request.CustomerPartyId;
        revenue.PartnerOverrideRequiresRerate = revenue.PartnerSuggestedId is Guid suggested && suggested != request.CustomerPartyId;
        _audit.Append(
            AuditActions.RevenuePartnerChange,
            AuditObjectTypes.Revenue,
            revenue.Id,
            before,
            request.CustomerPartyId?.ToString(),
            request.Reason.Trim());
        await _db.SaveChangesAsync(cancellationToken);
    }
}
