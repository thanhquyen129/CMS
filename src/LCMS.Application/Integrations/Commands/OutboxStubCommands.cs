using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Integrations.Commands;

/// <summary>
/// Local outbox stub: enqueue a message for later process-once. No broker / no distributed topology.
/// </summary>
public sealed record EnqueueOutboxMessageCommand(
    string Topic,
    string PayloadJson) : IRequest<Guid>;

public sealed class EnqueueOutboxMessageCommandValidator : AbstractValidator<EnqueueOutboxMessageCommand>
{
    public EnqueueOutboxMessageCommandValidator()
    {
        RuleFor(x => x.Topic)
            .NotEmpty().WithMessage("Chủ đề outbox không được để trống.")
            .MaximumLength(128);
        RuleFor(x => x.PayloadJson)
            .NotEmpty().WithMessage("Payload outbox không được để trống.");
    }
}

public sealed class EnqueueOutboxMessageCommandHandler : IRequestHandler<EnqueueOutboxMessageCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public EnqueueOutboxMessageCommandHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<Guid> Handle(EnqueueOutboxMessageCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var message = new OutboxMessage
        {
            TenantId = _tenantContext.TenantId!.Value,
            Topic = request.Topic.Trim(),
            PayloadJson = request.PayloadJson.Trim(),
            Status = OutboxMessageStatuses.Pending,
            EnqueuedAt = DateTimeOffset.UtcNow
        };

        _db.OutboxMessages.Add(message);
        await _db.SaveChangesAsync(cancellationToken);
        return message.Id;
    }
}

/// <summary>
/// Process the oldest pending outbox row once (in-process stub). Marks processed; no external publish.
/// </summary>
public sealed record ProcessOutboxOnceCommand : IRequest<ProcessOutboxOnceResult>;

public sealed record ProcessOutboxOnceResult(
    bool Processed,
    Guid? MessageId,
    string? Topic,
    string? Status);

public sealed class ProcessOutboxOnceCommandHandler
    : IRequestHandler<ProcessOutboxOnceCommand, ProcessOutboxOnceResult>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ProcessOutboxOnceCommandHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<ProcessOutboxOnceResult> Handle(
        ProcessOutboxOnceCommand request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var message = await _db.OutboxMessages
            .Where(m => m.Status == OutboxMessageStatuses.Pending)
            .OrderBy(m => m.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (message is null)
        {
            return new ProcessOutboxOnceResult(false, null, null, null);
        }

        message.AttemptNo += 1;
        message.Status = OutboxMessageStatuses.Processed;
        message.ProcessedAt = DateTimeOffset.UtcNow;
        message.LastError = null;
        await _db.SaveChangesAsync(cancellationToken);

        return new ProcessOutboxOnceResult(true, message.Id, message.Topic, message.Status);
    }
}

public sealed record OutboxMessageDto(
    Guid Id,
    string Topic,
    string PayloadJson,
    string Status,
    int AttemptNo,
    string? LastError,
    DateTimeOffset EnqueuedAt,
    DateTimeOffset? ProcessedAt);

public sealed record ListOutboxMessagesQuery(
    string? Status = null,
    int Take = 100) : IRequest<IReadOnlyList<OutboxMessageDto>>;

public sealed class ListOutboxMessagesQueryHandler
    : IRequestHandler<ListOutboxMessagesQuery, IReadOnlyList<OutboxMessageDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ListOutboxMessagesQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<OutboxMessageDto>> Handle(
        ListOutboxMessagesQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var take = Math.Clamp(request.Take, 1, 500);
        var query = _db.OutboxMessages.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim().ToLowerInvariant();
            query = query.Where(m => m.Status == status);
        }

        return await query
            .OrderByDescending(m => m.Id)
            .Take(take)
            .Select(m => new OutboxMessageDto(
                m.Id,
                m.Topic,
                m.PayloadJson,
                m.Status,
                m.AttemptNo,
                m.LastError,
                m.EnqueuedAt,
                m.ProcessedAt))
            .ToListAsync(cancellationToken);
    }
}
