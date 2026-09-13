using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Approvals;
using LCMS.Application.Audit;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Tenancy;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LCMS.Application.Settlements.Commands;

/// <summary>
/// Result of write-off request: applied now, or queued for approval when over threshold (P03 / ADR-0008).
/// </summary>
public sealed record WriteOffResult(bool AppliedImmediately, Guid? ApprovalId, int? RequiredLevel = null);

/// <summary>
/// AP write-off: ≤ MaxWriteOffAmount apply immediately; above → Approval then apply on approve.
/// Never invents Cost; does not fake settlement cash.
/// </summary>
public sealed record WriteOffAccountsPayableCommand(
    Guid AccountsPayableId,
    decimal Amount,
    string Reason) : IRequest<WriteOffResult>;

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

public sealed class WriteOffAccountsPayableCommandHandler
    : IRequestHandler<WriteOffAccountsPayableCommand, WriteOffResult>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;
    private readonly IAuditWriter _audit;
    private readonly SettlementOptions _options;
    private readonly ApprovalMatrixOptions _matrix;
    private readonly TenantFinancialOptionsResolver _financial;
    private readonly IPermissionService _permissions;

    public WriteOffAccountsPayableCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user,
        IAuditWriter audit,
        IOptions<SettlementOptions> options,
        IOptions<ApprovalMatrixOptions> matrix,
        TenantFinancialOptionsResolver financial,
        IPermissionService permissions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
        _audit = audit;
        _options = options.Value;
        _matrix = matrix.Value;
        _financial = financial;
        _permissions = permissions;
    }

    public async Task<WriteOffResult> Handle(
        WriteOffAccountsPayableCommand request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(
            PermissionCodes.ApWriteOff,
            "Bạn không có quyền xóa nợ phải trả.",
            cancellationToken);

        var amount = decimal.Round(request.Amount, 4, MidpointRounding.AwayFromZero);
        var reason = request.Reason.Trim();
        var maxImmediate = await _financial.GetMaxWriteOffAmountAsync(cancellationToken);
        var requiredLevel = ApprovalMatrixResolver.ResolveRequiredLevel(
            _matrix, ApprovalObjectTypes.AccountsPayable, amount);

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

        if (amount <= maxImmediate)
        {
            await WriteOffApplier.ApplyPayableAsync(
                _db, _audit, _user, ap, amount, reason, cancellationToken);
            return new WriteOffResult(true, null);
        }

        // Over threshold → Approval gate (do not apply yet).
        var objectType = ApprovalObjectTypes.AccountsPayable;
        var pendingExists = await _db.Approvals.AsNoTracking()
            .AnyAsync(
                a => a.ObjectType == objectType
                     && a.ObjectId == ap.Id
                     && a.Status == ApprovalStatuses.Pending,
                cancellationToken);
        if (pendingExists)
        {
            throw new ConflictAppException(
                "Khoản phải trả đã có yêu cầu xóa nợ đang chờ phê duyệt.");
        }

        var payload = new WriteOffApplier.PendingPayload(amount, reason, ap.CurrencyCode);
        var approval = new Approval
        {
            TenantId = _tenantContext.TenantId!.Value,
            ObjectType = objectType,
            ObjectId = ap.Id,
            Status = ApprovalStatuses.Pending,
            RequiredLevel = requiredLevel,
            CurrentLevel = 0,
            RequestedBy = _user.UserId,
            RequestedAt = DateTimeOffset.UtcNow,
            RequestReason =
                $"Xóa nợ {amount} {ap.CurrencyCode} (vượt trần {maxImmediate}; cấp {requiredLevel}): {reason}",
            Notes = WriteOffApplier.EncodePendingNotes(payload)
        };
        _db.Approvals.Add(approval);
        await _db.SaveChangesAsync(cancellationToken);
        return new WriteOffResult(false, approval.Id, requiredLevel);
    }
}
