using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Exceptions.Commands;

public sealed record ResolveExceptionCommand(Guid ExceptionId, string? ResolutionNotes) : IRequest;

public sealed class ResolveExceptionCommandValidator : AbstractValidator<ResolveExceptionCommand>
{
    public ResolveExceptionCommandValidator()
    {
        RuleFor(x => x.ExceptionId).NotEmpty().WithMessage("Ngoại lệ không hợp lệ.");
        RuleFor(x => x.ResolutionNotes).MaximumLength(2048).When(x => x.ResolutionNotes is not null);
    }
}

/// <summary>Resolve stub — marks exception resolved; does not hard-delete or mutate Variance.</summary>
public sealed class ResolveExceptionCommandHandler : IRequestHandler<ResolveExceptionCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;

    public ResolveExceptionCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
    }

    public async Task Handle(ResolveExceptionCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var entity = await _db.Exceptions
            .FirstOrDefaultAsync(e => e.Id == request.ExceptionId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy ngoại lệ.");

        if (string.Equals(entity.Status, ExceptionStatuses.Closed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(entity.Status, ExceptionStatuses.Cancelled, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Không thể xử lý ngoại lệ đã đóng hoặc đã hủy.");
        }

        if (string.Equals(entity.Status, ExceptionStatuses.Resolved, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        entity.Status = ExceptionStatuses.Resolved;
        entity.ResolvedAt = DateTimeOffset.UtcNow;
        entity.ResolvedBy = _user.UserId;
        entity.ResolutionNotes = string.IsNullOrWhiteSpace(request.ResolutionNotes)
            ? null
            : request.ResolutionNotes.Trim();

        await _db.SaveChangesAsync(cancellationToken);
    }
}

public sealed record CloseExceptionCommand(Guid ExceptionId) : IRequest;

public sealed class CloseExceptionCommandValidator : AbstractValidator<CloseExceptionCommand>
{
    public CloseExceptionCommandValidator()
    {
        RuleFor(x => x.ExceptionId).NotEmpty().WithMessage("Ngoại lệ không hợp lệ.");
    }
}

/// <summary>Close stub — closes resolved (or open) exception without hard delete.</summary>
public sealed class CloseExceptionCommandHandler : IRequestHandler<CloseExceptionCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;

    public CloseExceptionCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
    }

    public async Task Handle(CloseExceptionCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var entity = await _db.Exceptions
            .FirstOrDefaultAsync(e => e.Id == request.ExceptionId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy ngoại lệ.");

        if (string.Equals(entity.Status, ExceptionStatuses.Closed, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (string.Equals(entity.Status, ExceptionStatuses.Cancelled, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Không thể đóng ngoại lệ đã hủy.");
        }

        entity.Status = ExceptionStatuses.Closed;
        entity.ClosedAt = DateTimeOffset.UtcNow;
        entity.ClosedBy = _user.UserId;
        await _db.SaveChangesAsync(cancellationToken);
    }
}

public sealed record EscalateExceptionCommand(Guid ExceptionId, string? EscalationReason) : IRequest;

public sealed class EscalateExceptionCommandValidator : AbstractValidator<EscalateExceptionCommand>
{
    public EscalateExceptionCommandValidator()
    {
        RuleFor(x => x.ExceptionId).NotEmpty().WithMessage("Ngoại lệ không hợp lệ.");
        RuleFor(x => x.EscalationReason)
            .NotEmpty().WithMessage("Lý do leo thang ngoại lệ không được để trống.")
            .MaximumLength(2048).WithMessage("Lý do leo thang tối đa 2048 ký tự.");
    }
}

/// <summary>
/// Escalate stub — marks exception escalated (SLA inbox); does not invent Variance or change permissions.
/// </summary>
public sealed class EscalateExceptionCommandHandler : IRequestHandler<EscalateExceptionCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;

    public EscalateExceptionCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
    }

    public async Task Handle(EscalateExceptionCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var entity = await _db.Exceptions
            .FirstOrDefaultAsync(e => e.Id == request.ExceptionId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy ngoại lệ.");

        if (string.Equals(entity.Status, ExceptionStatuses.Closed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(entity.Status, ExceptionStatuses.Cancelled, StringComparison.OrdinalIgnoreCase)
            || string.Equals(entity.Status, ExceptionStatuses.Resolved, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Không thể leo thang ngoại lệ đã xử lý, đã đóng hoặc đã hủy.");
        }

        if (string.Equals(entity.Status, ExceptionStatuses.Escalated, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        entity.Status = ExceptionStatuses.Escalated;
        entity.EscalatedAt = DateTimeOffset.UtcNow;
        entity.EscalatedBy = _user.UserId;
        entity.EscalationReason = request.EscalationReason!.Trim();

        // Stub: bump severity one step when escalating (cap at critical).
        entity.Severity = entity.Severity switch
        {
            ExceptionSeverities.Low => ExceptionSeverities.Medium,
            ExceptionSeverities.Medium => ExceptionSeverities.High,
            ExceptionSeverities.High => ExceptionSeverities.Critical,
            _ => ExceptionSeverities.Critical
        };

        await _db.SaveChangesAsync(cancellationToken);
    }
}
