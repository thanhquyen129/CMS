using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.PartyBankAccounts.Queries;

public sealed record PartyBankAccountDto(
    Guid Id,
    Guid PartyId,
    string BankName,
    string? BankBranch,
    string AccountNumber,
    string? AccountName,
    string CurrencyCode,
    bool IsDefault,
    bool IsActive,
    string? Note,
    DateTimeOffset CreatedAt);

public sealed record ListPartyBankAccountsQuery(Guid PartyId) : IRequest<IReadOnlyList<PartyBankAccountDto>>;

public sealed class ListPartyBankAccountsQueryHandler
    : IRequestHandler<ListPartyBankAccountsQuery, IReadOnlyList<PartyBankAccountDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ListPartyBankAccountsQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<PartyBankAccountDto>> Handle(
        ListPartyBankAccountsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        if (!await _db.BusinessParties.AnyAsync(p => p.Id == request.PartyId, cancellationToken))
        {
            throw new NotFoundAppException("Không tìm thấy đối tác.");
        }

        return await _db.PartyBankAccounts.AsNoTracking()
            .Where(b => b.PartyId == request.PartyId)
            .OrderByDescending(b => b.IsDefault)
            .ThenBy(b => b.BankName)
            .ThenBy(b => b.AccountNumber)
            .Select(b => new PartyBankAccountDto(
                b.Id,
                b.PartyId,
                b.BankName,
                b.BankBranch,
                b.AccountNumber,
                b.AccountName,
                b.CurrencyCode,
                b.IsDefault,
                b.IsActive,
                b.Note,
                b.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
