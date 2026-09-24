using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Approvals;
using LCMS.Application.Audit;
using LCMS.Application.Common;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Tenancy;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LCMS.Application.Settlements.Commands;

/// <summary>
/// AR write-off: ≤ MaxWriteOffAmount (tenant override) apply immediately; above → Approval matrix.
/// </summary>
public sealed record WriteOffAccountsReceivableCommand(
    Guid AccountsReceivableId,
    decimal Amount,
    string Reason,
    string? IfMatch = null) : IRequest<WriteOffResult>;

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
    : IRequestHandler<WriteOffAccountsReceivableCommand, WriteOffResult>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;
    private readonly IAuditWriter _audit;
    private readonly ApprovalMatrixOptions _matrix;
    private readonly TenantFinancialOptionsResolver _financial;
    private readonly IPermissionService _permissions;
    private readonly IRowVersionGuard _versions;

    public WriteOffAccountsReceivableCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user,
        IAuditWriter audit,
        IOptions<ApprovalMatrixOptions> matrix,
        TenantFinancialOptionsResolver financial,
        IPermissionService permissions,
        IRowVersionGuard versions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
        _audit = audit;
        _matrix = matrix.Value;
        _financial = financial;
        _permissions = permissions;
        _versions = versions;
    }

    public async Task<WriteOffResult> Handle(
        WriteOffAccountsReceivableCommand request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(
            PermissionCodes.ArWriteOff,
            "Bạn không có quyền xóa nợ phải thu.",
            cancellationToken);

        var amount = decimal.Round(request.Amount, 4, MidpointRounding.AwayFromZero);
        var reason = request.Reason.Trim();
        var maxImmediate = await _financial.GetMaxWriteOffAmountAsync(cancellationToken);
        var requiredLevel = ApprovalMatrixResolver.ResolveRequiredLevel(
            _matrix, ApprovalObjectTypes.AccountsReceivable, amount);

        var ar = await _db.AccountsReceivable
            .FirstOrDefaultAsync(a => a.Id == request.AccountsReceivableId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy khoản phải thu.");
        _versions.EnsureCurrent(ar, request.IfMatch);

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

        if (amount <= maxImmediate)
        {
            await WriteOffApplier.ApplyReceivableAsync(
                _db, _audit, _user, ar, amount, reason, cancellationToken);
            return new WriteOffResult(true, null);
        }

        var objectType = ApprovalObjectTypes.AccountsReceivable;
        var pendingExists = await _db.Approvals.AsNoTracking()
            .AnyAsync(
                a => a.ObjectType == objectType
                     && a.ObjectId == ar.Id
                     && a.Status == ApprovalStatuses.Pending,
                cancellationToken);
        if (pendingExists)
        {
            throw new ConflictAppException(
                "Khoản phải thu đã có yêu cầu xóa nợ đang chờ phê duyệt.");
        }

        var payload = new WriteOffApplier.PendingPayload(amount, reason, ar.CurrencyCode);
        var approval = new Approval
        {
            TenantId = _tenantContext.TenantId!.Value,
            ObjectType = objectType,
            ObjectId = ar.Id,
            Status = ApprovalStatuses.Pending,
            RequiredLevel = requiredLevel,
            CurrentLevel = 0,
            RequestedBy = _user.UserId,
            RequestedAt = DateTimeOffset.UtcNow,
            RequestReason =
                $"Xóa nợ {amount} {ar.CurrencyCode} (vượt trần {maxImmediate}; cấp {requiredLevel}): {reason}",
            Notes = WriteOffApplier.EncodePendingNotes(payload)
        };
        _db.Approvals.Add(approval);
        await _db.SaveChangesAsync(cancellationToken);
        return new WriteOffResult(false, approval.Id, requiredLevel);
    }
}
