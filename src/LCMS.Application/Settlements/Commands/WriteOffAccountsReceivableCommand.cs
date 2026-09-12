using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Audit;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LCMS.Application.Settlements.Commands;

/// <summary>
/// Small-remainder AR write-off stub: reduces obligation via AdjustmentAmount + required reason note.
/// Never silently wipes outstanding; never invents Revenue (C-004); does not fake collection cash.
/// </summary>
public sealed record WriteOffAccountsReceivableCommand(
    Guid AccountsReceivableId,
    decimal Amount,
    string Reason) : IRequest;

public sealed class WriteOffAccountsReceivableCommandValidator
    : AbstractValidator<WriteOffAccountsReceivableCommand>
{
    public WriteOffAccountsReceivableCommandValidator()
    {
        RuleFor(x => x.AccountsReceivableId).NotEmpty().WithMessage("Khoản phải thu không hợp lệ.");
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Số tiền xóa nợ phải lớn hơn 0.");
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Lý do xóa nợ không được để trống.")
            .MaximumLength(1024).WithMessage("Lý do xóa nợ không được vượt quá 1024 ký tự.");
    }
}

public sealed class WriteOffAccountsReceivableCommandHandler
    : IRequestHandler<WriteOffAccountsReceivableCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;
    private readonly IAuditWriter _audit;
    private readonly SettlementOptions _options;

    public WriteOffAccountsReceivableCommandHandler(
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

    public async Task Handle(WriteOffAccountsReceivableCommand request, CancellationToken cancellationToken)
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

        var ar = await _db.AccountsReceivable
            .FirstOrDefaultAsync(a => a.Id == request.AccountsReceivableId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy khoản phải thu.");

        if (ar.RecordStatus != "active")
        {
            throw new ConflictAppException("Chỉ được xóa nợ khoản phải thu đang hiệu lực.");
        }

        var outstanding = ar.DeriveOutstanding();
        if (outstanding <= 0)
        {
            throw new ConflictAppException("Khoản phải thu không còn số dư để xóa nợ.");
        }

        if (amount > outstanding)
        {
            throw new ConflictAppException(
                $"Số tiền xóa nợ vượt số dư còn lại ({outstanding}) (C-008).");
        }

        var revenueCountBefore = await _db.Revenues.CountAsync(cancellationToken);
        var beforeJson = AuditJson.Serialize(new
        {
            id = ar.Id,
            billId = ar.BillId,
            recognized = ar.RecognizedAmount,
            adjustment = ar.AdjustmentAmount,
            settled = ar.FinalizedSettledAmount,
            outstanding,
            settlementStatus = ar.SettlementStatus,
            currency = ar.CurrencyCode
        });

        ar.AdjustmentAmount = decimal.Round(ar.AdjustmentAmount - amount, 4, MidpointRounding.AwayFromZero);
        ar.SettlementStatus = SettlementHelpers.DeriveApArSettlementStatus(
            ar.RecognizedAmount, ar.AdjustmentAmount, ar.FinalizedSettledAmount);
        ar.UpdatedAt = DateTimeOffset.UtcNow;
        ar.UpdatedBy = _user.UserId;

        var reasonLine = $"[xóa nợ {amount}] {request.Reason.Trim()}";
        ar.Notes = string.IsNullOrWhiteSpace(ar.Notes)
            ? reasonLine
            : $"{ar.Notes}\n{reasonLine}";

        _audit.Append(
            AuditActions.AccountsReceivableWriteOff,
            AuditObjectTypes.AccountsReceivable,
            ar.Id,
            beforeJson: beforeJson,
            afterJson: AuditJson.Serialize(new
            {
                id = ar.Id,
                billId = ar.BillId,
                writeOff = amount,
                recognized = ar.RecognizedAmount,
                adjustment = ar.AdjustmentAmount,
                settled = ar.FinalizedSettledAmount,
                outstanding = ar.DeriveOutstanding(),
                settlementStatus = ar.SettlementStatus,
                currency = ar.CurrencyCode
            }),
            reason: request.Reason.Trim());

        await _db.SaveChangesAsync(cancellationToken);

        var revenueCountAfter = await _db.Revenues.CountAsync(cancellationToken);
        if (revenueCountAfter != revenueCountBefore)
        {
            throw new ConflictAppException("Xóa nợ phải thu không được tạo Doanh thu mới (C-004).");
        }
    }
}
