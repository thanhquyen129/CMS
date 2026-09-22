using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.FinancialCloses.Queries;

public sealed record GetFinancialCloseEligibilityQuery(Guid FinancialCloseId)
    : IRequest<CloseEligibilityResult>;

public sealed class GetFinancialCloseEligibilityQueryHandler
    : IRequestHandler<GetFinancialCloseEligibilityQuery, CloseEligibilityResult>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICloseEligibilityChecker _eligibility;

    public GetFinancialCloseEligibilityQueryHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICloseEligibilityChecker eligibility)
    {
        _db = db;
        _tenantContext = tenantContext;
        _eligibility = eligibility;
    }

    public async Task<CloseEligibilityResult> Handle(
        GetFinancialCloseEligibilityQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var close = await _db.FinancialCloses.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.FinancialCloseId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy lần chốt tài chính.");

        return await _eligibility.EvaluateAsync(close, cancellationToken);
    }
}
