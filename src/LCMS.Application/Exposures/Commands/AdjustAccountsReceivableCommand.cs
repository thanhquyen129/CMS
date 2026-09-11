using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Exposures.Commands;

/// <summary>
/// Adjust recognized AR. Outstanding remains derived (C-015) — never user-entered SoT.
/// </summary>
public sealed record AdjustAccountsReceivableCommand(
    Guid AccountsReceivableId,
    decimal DeltaAmount,
    string Reason) : IRequest;

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

public sealed class AdjustAccountsReceivableCommandHandler : IRequestHandler<AdjustAccountsReceivableCommand>
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

    public async Task Handle(AdjustAccountsReceivableCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var ar = await _db.AccountsReceivable
            .FirstOrDefaultAsync(a => a.Id == request.AccountsReceivableId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy khoản phải thu.");

        if (ar.RecordStatus != "active")
        {
            throw new ConflictAppException("Chỉ được điều chỉnh khoản phải thu đang hiệu lực.");
        }

        var delta = decimal.Round(request.DeltaAmount, 4, MidpointRounding.AwayFromZero);
        var nextOutstanding = ar.RecognizedAmount + ar.AdjustmentAmount + delta - ar.FinalizedSettledAmount;
        if (nextOutstanding < 0)
        {
            throw new ConflictAppException("Số dư còn lại (outstanding) sau điều chỉnh không được âm.");
        }

        ar.AdjustmentAmount = decimal.Round(ar.AdjustmentAmount + delta, 4, MidpointRounding.AwayFromZero);
        ar.UpdatedAt = DateTimeOffset.UtcNow;
        ar.UpdatedBy = _user.UserId;
        var reasonLine = $"[điều chỉnh {delta}] {request.Reason.Trim()}";
        ar.Notes = string.IsNullOrWhiteSpace(ar.Notes)
            ? reasonLine
            : $"{ar.Notes}\n{reasonLine}";

        await _db.SaveChangesAsync(cancellationToken);
    }
}
