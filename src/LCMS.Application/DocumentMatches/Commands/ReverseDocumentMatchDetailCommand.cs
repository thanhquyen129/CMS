using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.DocumentMatches.Commands;

public sealed record ReverseDocumentMatchDetailCommand(
    Guid MatchId,
    Guid DetailId,
    string Reason) : IRequest;

public sealed class ReverseDocumentMatchDetailCommandValidator
    : AbstractValidator<ReverseDocumentMatchDetailCommand>
{
    public ReverseDocumentMatchDetailCommandValidator()
    {
        RuleFor(x => x.MatchId).NotEmpty().WithMessage("Phiên khớp không hợp lệ.");
        RuleFor(x => x.DetailId).NotEmpty().WithMessage("Chi tiết khớp không hợp lệ.");
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Lý do đảo khớp không được để trống.")
            .MaximumLength(512).WithMessage("Lý do đảo khớp không được vượt quá 512 ký tự.");
    }
}

/// <summary>
/// Reverses a match detail (RV-003): status → reversed; restores line open amounts; no hard delete.
/// </summary>
public sealed class ReverseDocumentMatchDetailCommandHandler
    : IRequestHandler<ReverseDocumentMatchDetailCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;

    public ReverseDocumentMatchDetailCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
    }

    public async Task Handle(ReverseDocumentMatchDetailCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var match = await _db.DocumentMatches
            .FirstOrDefaultAsync(m => m.Id == request.MatchId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy phiên khớp chứng từ.");

        if (string.Equals(match.MatchStatus, DocumentMatchStatuses.Cancelled, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Phiên khớp đã hủy; không thể đảo chi tiết.");
        }

        var detail = await _db.DocumentMatchDetails
            .FirstOrDefaultAsync(d => d.Id == request.DetailId && d.MatchId == match.Id, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy chi tiết khớp chứng từ.");

        if (string.Equals(detail.DetailStatus, DocumentMatchDetailStatuses.Reversed, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Chi tiết khớp đã được đảo.");
        }

        var sourceLine = await _db.FinancialDocumentLines
            .FirstOrDefaultAsync(l => l.Id == detail.SourceLineId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy dòng chứng từ nguồn.");

        FinancialDocumentLine? targetLine = null;
        if (detail.TargetLineId.HasValue)
        {
            targetLine = await _db.FinancialDocumentLines
                .FirstOrDefaultAsync(l => l.Id == detail.TargetLineId, cancellationToken)
                ?? throw new NotFoundAppException("Không tìm thấy dòng chứng từ đích.");
        }

        var amount = detail.MatchedAmount;
        sourceLine.MatchedAmount = decimal.Round(
            Math.Max(0m, sourceLine.MatchedAmount - amount), 4, MidpointRounding.AwayFromZero);
        if (targetLine is not null)
        {
            targetLine.MatchedAmount = decimal.Round(
                Math.Max(0m, targetLine.MatchedAmount - amount), 4, MidpointRounding.AwayFromZero);
        }

        detail.DetailStatus = DocumentMatchDetailStatuses.Reversed;
        detail.ReversedAt = DateTimeOffset.UtcNow;
        detail.ReversedBy = _user.UserId;
        detail.ReverseReason = request.Reason.Trim();

        await _db.SaveChangesAsync(cancellationToken);

        await RefreshDocumentMatchingStatusAsync(sourceLine.DocumentId, cancellationToken);
        if (targetLine is not null && targetLine.DocumentId != sourceLine.DocumentId)
        {
            await RefreshDocumentMatchingStatusAsync(targetLine.DocumentId, cancellationToken);
        }
    }

    private async Task RefreshDocumentMatchingStatusAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var document = await _db.FinancialDocuments
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);
        if (document is null)
        {
            return;
        }

        var lines = await _db.FinancialDocumentLines
            .Where(l => l.DocumentId == documentId)
            .ToListAsync(cancellationToken);

        if (lines.Count == 0 || lines.All(l => l.MatchedAmount <= 0m))
        {
            document.MatchingStatus = FinancialDocumentMatchingStatuses.Unmatched;
        }
        else if (lines.All(l => l.MatchedAmount >= l.Amount))
        {
            document.MatchingStatus = FinancialDocumentMatchingStatuses.Matched;
        }
        else
        {
            document.MatchingStatus = FinancialDocumentMatchingStatuses.PartiallyMatched;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
