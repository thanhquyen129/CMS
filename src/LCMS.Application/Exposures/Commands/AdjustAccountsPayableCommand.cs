using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Exposures.Commands;

/// <summary>
/// Adjust recognized AP. Outstanding remains derived (C-015) — never user-entered SoT.
/// </summary>
public sealed record AdjustAccountsPayableCommand(
    Guid AccountsPayableId,
    decimal DeltaAmount,
    string Reason) : IRequest;

public sealed class AdjustAccountsPayableCommandValidator : AbstractValidator<AdjustAccountsPayableCommand>
{
    public AdjustAccountsPayableCommandValidator()
    {
        RuleFor(x => x.AccountsPayableId).NotEmpty().WithMessage("Khoản phải trả không hợp lệ.");
        RuleFor(x => x.DeltaAmount)
            .NotEqual(0).WithMessage("Số tiền điều chỉnh không được bằng 0.");
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Lý do điều chỉnh không được để trống.")
            .MaximumLength(1024).WithMessage("Lý do điều chỉnh không được vượt quá 1024 ký tự.");
    }
}

public sealed class AdjustAccountsPayableCommandHandler : IRequestHandler<AdjustAccountsPayableCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;

    public AdjustAccountsPayableCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
    }

    public async Task Handle(AdjustAccountsPayableCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var ap = await _db.AccountsPayable
            .FirstOrDefaultAsync(a => a.Id == request.AccountsPayableId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy khoản phải trả.");

        if (ap.RecordStatus != "active")
        {
            throw new ConflictAppException("Chỉ được điều chỉnh khoản phải trả đang hiệu lực.");
        }

        var delta = decimal.Round(request.DeltaAmount, 4, MidpointRounding.AwayFromZero);
        var nextOutstanding = ap.RecognizedAmount + ap.AdjustmentAmount + delta - ap.FinalizedSettledAmount;
        if (nextOutstanding < 0)
        {
            throw new ConflictAppException("Số dư còn lại (outstanding) sau điều chỉnh không được âm.");
        }

        ap.AdjustmentAmount = decimal.Round(ap.AdjustmentAmount + delta, 4, MidpointRounding.AwayFromZero);
        ap.UpdatedAt = DateTimeOffset.UtcNow;
        ap.UpdatedBy = _user.UserId;
        // Notes append reason for Pass 1 audit trail (dedicated AP adjustment table deferred).
        var reasonLine = $"[điều chỉnh {delta}] {request.Reason.Trim()}";
        ap.Notes = string.IsNullOrWhiteSpace(ap.Notes)
            ? reasonLine
            : $"{ap.Notes}\n{reasonLine}";

        await _db.SaveChangesAsync(cancellationToken);
    }
}
