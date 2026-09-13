using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Exposures.Commands;

public sealed record AdjustAccountsReceivableCommand(
    Guid AccountsReceivableId,
    decimal DeltaAmount,
    string Reason) : IRequest<Guid>;

public sealed class AdjustAccountsReceivableCommandValidator
    : AbstractValidator<AdjustAccountsReceivableCommand>
{
    public AdjustAccountsReceivableCommandValidator()
    {
        RuleFor(x => x.AccountsReceivableId).NotEmpty().WithMessage("Khoản phải thu không hợp lệ.");
        RuleFor(x => x.DeltaAmount)
            .NotEqual(0).WithMessage("Số tiền điều chỉnh không được bằng 0.");
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Lý do điều chỉnh không được để trống.")
            .MaximumLength(1024).WithMessage("Lý do điều chỉnh không được vượt quá 1024 ký tự.");
    }
}

public sealed class AdjustAccountsReceivableCommandHandler : IRequestHandler<AdjustAccountsReceivableCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;

    public AdjustAccountsReceivableCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
    }

    public async Task<Guid> Handle(AdjustAccountsReceivableCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var ar = await _db.AccountsReceivable
            .FirstOrDefaultAsync(a => a.Id == request.AccountsReceivableId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy khoản phải thu.");

        if (ar.RecordStatus != ApArRecordStatuses.Active)
        {
            throw new ConflictAppException("Chỉ được điều chỉnh khoản phải thu đang hiệu lực.");
        }

        var delta = decimal.Round(request.DeltaAmount, 4, MidpointRounding.AwayFromZero);
        var outstandingBefore = ar.DeriveOutstanding();
        if (outstandingBefore + delta < 0)
        {
            throw new ConflictAppException("Số dư còn lại (outstanding) sau điều chỉnh không được âm.");
        }

        var adjBefore = ar.AdjustmentAmount;
        ar.AdjustmentAmount = decimal.Round(ar.AdjustmentAmount + delta, 4, MidpointRounding.AwayFromZero);
        ar.UpdatedAt = DateTimeOffset.UtcNow;
        ar.UpdatedBy = _user.UserId;
        var reason = request.Reason.Trim();
        var reasonLine = $"[điều chỉnh {delta}] {reason}";
        ar.Notes = string.IsNullOrWhiteSpace(ar.Notes)
            ? reasonLine
            : $"{ar.Notes}\n{reasonLine}";

        var row = new AccountsReceivableAdjustment
        {
            TenantId = _tenantContext.TenantId!.Value,
            AccountsReceivableId = ar.Id,
            AdjustmentType = ApArAdjustmentTypes.Adjustment,
            DeltaAmount = delta,
            CurrencyCode = ar.CurrencyCode,
            Reason = reason,
            EffectiveDate = DateOnly.FromDateTime(DateTime.UtcNow),
            AdjustmentAmountBefore = adjBefore,
            AdjustmentAmountAfter = ar.AdjustmentAmount,
            OutstandingBefore = outstandingBefore,
            OutstandingAfter = ar.DeriveOutstanding()
        };
        _db.AccountsReceivableAdjustments.Add(row);
        await _db.SaveChangesAsync(cancellationToken);
        return row.Id;
    }
}
