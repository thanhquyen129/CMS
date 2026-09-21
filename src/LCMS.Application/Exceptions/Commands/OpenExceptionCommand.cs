using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.FinancialControl;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LCMS.Application.Exceptions.Commands;

public sealed record OpenExceptionCommand(
    string RuleCode,
    string Severity,
    string Title,
    string? Description,
    Guid? OwnerId,
    DateTimeOffset? DueAt,
    Guid? BillId,
    Guid? ReconciliationId,
    Guid? VarianceId,
    string? ObjectType,
    Guid? ObjectId) : IRequest<Guid>;

public sealed class OpenExceptionCommandValidator : AbstractValidator<OpenExceptionCommand>
{
    private static readonly HashSet<string> Severities = new(StringComparer.OrdinalIgnoreCase)
    {
        ExceptionSeverities.Low,
        ExceptionSeverities.Medium,
        ExceptionSeverities.High,
        ExceptionSeverities.Critical
    };

    private static readonly HashSet<string> ObjectTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ApprovalObjectTypes.Cost,
        ApprovalObjectTypes.Revenue,
        ApprovalObjectTypes.Document,
        ApprovalObjectTypes.Payment,
        ApprovalObjectTypes.Collection,
        ApprovalObjectTypes.Settlement,
        ApprovalObjectTypes.Variance,
        ApprovalObjectTypes.Exception,
        ApprovalObjectTypes.Other,
        ReconciliationObjectTypes.AccountsPayable,
        ReconciliationObjectTypes.AccountsReceivable
    };

    public OpenExceptionCommandValidator()
    {
        RuleFor(x => x.RuleCode)
            .NotEmpty().WithMessage("Mã quy tắc ngoại lệ không được để trống.")
            .MaximumLength(128).WithMessage("Mã quy tắc ngoại lệ tối đa 128 ký tự.");
        RuleFor(x => x.Severity)
            .NotEmpty().WithMessage("Mức độ nghiêm trọng không được để trống.")
            .Must(s => Severities.Contains(s.Trim()))
            .WithMessage("Mức độ nghiêm trọng không hợp lệ (low|medium|high|critical).");
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Tiêu đề ngoại lệ không được để trống.")
            .MaximumLength(256).WithMessage("Tiêu đề ngoại lệ tối đa 256 ký tự.");
        RuleFor(x => x.Description).MaximumLength(4096).When(x => x.Description is not null);
        RuleFor(x => x.ObjectType)
            .Must(t => t is null || ObjectTypes.Contains(t.Trim()))
            .WithMessage("Loại đối tượng ngoại lệ không hợp lệ.");
        RuleFor(x => x)
            .Must(x => !(x.ObjectId.HasValue ^ !string.IsNullOrWhiteSpace(x.ObjectType)))
            .WithMessage("Đối tượng ngoại lệ phải có đủ loại và mã, hoặc cả hai để trống.");
    }
}

