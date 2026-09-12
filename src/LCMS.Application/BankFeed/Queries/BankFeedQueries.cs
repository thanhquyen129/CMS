using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.BankFeed.Queries;

public sealed record BankFeedLineDto(
    Guid Id,
    DateOnly ValueDate,
    decimal Amount,
    string CurrencyCode,
    string Direction,
    string? BankReference,
    string? CounterpartyName,
    string? Description,
    string Status,
    Guid? MatchedReconciliationDetailId,
    DateTimeOffset? MatchedAt,
    DateTimeOffset? IgnoredAt,
    string? IgnoreReason);

public sealed record ListBankFeedLinesQuery(string? Status) : IRequest<IReadOnlyList<BankFeedLineDto>>;
public sealed record GetBankFeedLineByIdQuery(Guid Id) : IRequest<BankFeedLineDto>;

public sealed class ListBankFeedLinesQueryHandler
    : IRequestHandler<ListBankFeedLinesQuery, IReadOnlyList<BankFeedLineDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ListBankFeedLinesQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<BankFeedLineDto>> Handle(
        ListBankFeedLinesQuery request,
        CancellationToken cancellationToken)
    {
        EnsureTenant(_tenantContext);
        var query = _db.BankFeedLines.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim().ToLowerInvariant();
            query = query.Where(x => x.Status == status);
        }

        var list = await query
            .OrderByDescending(x => x.ValueDate)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);
        return list.Select(Map).ToList();
    }

    internal static void EnsureTenant(ITenantContext tenantContext)
    {
        if (!tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }
    }

    internal static BankFeedLineDto Map(BankFeedLine x) =>
        new(
            x.Id,
            x.ValueDate,
            x.Amount,
            x.CurrencyCode,
            x.Direction,
            x.BankReference,
            x.CounterpartyName,
            x.Description,
            x.Status,
            x.MatchedReconciliationDetailId,
            x.MatchedAt,
            x.IgnoredAt,
            x.IgnoreReason);
}

public sealed class GetBankFeedLineByIdQueryHandler : IRequestHandler<GetBankFeedLineByIdQuery, BankFeedLineDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public GetBankFeedLineByIdQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<BankFeedLineDto> Handle(GetBankFeedLineByIdQuery request, CancellationToken cancellationToken)
    {
        ListBankFeedLinesQueryHandler.EnsureTenant(_tenantContext);
        var line = await _db.BankFeedLines.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy dòng ngân hàng.");
        return ListBankFeedLinesQueryHandler.Map(line);
    }
}
