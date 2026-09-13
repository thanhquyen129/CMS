using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Exposures.Commands;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.DocumentMatches.Commands;

public static class ExposureMatchSources
{
    public const string DocumentMatchDetail = "document_match_detail";
}

public sealed record MatchExposureProposalDto(
    Guid MatchDetailId,
    string Kind,
    Guid? CostId,
    Guid? RevenueId,
    Guid FinancialDocumentId,
    Guid? BillId,
    Guid? CounterpartyId,
    decimal Amount,
    string CurrencyCode,
    bool AlreadyExists,
    Guid? ExistingExposureId,
    string? SkipReason);

public sealed record ProposeExposuresFromMatchQuery(Guid MatchId)
    : IRequest<IReadOnlyList<MatchExposureProposalDto>>;

public sealed class ProposeExposuresFromMatchQueryHandler
    : IRequestHandler<ProposeExposuresFromMatchQuery, IReadOnlyList<MatchExposureProposalDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ProposeExposuresFromMatchQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<MatchExposureProposalDto>> Handle(
        ProposeExposuresFromMatchQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var match = await _db.DocumentMatches.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == request.MatchId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy phiên khớp chứng từ.");

        if (string.Equals(match.MatchStatus, DocumentMatchStatuses.Cancelled, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Không đề xuất exposure từ phiên khớp đã hủy.");
        }

        return await BuildProposalsAsync(match.Id, cancellationToken);
    }

    private async Task<IReadOnlyList<MatchExposureProposalDto>> BuildProposalsAsync(
        Guid matchId,
        CancellationToken cancellationToken)
    {
        var details = await _db.DocumentMatchDetails.AsNoTracking()
            .Where(d => d.MatchId == matchId && d.DetailStatus == DocumentMatchDetailStatuses.Active)
            .OrderBy(d => d.Id)
            .ToListAsync(cancellationToken);

        var proposals = new List<MatchExposureProposalDto>();
        foreach (var detail in details)
        {
            if (!detail.TargetCostId.HasValue && !detail.TargetRevenueId.HasValue)
            {
                proposals.Add(new MatchExposureProposalDto(
                    detail.Id,
                    "skipped",
                    null,
                    null,
                    Guid.Empty,
                    null,
                    null,
                    detail.MatchedAmount,
                    "",
                    false,
                    null,
                    "Chi tiết line↔line không tạo exposure (cần liên kết Cost/Revenue sẵn có — C-003/C-004)."));
                continue;
            }

            var sourceLine = await _db.FinancialDocumentLines.AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == detail.SourceLineId, cancellationToken);
            if (sourceLine is null)
            {
                proposals.Add(new MatchExposureProposalDto(
                    detail.Id, "skipped", null, null, Guid.Empty, null, null,
                    detail.MatchedAmount, "", false, null, "Không tìm thấy dòng chứng từ nguồn."));
                continue;
            }

            var doc = await _db.FinancialDocuments.AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == sourceLine.DocumentId, cancellationToken);
            if (doc is null)
            {
                proposals.Add(new MatchExposureProposalDto(
                    detail.Id, "skipped", null, null, Guid.Empty, null, null,
                    detail.MatchedAmount, "", false, null, "Không tìm thấy chứng từ nguồn."));
                continue;
            }

            if (detail.TargetCostId.HasValue)
            {
                if (doc.Direction == FinancialDocumentDirections.Receivable)
                {
                    proposals.Add(new MatchExposureProposalDto(
                        detail.Id, "skipped", detail.TargetCostId, null, doc.Id, doc.BillId, doc.CounterpartyId,
                        detail.MatchedAmount, sourceLine.CurrencyCode, false, null,
                        "Chứng từ phải thu không tạo exposure phải trả."));
                    continue;
                }

                var cost = await _db.Costs.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == detail.TargetCostId, cancellationToken);
                if (cost is null)
                {
                    proposals.Add(new MatchExposureProposalDto(
                        detail.Id, "skipped", detail.TargetCostId, null, doc.Id, doc.BillId, doc.CounterpartyId,
                        detail.MatchedAmount, sourceLine.CurrencyCode, false, null,
                        "Không tìm thấy chi phí đã khớp."));
                    continue;
                }

                var existing = await _db.PayableExposures.AsNoTracking()
                    .FirstOrDefaultAsync(
                        e => e.SourceType == ExposureMatchSources.DocumentMatchDetail
                             && e.SourceId == detail.Id
                             && e.RecordStatus == "active",
                        cancellationToken);

                proposals.Add(new MatchExposureProposalDto(
                    detail.Id,
                    "payable",
                    cost.Id,
                    null,
                    doc.Id,
                    cost.BillId ?? doc.BillId,
                    doc.CounterpartyId,
                    detail.MatchedAmount,
                    sourceLine.CurrencyCode,
                    existing is not null,
                    existing?.Id,
                    existing is not null ? "Đã tạo exposure từ chi tiết khớp này." : null));
                continue;
            }

            // TargetRevenueId
            if (doc.Direction == FinancialDocumentDirections.Payable)
            {
                proposals.Add(new MatchExposureProposalDto(
                    detail.Id, "skipped", null, detail.TargetRevenueId, doc.Id, doc.BillId, doc.CounterpartyId,
                    detail.MatchedAmount, sourceLine.CurrencyCode, false, null,
                    "Chứng từ phải trả không tạo exposure phải thu."));
                continue;
            }

            var revenue = await _db.Revenues.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == detail.TargetRevenueId, cancellationToken);
            if (revenue is null)
            {
                proposals.Add(new MatchExposureProposalDto(
                    detail.Id, "skipped", null, detail.TargetRevenueId, doc.Id, doc.BillId, doc.CounterpartyId,
                    detail.MatchedAmount, sourceLine.CurrencyCode, false, null,
                    "Không tìm thấy doanh thu đã khớp."));
                continue;
            }

            var existingAr = await _db.ReceivableExposures.AsNoTracking()
                .FirstOrDefaultAsync(
                    e => e.SourceType == ExposureMatchSources.DocumentMatchDetail
                         && e.SourceId == detail.Id
                         && e.RecordStatus == "active",
                    cancellationToken);

            proposals.Add(new MatchExposureProposalDto(
                detail.Id,
                "receivable",
                null,
                revenue.Id,
                doc.Id,
                revenue.BillId,
                doc.CounterpartyId,
                detail.MatchedAmount,
                sourceLine.CurrencyCode,
                existingAr is not null,
                existingAr?.Id,
                existingAr is not null ? "Đã tạo exposure từ chi tiết khớp này." : null));
        }

        return proposals;
    }
}

