using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Audit;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.FinancialDocuments;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

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
/// Adds N:N match detail. Enforces method shape + C-007 tolerance policy.
/// Links only — never creates Cost/Revenue (C-003/C-004).
/// </summary>
public sealed class AddDocumentMatchDetailCommandHandler : IRequestHandler<AddDocumentMatchDetailCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly DocumentOptions _options;
    private readonly IAuditWriter _audit;

    public AddDocumentMatchDetailCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IOptions<DocumentOptions> options,
        IAuditWriter audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _options = options.Value;
        _audit = audit;
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

        EnsureMethodTargets(match.MatchMethod, request);

        var amount = decimal.Round(request.MatchedAmount, 4, MidpointRounding.AwayFromZero);

        var sourceLine = await _db.FinancialDocumentLines
            .FirstOrDefaultAsync(l => l.Id == request.SourceLineId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy dòng chứng từ nguồn.");

        var sourceDocument = await _db.FinancialDocuments
            .FirstOrDefaultAsync(d => d.Id == sourceLine.DocumentId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy chứng từ nguồn.");

        EnsureDocumentMatchable(sourceDocument);

        await EnsureOpenAmountAllowsAsync(
            sourceLine,
            amount,
            match.ToleranceAmount,
            match.TolerancePercent,
            cancellationToken);

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

            var targetDocument = await _db.FinancialDocuments
                .FirstOrDefaultAsync(d => d.Id == targetLine.DocumentId, cancellationToken)
                ?? throw new NotFoundAppException("Không tìm thấy chứng từ đích.");

            EnsureDocumentMatchable(targetDocument);

            if (!string.Equals(sourceLine.CurrencyCode, targetLine.CurrencyCode, StringComparison.OrdinalIgnoreCase))
            {
                throw new ConflictAppException("Không khớp khác tiền tệ (C-014).");
            }

            await EnsureOpenAmountAllowsAsync(
                targetLine,
                amount,
                match.ToleranceAmount,
                match.TolerancePercent,
                cancellationToken);
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
            MatchedAmount = amount,
            DetailStatus = DocumentMatchDetailStatuses.Active
        };

        var beforeJson = AuditJson.Serialize(new
        {
            matchId = match.Id,
            documentId = sourceDocument.Id,
            matchingStatus = sourceDocument.MatchingStatus,
            sourceLineId = sourceLine.Id,
            sourceMatchedAmount = sourceLine.MatchedAmount,
            lineAmount = sourceLine.Amount
        });

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

        // Reload matching status for audit after refresh
        var refreshedDoc = await _db.FinancialDocuments.AsNoTracking()
            .FirstAsync(d => d.Id == sourceDocument.Id, cancellationToken);

        _audit.Append(
            AuditActions.DocumentMatchDetailAdd,
            AuditObjectTypes.DocumentMatch,
            match.Id,
            beforeJson: beforeJson,
            afterJson: AuditJson.Serialize(new
            {
                matchId = match.Id,
                detailId = detail.Id,
                documentId = sourceDocument.Id,
                matchingStatus = refreshedDoc.MatchingStatus,
                sourceLineId = sourceLine.Id,
                targetLineId = targetLine?.Id,
                targetCostId = request.TargetCostId,
                targetRevenueId = request.TargetRevenueId,
                matchedAmount = amount,
                sourceMatchedAmount = sourceLine.MatchedAmount,
                matchMethod = match.MatchMethod
            }));
        await _db.SaveChangesAsync(cancellationToken);

        return detail.Id;
    }

    private static void EnsureMethodTargets(string matchMethod, AddDocumentMatchDetailCommand request)
    {
        if (string.Equals(matchMethod, DocumentMatchMethods.LineToLine, StringComparison.OrdinalIgnoreCase))
        {
            if (!request.TargetLineId.HasValue || request.TargetCostId.HasValue || request.TargetRevenueId.HasValue)
            {
                throw new ConflictAppException("Phương thức line_to_line chỉ cho phép khớp dòng ↔ dòng.");
            }

            return;
        }

        if (string.Equals(matchMethod, DocumentMatchMethods.LineToCost, StringComparison.OrdinalIgnoreCase))
        {
            if (!request.TargetCostId.HasValue || request.TargetLineId.HasValue || request.TargetRevenueId.HasValue)
            {
                throw new ConflictAppException("Phương thức line_to_cost chỉ cho phép liên kết dòng ↔ chi phí (C-003).");
            }

            return;
        }

        if (string.Equals(matchMethod, DocumentMatchMethods.LineToRevenue, StringComparison.OrdinalIgnoreCase))
        {
            if (!request.TargetRevenueId.HasValue || request.TargetLineId.HasValue || request.TargetCostId.HasValue)
            {
                throw new ConflictAppException("Phương thức line_to_revenue chỉ cho phép liên kết dòng ↔ doanh thu (C-004).");
            }

            return;
        }

        throw new ConflictAppException("Phương thức khớp không được hỗ trợ.");
    }

    private void EnsureDocumentMatchable(FinancialDocument document)
    {
        if (!string.Equals(document.RecordStatus, FinancialDocumentRecordStatuses.Active, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Không khớp chứng từ đã hủy hoặc vô hiệu.");
        }

        if (!string.Equals(document.ReceiptStatus, FinancialDocumentReceiptStatuses.Received, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Chỉ khớp chứng từ đã nhận.");
        }

        if (_options.RequireAcceptBeforeMatch
            && !string.Equals(document.AcceptanceStatus, FinancialDocumentAcceptanceStatuses.Accepted, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Phải chấp nhận chứng từ trước khi khớp (Received ≠ Accepted).");
        }
    }

    private async Task EnsureOpenAmountAllowsAsync(
        FinancialDocumentLine line,
        decimal additional,
        decimal toleranceAbsolute,
        decimal tolerancePercent,
        CancellationToken cancellationToken)
    {
        var amounts = await _db.DocumentMatchDetails.AsNoTracking()
            .Where(d =>
                (d.SourceLineId == line.Id || d.TargetLineId == line.Id)
                && d.DetailStatus == DocumentMatchDetailStatuses.Active)
            .Select(d => d.MatchedAmount)
            .ToListAsync(cancellationToken);
        var alreadyMatched = amounts.Sum();

        var openAmount = line.Amount - alreadyMatched;
        var tolerance = DocumentMatchTolerance.EffectiveTolerance(line.Amount, toleranceAbsolute, tolerancePercent);
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