/// <summary>
/// Opens an exception work item (severity/owner/SLA/object link). Does not invent Variance (Variance ≠ Exception).
/// </summary>
public sealed class OpenExceptionCommandHandler : IRequestHandler<OpenExceptionCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly FinancialControlOptions _options;
    private readonly IOperatorNotificationPublisher _notifications;

    public OpenExceptionCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IOptions<FinancialControlOptions> options,
        IOperatorNotificationPublisher notifications)
    {
        _db = db;
        _tenantContext = tenantContext;
        _options = options.Value;
        _notifications = notifications;
    }

    public async Task<Guid> Handle(OpenExceptionCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;
        var severity = request.Severity.Trim().ToLowerInvariant();

        if (request.BillId.HasValue)
        {
            var billExists = await _db.Bills.AsNoTracking().AnyAsync(b => b.Id == request.BillId, cancellationToken);
            if (!billExists)
            {
                throw new NotFoundAppException("Không tìm thấy Bill.");
            }
        }

        if (request.ReconciliationId.HasValue)
        {
            var reconExists = await _db.Reconciliations.AsNoTracking()
                .AnyAsync(r => r.Id == request.ReconciliationId, cancellationToken);
            if (!reconExists)
            {
                throw new NotFoundAppException("Không tìm thấy phiên đối soát.");
            }
        }

        Variance? variance = null;
        if (request.VarianceId.HasValue)
        {
            variance = await _db.Variances
                .FirstOrDefaultAsync(v => v.Id == request.VarianceId, cancellationToken)
                ?? throw new NotFoundAppException("Không tìm thấy chênh lệch.");
        }

        string? objectType = null;
        Guid? objectId = null;
        if (!string.IsNullOrWhiteSpace(request.ObjectType) && request.ObjectId.HasValue)
        {
            objectType = request.ObjectType.Trim().ToLowerInvariant();
            objectId = request.ObjectId;
            await EnsureObjectExistsAsync(objectType, objectId.Value, cancellationToken);
        }

        var dueAt = request.DueAt;
        if (dueAt is null
            && _options.DefaultExceptionSlaHours.TryGetValue(severity, out var hours)
            && hours > 0)
        {
            dueAt = DateTimeOffset.UtcNow.AddHours(hours);
        }

        var varianceCountBefore = await _db.Variances.CountAsync(cancellationToken);

        var entity = new FinancialException
        {
            TenantId = tenantId,
            RuleCode = request.RuleCode.Trim(),
            Severity = severity,
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            OwnerId = request.OwnerId,
            DueAt = dueAt,
            BillId = request.BillId,
            ReconciliationId = request.ReconciliationId,
            VarianceId = request.VarianceId,
            ObjectType = objectType,
            ObjectId = objectId,
            Status = ExceptionStatuses.Open
        };

        _db.Exceptions.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        if (variance is not null)
        {
            variance.ExceptionId = entity.Id;
            await _db.SaveChangesAsync(cancellationToken);
        }

        var varianceCountAfter = await _db.Variances.CountAsync(cancellationToken);
        if (varianceCountAfter != varianceCountBefore)
        {
            throw new ConflictAppException(
                "Mở Ngoại lệ không được tạo Chênh lệch mới — Chênh lệch ≠ Ngoại lệ.");
        }

        await _notifications.PublishAsync(
            new NotificationPublishRequest(
                NotificationEventTypes.ExceptionOpened,
                "Ngoại lệ tài chính mở",
                entity.Title,
                "/queues/exceptions",
                "exception",
                entity.Id),
            cancellationToken);

        return entity.Id;
    }

    private async Task EnsureObjectExistsAsync(string objectType, Guid objectId, CancellationToken cancellationToken)
    {
        var exists = objectType switch
        {
            ApprovalObjectTypes.Cost => await _db.Costs.AsNoTracking().AnyAsync(x => x.Id == objectId, cancellationToken),
            ApprovalObjectTypes.Revenue => await _db.Revenues.AsNoTracking().AnyAsync(x => x.Id == objectId, cancellationToken),
            ApprovalObjectTypes.Document => await _db.FinancialDocuments.AsNoTracking().AnyAsync(x => x.Id == objectId, cancellationToken),
            ApprovalObjectTypes.Payment => await _db.Payments.AsNoTracking().AnyAsync(x => x.Id == objectId, cancellationToken),
            ApprovalObjectTypes.Collection => await _db.Collections.AsNoTracking().AnyAsync(x => x.Id == objectId, cancellationToken),
            ApprovalObjectTypes.Variance => await _db.Variances.AsNoTracking().AnyAsync(x => x.Id == objectId, cancellationToken),
            ReconciliationObjectTypes.AccountsPayable => await _db.AccountsPayable.AsNoTracking().AnyAsync(x => x.Id == objectId, cancellationToken),
            ReconciliationObjectTypes.AccountsReceivable => await _db.AccountsReceivable.AsNoTracking().AnyAsync(x => x.Id == objectId, cancellationToken),
            ApprovalObjectTypes.Settlement or ApprovalObjectTypes.Other or ApprovalObjectTypes.Exception => true,
            _ => false
        };

        if (!exists)
        {
            throw new NotFoundAppException("Không tìm thấy đối tượng gắn ngoại lệ.");
        }
    }
}
