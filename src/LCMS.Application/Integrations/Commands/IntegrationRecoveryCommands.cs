using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Integrations.Commands;

public sealed record RecordIntegrationErrorCommand(
    Guid IntegrationRecordId,
    string ErrorCode,
    string Message,
    string? Detail,
    DateTimeOffset? NextRetryAt) : IRequest<Guid>;

public sealed class RecordIntegrationErrorCommandValidator : AbstractValidator<RecordIntegrationErrorCommand>
{
    public RecordIntegrationErrorCommandValidator()
    {
        RuleFor(x => x.IntegrationRecordId).NotEmpty().WithMessage("Bản ghi tích hợp không hợp lệ.");
        RuleFor(x => x.ErrorCode)
            .NotEmpty().WithMessage("Mã lỗi tích hợp không được để trống.")
            .MaximumLength(64);
        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("Thông báo lỗi tích hợp không được để trống.")
            .MaximumLength(2048);
        RuleFor(x => x.Detail).MaximumLength(8192).When(x => x.Detail is not null);
    }
}

public sealed class RecordIntegrationErrorCommandHandler : IRequestHandler<RecordIntegrationErrorCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IOperatorNotificationPublisher _notifications;

    public RecordIntegrationErrorCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IOperatorNotificationPublisher notifications)
    {
        _db = db;
        _tenantContext = tenantContext;
        _notifications = notifications;
    }

    public async Task<Guid> Handle(RecordIntegrationErrorCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;
        var record = await _db.IntegrationRecords
            .FirstOrDefaultAsync(r => r.Id == request.IntegrationRecordId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy bản ghi tích hợp.");

        var attemptNo = await _db.IntegrationErrors
            .Where(e => e.IntegrationRecordId == record.Id)
            .Select(e => (int?)e.AttemptNo)
            .MaxAsync(cancellationToken) ?? 0;

        var error = new IntegrationError
        {
            TenantId = tenantId,
            IntegrationRecordId = record.Id,
            ErrorCode = request.ErrorCode.Trim(),
            Message = request.Message.Trim(),
            Detail = string.IsNullOrWhiteSpace(request.Detail) ? null : request.Detail.Trim(),
            AttemptNo = attemptNo + 1,
            OccurredAt = DateTimeOffset.UtcNow,
            NextRetryAt = request.NextRetryAt,
            RecoveryStatus = IntegrationErrorRecoveryStatuses.Pending
        };

        record.Status = IntegrationRecordStatuses.Failed;
        record.UpdatedAt = DateTimeOffset.UtcNow;

        _db.IntegrationErrors.Add(error);
        await _db.SaveChangesAsync(cancellationToken);
        await _notifications.PublishAsync(
            new NotificationPublishRequest(
                NotificationEventTypes.IntegrationError,
                "Lỗi tích hợp",
                error.Message,
                "/integration-errors",
                "integration_error",
                error.Id),
            cancellationToken);
        return error.Id;
    }
}

public sealed record MarkIntegrationErrorRetriedCommand(
    Guid IntegrationErrorId,
    string? Note) : IRequest;

public sealed class MarkIntegrationErrorRetriedCommandValidator
    : AbstractValidator<MarkIntegrationErrorRetriedCommand>
{
    public MarkIntegrationErrorRetriedCommandValidator()
    {
        RuleFor(x => x.IntegrationErrorId).NotEmpty().WithMessage("Lỗi tích hợp không hợp lệ.");
        RuleFor(x => x.Note).MaximumLength(2048).When(x => x.Note is not null);
    }
}

public sealed class MarkIntegrationErrorRetriedCommandHandler
    : IRequestHandler<MarkIntegrationErrorRetriedCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;

    public MarkIntegrationErrorRetriedCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
    }

    public async Task Handle(MarkIntegrationErrorRetriedCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var error = await _db.IntegrationErrors
            .FirstOrDefaultAsync(e => e.Id == request.IntegrationErrorId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy lỗi tích hợp.");

        if (string.Equals(error.RecoveryStatus, IntegrationErrorRecoveryStatuses.DeadLetter, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Lỗi đã vào dead-letter; không thể đánh dấu thử lại.");
        }

        if (string.Equals(error.RecoveryStatus, IntegrationErrorRecoveryStatuses.Retried, StringComparison.OrdinalIgnoreCase))
        {
            return; // idempotent
        }

        var record = await _db.IntegrationRecords
            .FirstOrDefaultAsync(r => r.Id == error.IntegrationRecordId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy bản ghi tích hợp.");

        var now = DateTimeOffset.UtcNow;
        error.RecoveryStatus = IntegrationErrorRecoveryStatuses.Retried;
        error.RecoveredAt = now;
        error.RecoveredBy = _user.UserId;
        error.RecoveryNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        error.NextRetryAt = null;

        if (string.Equals(record.Status, IntegrationRecordStatuses.Failed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(record.Status, IntegrationRecordStatuses.DeadLetter, StringComparison.OrdinalIgnoreCase))
        {
            record.Status = IntegrationRecordStatuses.Accepted;
            record.UpdatedAt = now;
            record.UpdatedBy = _user.UserId;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}

public sealed record DeadLetterIntegrationErrorCommand(
    Guid IntegrationErrorId,
    string? Note) : IRequest;

public sealed class DeadLetterIntegrationErrorCommandValidator
    : AbstractValidator<DeadLetterIntegrationErrorCommand>
{
    public DeadLetterIntegrationErrorCommandValidator()
    {
        RuleFor(x => x.IntegrationErrorId).NotEmpty().WithMessage("Lỗi tích hợp không hợp lệ.");
        RuleFor(x => x.Note).MaximumLength(2048).When(x => x.Note is not null);
    }
}

public sealed class DeadLetterIntegrationErrorCommandHandler
    : IRequestHandler<DeadLetterIntegrationErrorCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;

    public DeadLetterIntegrationErrorCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
    }

    public async Task Handle(DeadLetterIntegrationErrorCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var error = await _db.IntegrationErrors
            .FirstOrDefaultAsync(e => e.Id == request.IntegrationErrorId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy lỗi tích hợp.");

        if (string.Equals(error.RecoveryStatus, IntegrationErrorRecoveryStatuses.DeadLetter, StringComparison.OrdinalIgnoreCase))
        {
            return; // idempotent
        }

        var record = await _db.IntegrationRecords
            .FirstOrDefaultAsync(r => r.Id == error.IntegrationRecordId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy bản ghi tích hợp.");

        var now = DateTimeOffset.UtcNow;
        error.RecoveryStatus = IntegrationErrorRecoveryStatuses.DeadLetter;
        error.RecoveredAt = now;
        error.RecoveredBy = _user.UserId;
        error.RecoveryNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        error.NextRetryAt = null;

        record.Status = IntegrationRecordStatuses.DeadLetter;
        record.UpdatedAt = now;
        record.UpdatedBy = _user.UserId;

        await _db.SaveChangesAsync(cancellationToken);
    }
}
