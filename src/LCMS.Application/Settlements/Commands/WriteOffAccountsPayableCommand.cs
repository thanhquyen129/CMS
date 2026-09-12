using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LCMS.Application.Settlements.Commands;

/// <summary>
/// Small-remainder AP write-off stub: reduces obligation via AdjustmentAmount + required reason note.
/// Never silently wipes outstanding; never invents Cost (C-003); does not fake settlement cash.
/// </summary>
public sealed record WriteOffAccountsPayableCommand(
    Guid AccountsPayableId,
    decimal Amount,
    string Reason) : IRequest;

public sealed class WriteOffAccountsPayableCommandValidator : AbstractValidator<WriteOffAccountsPayableCommand>
{
    public WriteOffAccountsPayableCommandValidator()
    {
        RuleFor(x => x.AccountsPayableId).NotEmpty().WithMessage("Khoản phải trả không hợp lệ.");
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Số tiền xóa nợ phải lớn hơn 0.");
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Lý do xóa nợ không được để trống.")
            .MaximumLength(1024).WithMessage("Lý do xóa nợ không được vượt quá 1024 ký tự.");
    }
}

public sealed class WriteOffAccountsPayableCommandHandler : IRequestHandler<WriteOffAccountsPayableCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;
    private readonly IAuditWriter _audit;
    private readonly SettlementOptions _options;

    public WriteOffAccountsPayableCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user,
        IAuditWriter audit,
        IOptions<SettlementOptions> options)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
        _audit = audit;
        _options = options.Value;
    }

    public async Task Handle(WriteOffAccountsPayableCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var amount = decimal.Round(request.Amount, 4, MidpointRounding.AwayFromZero);
        var max = _options.MaxWriteOffAmount;
        if (amount > max)
        {
            throw new ConflictAppException(
                $"Số tiền xóa nợ vượt trần stub ({max}). Không được xóa nợ lớn im lặng.");
        }

        var ap = await _db.AccountsPayable
            .FirstOrDefaultAsync(a => a.Id == request.AccountsPayableId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy khoản phải trả.");

        if (ap.RecordStatus != "active")
        {
            throw new ConflictAppException("Chỉ được xóa nợ khoản phải trả đang hiệu lực.");
        }

        var outstanding = ap.DeriveOutstanding();
        if (outstanding <= 0)
        {
            throw new ConflictAppException("Khoản phải trả không còn số dư để xóa nợ.");
        }

        if (amount > outstanding)
        {
            throw new ConflictAppException(
                $"Số tiền xóa nợ vượt số dư còn lại ({outstanding}) (C-008).");
        }

        var costCountBefore = await _db.Costs.CountAsync(cancellationToken);
        var beforeJson =
            $"{{\"outstanding\":{outstanding},\"adjustment\":{ap.AdjustmentAmount}}}";

        // Write-off reduces obligation (negative adjustment) — not fake cash settlement.
        ap.AdjustmentAmount = decimal.Round(ap.AdjustmentAmount - amount, 4, MidpointRounding.AwayFromZero);
        ap.SettlementStatus = SettlementHelpers.DeriveApArSettlementStatus(
            ap.RecognizedAmount, ap.AdjustmentAmount, ap.FinalizedSettledAmount);
        ap.UpdatedAt = DateTimeOffset.UtcNow;
        ap.UpdatedBy = _user.UserId;

        var reasonLine = $"[xóa nợ {amount}] {request.Reason.Trim()}";
        ap.Notes = string.IsNullOrWhiteSpace(ap.Notes)
            ? reasonLine
            : $"{ap.Notes}\n{reasonLine}";

        _audit.Append(
            AuditActions.AccountsPayableWriteOff,
            AuditObjectTypes.AccountsPayable,
            ap.Id,
            beforeJson: beforeJson,
            afterJson: $"{{\"writeOff\":{amount},\"outstanding\":{ap.DeriveOutstanding()},\"adjustment\":{ap.AdjustmentAmount}}}",
            reason: request.Reason.Trim());

        await _db.SaveChangesAsync(cancellationToken);

        var costCountAfter = await _db.Costs.CountAsync(cancellationToken);
        if (costCountAfter != costCountBefore)
        {
            throw new ConflictAppException("Xóa nợ phải trả không được tạo Chi phí mới (C-003).");
        }
    }
}
