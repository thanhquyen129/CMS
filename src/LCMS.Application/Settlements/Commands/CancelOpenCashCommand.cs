using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Settlements.Commands;

public sealed record CancelPaymentCommand(Guid PaymentId, string Reason) : IRequest;

public sealed class CancelPaymentCommandValidator : AbstractValidator<CancelPaymentCommand>
{
    public CancelPaymentCommandValidator()
    {
        RuleFor(x => x.PaymentId).NotEmpty().WithMessage("Thanh toán không hợp lệ.");
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Lý do hủy thanh toán không được để trống.")
            .MaximumLength(512).WithMessage("Lý do hủy thanh toán không được vượt quá 512 ký tự.");
    }
}

/// <summary>
/// Cancels an open payment. Finalized allocations must be reversed first.
/// Draft allocations are reversed in the same save and do not change AP outstanding.
/// </summary>
public sealed class CancelPaymentCommandHandler : IRequestHandler<CancelPaymentCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;
    private readonly IAuditWriter _audit;

    public CancelPaymentCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user,
        IAuditWriter audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
        _audit = audit;
    }

    public async Task Handle(CancelPaymentCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var payment = await _db.Payments.FirstOrDefaultAsync(p => p.Id == request.PaymentId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy thanh toán.");
        if (!string.Equals(payment.Status, PaymentStatuses.Open, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Chỉ hủy thanh toán đang mở.");
        }

        var allocations = await _db.PaymentAllocations
            .Where(a => a.PaymentId == payment.Id)
            .ToListAsync(cancellationToken);
        if (allocations.Any(a => string.Equals(a.AllocationStatus, SettlementAllocationStatuses.Finalized, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ConflictAppException("Phải đảo phân bổ đã chốt trước khi hủy thanh toán.");
        }

        var reason = request.Reason.Trim();
        var now = DateTimeOffset.UtcNow;
        foreach (var draft in allocations.Where(a =>
                     string.Equals(a.AllocationStatus, SettlementAllocationStatuses.Draft, StringComparison.OrdinalIgnoreCase)))
        {
            draft.AllocationStatus = SettlementAllocationStatuses.Reversed;
            draft.ReversedAt = now;
            draft.ReversedBy = _user.UserId;
            draft.ReverseReason = reason;
        }

        var before = payment.Status;
        payment.Status = PaymentStatuses.Cancelled;
        _audit.Append(
            AuditActions.PaymentCancel,
            AuditObjectTypes.Payment,
            payment.Id,
            before,
            payment.Status,
            reason);
        await _db.SaveChangesAsync(cancellationToken);
    }
}

public sealed record CancelCollectionCommand(Guid CollectionId, string Reason) : IRequest;

public sealed class CancelCollectionCommandValidator : AbstractValidator<CancelCollectionCommand>
{
    public CancelCollectionCommandValidator()
    {
        RuleFor(x => x.CollectionId).NotEmpty().WithMessage("Phiếu thu không hợp lệ.");
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Lý do hủy phiếu thu không được để trống.")
            .MaximumLength(512).WithMessage("Lý do hủy phiếu thu không được vượt quá 512 ký tự.");
    }
}

/// <summary>
/// Cancels an open collection. Finalized allocations must be reversed first.
/// Draft allocations are reversed in the same save and do not change AR outstanding.
/// </summary>
public sealed class CancelCollectionCommandHandler : IRequestHandler<CancelCollectionCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;
    private readonly IAuditWriter _audit;

    public CancelCollectionCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user,
        IAuditWriter audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
        _audit = audit;
    }

    public async Task Handle(CancelCollectionCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var collection = await _db.Collections.FirstOrDefaultAsync(c => c.Id == request.CollectionId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy phiếu thu.");
        if (!string.Equals(collection.Status, CollectionStatuses.Open, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Chỉ hủy phiếu thu đang mở.");
        }

        var allocations = await _db.CollectionAllocations
            .Where(a => a.CollectionId == collection.Id)
            .ToListAsync(cancellationToken);
        if (allocations.Any(a => string.Equals(a.AllocationStatus, SettlementAllocationStatuses.Finalized, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ConflictAppException("Phải đảo phân bổ đã chốt trước khi hủy phiếu thu.");
        }

        var reason = request.Reason.Trim();
        var now = DateTimeOffset.UtcNow;
        foreach (var draft in allocations.Where(a =>
                     string.Equals(a.AllocationStatus, SettlementAllocationStatuses.Draft, StringComparison.OrdinalIgnoreCase)))
        {
            draft.AllocationStatus = SettlementAllocationStatuses.Reversed;
            draft.ReversedAt = now;
            draft.ReversedBy = _user.UserId;
            draft.ReverseReason = reason;
        }

        var before = collection.Status;
        collection.Status = CollectionStatuses.Cancelled;
        _audit.Append(
            AuditActions.CollectionCancel,
            AuditObjectTypes.Collection,
            collection.Id,
            before,
            collection.Status,
            reason);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
