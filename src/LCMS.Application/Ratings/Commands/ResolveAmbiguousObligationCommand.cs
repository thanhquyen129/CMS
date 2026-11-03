using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Audit;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Ratings;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using LCMS.Domain.Pricing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Ratings.Commands;

public sealed record ResolveAmbiguousObligationCommand(
    Guid ExceptionId,
    string ResolutionAction,
    string Reason,
    Guid? KeepFactId = null,
    Guid? SplitOccurrenceAnchorId = null,
    string? NewServiceOccurrenceKey = null) : IRequest<ResolveAmbiguousObligationResult>;

public sealed record ResolveAmbiguousObligationResult(
    Guid ExceptionId,
    string ResolutionAction,
    int AffectedFactCount);

public sealed class ResolveAmbiguousObligationCommandValidator
    : AbstractValidator<ResolveAmbiguousObligationCommand>
{
    public ResolveAmbiguousObligationCommandValidator()
    {
        RuleFor(x => x.ExceptionId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1024)
            .WithMessage("Bắt buộc nhập lý do giải quyết ngoại lệ mơ hồ.");
        RuleFor(x => x.ResolutionAction)
            .Must(a => a is AmbiguityResolutionActions.MergeSameObligation
                or AmbiguityResolutionActions.SplitDistinctOccurrence
                or AmbiguityResolutionActions.Supersede)
            .WithMessage("resolutionAction phải là MERGE_SAME_OBLIGATION | SPLIT_DISTINCT_OCCURRENCE | SUPERSEDE.");
    }
}

public sealed class ResolveAmbiguousObligationCommandHandler
    : IRequestHandler<ResolveAmbiguousObligationCommand, ResolveAmbiguousObligationResult>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;
    private readonly ICurrentUserContext _user;
    private readonly IAuditWriter _audit;

    public ResolveAmbiguousObligationCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions,
        ICurrentUserContext user,
        IAuditWriter audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
        _user = user;
        _audit = audit;
    }

    public async Task<ResolveAmbiguousObligationResult> Handle(
        ResolveAmbiguousObligationCommand request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(
            PermissionCodes.CostCreate,
            "Bạn không có quyền giải quyết ngoại lệ nghĩa vụ kinh tế.",
            cancellationToken);

        var entity = await _db.Exceptions
            .FirstOrDefaultAsync(e => e.Id == request.ExceptionId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy ngoại lệ.");

        if (!string.Equals(entity.RuleCode, EconomicIdentity.AmbiguousRuleCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationAppException("Ngoại lệ không thuộc loại AMBIGUOUS_ECONOMIC_IDENTITY.");
        }

        if (string.Equals(entity.Status, ExceptionStatuses.Resolved, StringComparison.OrdinalIgnoreCase)
            || string.Equals(entity.Status, ExceptionStatuses.Closed, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Ngoại lệ đã được giải quyết hoặc đóng.");
        }

        var affected = 0;
        var action = request.ResolutionAction.Trim().ToUpperInvariant();
        var billId = entity.BillId;

        if (billId is Guid bill)
        {
            var costs = await _db.Costs
                .Where(c => c.BillId == bill
                            && c.RecordStatus == EconomicRecordStatuses.Active
                            && c.DeletedAt == null
                            && c.SourceType == CostSourceTypes.RatingDetail)
                .ToListAsync(cancellationToken);

            if (action == AmbiguityResolutionActions.MergeSameObligation)
            {
                var keepId = request.KeepFactId ?? costs.OrderByDescending(c => c.CreatedAt).FirstOrDefault()?.Id;
                foreach (var c in costs.Where(c => keepId.HasValue && c.Id != keepId.Value))
                {
                    if (RatingFinancialApply.HasProtectedMaturity(c))
                    {
                        continue;
                    }

                    c.RecordStatus = EconomicRecordStatuses.Superseded;
                    affected++;
                }
            }
            else if (action == AmbiguityResolutionActions.SplitDistinctOccurrence)
            {
                var i = 0;
                foreach (var c in costs)
                {
                    i++;
                    var occ = request.NewServiceOccurrenceKey is { Length: > 0 }
                        ? $"{request.NewServiceOccurrenceKey}:{i}"
                        : $"split:{c.Id:N}";
                    c.ServiceOccurrenceKey = occ;
                    c.EconomicIdentityKey = EconomicIdentity.Build(
                        c.TenantId,
                        c.BillId,
                        EconomicIdentity.DirectionBuy,
                        c.CostTypeCode ?? "COST",
                        c.TransportLegId,
                        c.ShipmentId,
                        c.VendorPartyId,
                        occ);
                    affected++;
                }
            }
            else if (action == AmbiguityResolutionActions.Supersede)
            {
                var keepId = request.KeepFactId;
                foreach (var c in costs.Where(c => !keepId.HasValue || c.Id != keepId.Value))
                {
                    if (RatingFinancialApply.HasProtectedMaturity(c))
                    {
                        continue;
                    }

                    c.RecordStatus = EconomicRecordStatuses.Superseded;
                    affected++;
                }
            }
        }

        entity.Status = ExceptionStatuses.Resolved;
        entity.ResolvedAt = DateTimeOffset.UtcNow;
        entity.ResolvedBy = _user.HasUser ? _user.UserId : null;
        entity.ResolutionNotes =
            $"{action}: {request.Reason.Trim()} | actor={entity.ResolvedBy} | at={entity.ResolvedAt:o}";

        _audit.Append(
            "rating.ambiguous.resolve",
            "exception",
            entity.Id,
            afterJson: AuditJson.Serialize(new
            {
                action,
                reason = request.Reason,
                affected,
                billId
            }),
            reason: request.Reason);

        await _db.SaveChangesAsync(cancellationToken);
        return new ResolveAmbiguousObligationResult(entity.Id, action, affected);
    }
}