public sealed record CreateExposuresFromMatchCommand(Guid MatchId)
    : IRequest<CreateExposuresFromMatchResult>;

public sealed record CreateExposuresFromMatchResult(
    int CreatedCount,
    int SkippedExistingCount,
    int SkippedIneligibleCount,
    IReadOnlyList<Guid> CreatedPayableExposureIds,
    IReadOnlyList<Guid> CreatedReceivableExposureIds);

public sealed class CreateExposuresFromMatchCommandHandler
    : IRequestHandler<CreateExposuresFromMatchCommand, CreateExposuresFromMatchResult>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ISender _sender;

    public CreateExposuresFromMatchCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ISender sender)
    {
        _db = db;
        _tenantContext = tenantContext;
        _sender = sender;
    }

    public async Task<CreateExposuresFromMatchResult> Handle(
        CreateExposuresFromMatchCommand request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var match = await _db.DocumentMatches.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == request.MatchId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy phiên khớp chứng từ.");

        if (string.Equals(match.MatchStatus, DocumentMatchStatuses.Cancelled, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Không tạo exposure từ phiên khớp đã hủy.");
        }

        var proposals = await _sender.Send(
            new ProposeExposuresFromMatchQuery(match.Id),
            cancellationToken);
        var createdAp = new List<Guid>();
        var createdAr = new List<Guid>();
        var skippedExisting = 0;
        var skippedIneligible = 0;

        var costBefore = await _db.Costs.CountAsync(cancellationToken);
        var revenueBefore = await _db.Revenues.CountAsync(cancellationToken);

        foreach (var p in proposals)
        {
            if (p.AlreadyExists)
            {
                skippedExisting++;
                continue;
            }

            if (p.Kind is not ("payable" or "receivable"))
            {
                skippedIneligible++;
                continue;
            }

            if (p.Kind == "payable" && p.CostId.HasValue)
            {
                var id = await _sender.Send(
                    new CreatePayableExposureCommand(
                        p.Amount,
                        p.CurrencyCode,
                        null,
                        null,
                        p.BillId,
                        p.CounterpartyId,
                        p.CostId,
                        p.FinancialDocumentId,
                        $"Tự tạo từ chi tiết khớp {p.MatchDetailId:N}.",
                        ExposureMatchSources.DocumentMatchDetail,
                        p.MatchDetailId),
                    cancellationToken);
                createdAp.Add(id);
            }
            else if (p.Kind == "receivable" && p.RevenueId.HasValue)
            {
                var id = await _sender.Send(
                    new CreateReceivableExposureCommand(
                        p.Amount,
                        p.CurrencyCode,
                        null,
                        null,
                        p.BillId,
                        p.CounterpartyId,
                        p.RevenueId,
                        p.FinancialDocumentId,
                        $"Tự tạo từ chi tiết khớp {p.MatchDetailId:N}.",
                        ExposureMatchSources.DocumentMatchDetail,
                        p.MatchDetailId),
                    cancellationToken);
                createdAr.Add(id);
            }
            else
            {
                skippedIneligible++;
            }
        }

        var costAfter = await _db.Costs.CountAsync(cancellationToken);
        var revenueAfter = await _db.Revenues.CountAsync(cancellationToken);
        if (costAfter != costBefore || revenueAfter != revenueBefore)
        {
            throw new ConflictAppException(
                "Tạo exposure từ khớp không được tạo Chi phí hoặc Doanh thu mới (C-003/C-004).");
        }

        return new CreateExposuresFromMatchResult(
            createdAp.Count + createdAr.Count,
            skippedExisting,
            skippedIneligible,
            createdAp,
            createdAr);
    }
}
