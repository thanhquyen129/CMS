using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.FinancialDocuments.Queries;

public sealed record FinancialDocumentLineDto(
    Guid Id,
    int LineNo,
    string? Description,
    decimal Amount,
    decimal MatchedAmount,
    decimal OpenAmount,
    string CurrencyCode,
    Guid? BillId,
    string? CostTypeCode,
    string? RevenueTypeCode);

public sealed record FinancialDocumentDto(
    Guid Id,
    string DocumentType,
    string DocumentNo,
    string Direction,
    decimal TotalAmount,
    string CurrencyCode,
    DateOnly DocumentDate,
    Guid? CounterpartyId,
    Guid? BillId,
    string ReceiptStatus,
    string AcceptanceStatus,
    string MatchingStatus,
    DateTimeOffset? ReceivedAt,
    DateTimeOffset? AcceptedAt,
    string RecordStatus,
    string? Notes,
    IReadOnlyList<FinancialDocumentLineDto> Lines);

public sealed record FinancialDocumentListItemDto(
    Guid Id,
    string DocumentType,
    string DocumentNo,
    string Direction,
    decimal TotalAmount,
    string CurrencyCode,
    string ReceiptStatus,
    string AcceptanceStatus,
    string MatchingStatus,
    DateOnly DocumentDate);

public sealed record GetFinancialDocumentByIdQuery(Guid Id) : IRequest<FinancialDocumentDto>;

public sealed class GetFinancialDocumentByIdQueryHandler
    : IRequestHandler<GetFinancialDocumentByIdQuery, FinancialDocumentDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public GetFinancialDocumentByIdQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<FinancialDocumentDto> Handle(
        GetFinancialDocumentByIdQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var document = await _db.FinancialDocuments.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy chứng từ tài chính.");

        var lines = await _db.FinancialDocumentLines.AsNoTracking()
            .Where(l => l.DocumentId == document.Id)
            .OrderBy(l => l.LineNo)
            .Select(l => new FinancialDocumentLineDto(
                l.Id,
                l.LineNo,
                l.Description,
                l.Amount,
                l.MatchedAmount,
                l.Amount - l.MatchedAmount,
                l.CurrencyCode,
                l.BillId,
                l.CostTypeCode,
                l.RevenueTypeCode))
            .ToListAsync(cancellationToken);

        return new FinancialDocumentDto(
            document.Id,
            document.DocumentType,
            document.DocumentNo,
            document.Direction,
            document.TotalAmount,
            document.CurrencyCode,
            document.DocumentDate,
            document.CounterpartyId,
            document.BillId,
            document.ReceiptStatus,
            document.AcceptanceStatus,
            document.MatchingStatus,
            document.ReceivedAt,
            document.AcceptedAt,
            document.RecordStatus,
            document.Notes,
            lines);
    }
}

public sealed record ListFinancialDocumentsQuery(
    string? DocumentType,
    string? ReceiptStatus,
    string? AcceptanceStatus,
    string? MatchingStatus) : IRequest<IReadOnlyList<FinancialDocumentListItemDto>>;

public sealed class ListFinancialDocumentsQueryHandler
    : IRequestHandler<ListFinancialDocumentsQuery, IReadOnlyList<FinancialDocumentListItemDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ListFinancialDocumentsQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<FinancialDocumentListItemDto>> Handle(
        ListFinancialDocumentsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var query = _db.FinancialDocuments.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.DocumentType))
        {
            var t = request.DocumentType.Trim().ToLowerInvariant();
            query = query.Where(d => d.DocumentType == t);
        }

        if (!string.IsNullOrWhiteSpace(request.ReceiptStatus))
        {
            var s = request.ReceiptStatus.Trim().ToLowerInvariant();
            query = query.Where(d => d.ReceiptStatus == s);
        }

        if (!string.IsNullOrWhiteSpace(request.AcceptanceStatus))
        {
            var s = request.AcceptanceStatus.Trim().ToLowerInvariant();
            query = query.Where(d => d.AcceptanceStatus == s);
        }

        if (!string.IsNullOrWhiteSpace(request.MatchingStatus))
        {
            var s = request.MatchingStatus.Trim().ToLowerInvariant();
            query = query.Where(d => d.MatchingStatus == s);
        }

        return await query
            .OrderByDescending(d => d.Id)
            .Select(d => new FinancialDocumentListItemDto(
                d.Id,
                d.DocumentType,
                d.DocumentNo,
                d.Direction,
                d.TotalAmount,
                d.CurrencyCode,
                d.ReceiptStatus,
                d.AcceptanceStatus,
                d.MatchingStatus,
                d.DocumentDate))
            .ToListAsync(cancellationToken);
    }
}
