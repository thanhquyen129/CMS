using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Reconciliations.Commands;

public sealed record StartReconciliationCommand(
    string? ReconciliationType,
    string? RuleCode,
    Guid? BillId,
    string? Notes) : IRequest<Guid>;

public sealed class StartReconciliationCommandValidator : AbstractValidator<StartReconciliationCommand>
{
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ReconciliationTypes.Manual,
        ReconciliationTypes.PaymentAp,
        ReconciliationTypes.CollectionAr,
        ReconciliationTypes.Document,
        ReconciliationTypes.CostRevenue
    };

    public StartReconciliationCommandValidator()
    {
        RuleFor(x => x.ReconciliationType)
            .Must(t => t is null || AllowedTypes.Contains(t.Trim()))
            .WithMessage("Loại đối soát không hợp lệ.");
        RuleFor(x => x.RuleCode).MaximumLength(128).When(x => x.RuleCode is not null);
        RuleFor(x => x.Notes).MaximumLength(2048).When(x => x.Notes is not null);
    }
}

/// <summary>Creates/starts a reconciliation session (draft → in_progress).</summary>
public sealed class StartReconciliationCommandHandler : IRequestHandler<StartReconciliationCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;

    public StartReconciliationCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
    }

    public async Task<Guid> Handle(StartReconciliationCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;
        if (request.BillId.HasValue)
        {
            var billExists = await _db.Bills.AsNoTracking()
                .AnyAsync(b => b.Id == request.BillId, cancellationToken);
            if (!billExists)
            {
                throw new NotFoundAppException("Không tìm thấy Bill.");
            }
        }

        var type = string.IsNullOrWhiteSpace(request.ReconciliationType)
            ? ReconciliationTypes.Manual
            : request.ReconciliationType.Trim().ToLowerInvariant();

        var versionNo = 1;
        if (request.BillId.HasValue)
        {
            versionNo = (await _db.Reconciliations
                .Where(r => r.BillId == request.BillId)
                .Select(r => (int?)r.VersionNo)
                .MaxAsync(cancellationToken) ?? 0) + 1;
        }

        var now = DateTimeOffset.UtcNow;
        var session = new Reconciliation
        {
            TenantId = tenantId,
            ReconciliationType = type,
            RuleCode = string.IsNullOrWhiteSpace(request.RuleCode) ? null : request.RuleCode.Trim(),
            VersionNo = versionNo,
            Status = ReconciliationStatuses.InProgress,
            BillId = request.BillId,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            StartedAt = now,
            StartedBy = _user.UserId
        };

        _db.Reconciliations.Add(session);
        await _db.SaveChangesAsync(cancellationToken);
        return session.Id;
    }
}
