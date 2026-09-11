using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.DocumentMatches.Commands;

public sealed record AddDocumentMatchDetailCommand(
    Guid MatchId,
    Guid SourceLineId,
    Guid? TargetLineId,
    Guid? TargetCostId,
    Guid? TargetRevenueId,
    decimal MatchedAmount) : IRequest<Guid>;

public sealed class AddDocumentMatchDetailCommandValidator : AbstractValidator<AddDocumentMatchDetailCommand>
{
    public AddDocumentMatchDetailCommandValidator()
    {
        RuleFor(x => x.MatchId).NotEmpty().WithMessage("Phiên khớp không hợp lệ.");
        RuleFor(x => x.SourceLineId).NotEmpty().WithMessage("Dòng nguồn không hợp lệ.");
        RuleFor(x => x.MatchedAmount)
            .GreaterThan(0).WithMessage("Số tiền khớp phải lớn hơn 0.");
        RuleFor(x => x)
            .Must(x =>
            {
                var targets = 0;
                if (x.TargetLineId.HasValue) targets++;
                if (x.TargetCostId.HasValue) targets++;
                if (x.TargetRevenueId.HasValue) targets++;
                return targets == 1;
            })
            .WithMessage("Phải chọn đúng một đích: dòng chứng từ, chi phí hoặc doanh thu.");
    }
}

/// <summary>
/// Adds N:N match detail. Enforces C-007 (no over-match; tolerance stub = 0).
/// Links only — never creates Cost/Revenue (C-003/C-004).
/// </summary>
public sealed class AddDocumentMatchDetailCommandHandler : IRequestHandler<AddDocumentMatchDetailCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public AddDocumentMatchDetailCommandHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<Guid> Handle(AddDocumentMatchDetailCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;
        var match = await _db.DocumentMatches
            .FirstOrDefaultAsync(m => m.Id == request.MatchId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy phiên khớp chứng từ.");

        if (!string.Equals(match.MatchStatus, DocumentMatchStatuses.Draft, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Chỉ thêm chi tiết khớp khi phiên ở trạng thái nháp.");
        }

        var amount = decimal.Round(request.MatchedAmount, 4, MidpointRounding.AwayFromZero);
        var tolerance = match.ToleranceAmount; // stub 0

        var sourceLine = await _db.FinancialDocumentLines
            .FirstOrDefaultAsync(l => l.Id == request.SourceLineId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy dòng chứng từ nguồn.");

        await EnsureOpenAmountAllowsAsync(sourceLine, amount, tolerance, cancellationToken);

        FinancialDocumentLine? targetLine = null;
        if (request.TargetLineId.HasValue)
        {
            if (request.TargetLineId == request.SourceLineId)
            {
                throw new ConflictAppException("Không khớp một dòng với chính nó.");
            }

            targetLine = await _db.FinancialDocumentLines
                .FirstOrDefaultAsync(l => l.Id == request.TargetLineId, cancellationToken)
                ?? throw new NotFoundAppException("Không tìm thấy dòng chứng từ đích.");

            if (!string.Equals(sourceLine.CurrencyCode, targetLine.CurrencyCode, StringComparison.OrdinalIgnoreCase))
            {
                throw new ConflictAppException("Không khớp khác tiền tệ (C-014).");
            }

            await EnsureOpenAmountAllowsAsync(targetLine, amount, tolerance, cancellationToken);
        }

        if (request.TargetCostId.HasValue)
        {
            var costExists = await _db.Costs.AsNoTracking()
                .AnyAsync(c => c.Id == request.TargetCostId, cancellationToken);
            if (!costExists)
            {
                throw new NotFoundAppException("Không tìm thấy chi phí để liên kết khớp.");
            }
            // Link only — do not create Cost (C-003).
        }

        if (request.TargetRevenueId.HasValue)
        {
            var revenueExists = await _db.Revenues.AsNoTracking()
                .AnyAsync(r => r.Id == request.TargetRevenueId, cancellationToken);
            if (!revenueExists)
            {
                throw new NotFoundAppException("Không tìm thấy doanh thu để liên kết khớp.");
            }
            // Link only — do not create Revenue (C-004).
        }

        var detail = new DocumentMatchDetail
        {
            TenantId = tenantId,
            MatchId = match.Id,
            SourceLineId = sourceLine.Id,
            TargetLineId = targetLine?.Id,
            TargetCostId = request.TargetCostId,
            TargetRevenueId = request.TargetRevenueId,
            MatchedAmount = amount
        };

        _db.DocumentMatchDetails.Add(detail);

        sourceLine.MatchedAmount = decimal.Round(sourceLine.MatchedAmount + amount, 4, MidpointRounding.AwayFromZero);
        if (targetLine is not null)
        {
            targetLine.MatchedAmount = decimal.Round(targetLine.MatchedAmount + amount, 4, MidpointRounding.AwayFromZero);
        }

        await _db.SaveChangesAsync(cancellationToken);

        await RefreshDocumentMatchingStatusAsync(sourceLine.DocumentId, cancellationToken);
        if (targetLine is not null && targetLine.DocumentId != sourceLine.DocumentId)
        {
            await RefreshDocumentMatchingStatusAsync(targetLine.DocumentId, cancellationToken);
        }

        return detail.Id;
    }

    private async Task EnsureOpenAmountAllowsAsync(
        FinancialDocumentLine line,
        decimal additional,
        decimal tolerance,
        CancellationToken cancellationToken)
    {
        var amounts = await _db.DocumentMatchDetails.AsNoTracking()
            .Where(d => d.SourceLineId == line.Id || d.TargetLineId == line.Id)
            .Select(d => d.MatchedAmount)
            .ToListAsync(cancellationToken);
        var alreadyMatched = amounts.Sum();

        var openAmount = line.Amount - alreadyMatched;
        if (additional > openAmount + tolerance)
        {
            throw new ConflictAppException(
                "Tổng số tiền khớp vượt số tiền mở của dòng chứng từ (C-007).");
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

        // ReceiptStatus / AcceptanceStatus untouched (AC-005).
        await _db.SaveChangesAsync(cancellationToken);
    }
}
