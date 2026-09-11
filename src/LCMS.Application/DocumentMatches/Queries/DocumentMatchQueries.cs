using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.DocumentMatches.Queries;

public sealed record DocumentMatchDetailDto(
    Guid Id,
    Guid SourceLineId,
    Guid? TargetLineId,
    Guid? TargetCostId,
    Guid? TargetRevenueId,
    decimal MatchedAmount);

public sealed record DocumentMatchDto(
    Guid Id,
    string MatchMethod,
    string MatchStatus,
    int VersionNo,
    Guid? PrimaryDocumentId,
    decimal ToleranceAmount,
    string? Notes,
    DateTimeOffset? ConfirmedAt,
    IReadOnlyList<DocumentMatchDetailDto> Details);

public sealed record GetDocumentMatchByIdQuery(Guid Id) : IRequest<DocumentMatchDto>;

public sealed class GetDocumentMatchByIdQueryHandler : IRequestHandler<GetDocumentMatchByIdQuery, DocumentMatchDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public GetDocumentMatchByIdQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<DocumentMatchDto> Handle(GetDocumentMatchByIdQuery request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var match = await _db.DocumentMatches.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy phiên khớp chứng từ.");

        var details = await _db.DocumentMatchDetails.AsNoTracking()
            .Where(d => d.MatchId == match.Id)
            .OrderBy(d => d.Id)
            .Select(d => new DocumentMatchDetailDto(
                d.Id,
                d.SourceLineId,
                d.TargetLineId,
                d.TargetCostId,
                d.TargetRevenueId,
                d.MatchedAmount))
            .ToListAsync(cancellationToken);

        return new DocumentMatchDto(
            match.Id,
            match.MatchMethod,
            match.MatchStatus,
            match.VersionNo,
            match.PrimaryDocumentId,
            match.ToleranceAmount,
            match.Notes,
            match.ConfirmedAt,
            details);
    }
}
