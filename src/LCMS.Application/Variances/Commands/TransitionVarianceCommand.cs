using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Variances.Commands;

public sealed record TransitionVarianceCommand(
    Guid VarianceId,
    string TargetStatus,
    string? Explanation = null) : IRequest;

public sealed class TransitionVarianceCommandValidator : AbstractValidator<TransitionVarianceCommand>
{
    public TransitionVarianceCommandValidator()
    {
        RuleFor(x => x.VarianceId).NotEmpty().WithMessage("Chênh lệch không hợp lệ.");
        RuleFor(x => x.TargetStatus)
            .NotEmpty()
            .Must(s =>
                s is VarianceStatuses.Accepted or VarianceStatuses.Cleared or VarianceStatuses.WrittenOff)
            .WithMessage("Trạng thái đích phải là accepted, cleared hoặc written_off.");
        RuleFor(x => x.Explanation).MaximumLength(2048).When(x => x.Explanation is not null);
    }
}

/// <summary>
/// Transition open variance → accepted | cleared | written_off.
/// Does not auto-open Exception (Variance ≠ Exception).
/// </summary>
public sealed class TransitionVarianceCommandHandler : IRequestHandler<TransitionVarianceCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public TransitionVarianceCommandHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task Handle(TransitionVarianceCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var entity = await _db.Variances
            .FirstOrDefaultAsync(v => v.Id == request.VarianceId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy chênh lệch.");

        var current = entity.Status.Trim().ToLowerInvariant();
        var target = request.TargetStatus.Trim().ToLowerInvariant();

        if (current == target)
        {
            return;
        }

        if (current != VarianceStatuses.Open)
        {
            throw new ConflictAppException(
                "Chỉ chuyển được chênh lệch đang mở (open → accepted / cleared / written_off).");
        }

        entity.Status = target;
        if (!string.IsNullOrWhiteSpace(request.Explanation))
        {
            entity.Explanation = request.Explanation.Trim();
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
