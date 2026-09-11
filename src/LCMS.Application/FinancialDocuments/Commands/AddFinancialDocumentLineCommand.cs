using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.FinancialDocuments.Commands;

public sealed record AddFinancialDocumentLineCommand(
    Guid DocumentId,
    decimal Amount,
    string? Description,
    Guid? BillId,
    string? CostTypeCode,
    string? RevenueTypeCode,
    string? CurrencyCode) : IRequest<Guid>;

public sealed class AddFinancialDocumentLineCommandValidator : AbstractValidator<AddFinancialDocumentLineCommand>
{
    public AddFinancialDocumentLineCommandValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty().WithMessage("Chứng từ không hợp lệ.");
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Số tiền dòng chứng từ phải lớn hơn 0.");
        RuleFor(x => x.Description).MaximumLength(512).When(x => x.Description is not null);
        RuleFor(x => x.CostTypeCode).MaximumLength(64).When(x => x.CostTypeCode is not null);
        RuleFor(x => x.RevenueTypeCode).MaximumLength(64).When(x => x.RevenueTypeCode is not null);
        RuleFor(x => x.CurrencyCode)
            .Length(3).WithMessage("Mã tiền tệ phải gồm 3 ký tự.")
            .When(x => !string.IsNullOrWhiteSpace(x.CurrencyCode));
    }
}

public sealed class AddFinancialDocumentLineCommandHandler : IRequestHandler<AddFinancialDocumentLineCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public AddFinancialDocumentLineCommandHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<Guid> Handle(AddFinancialDocumentLineCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;
        var document = await _db.FinancialDocuments
            .FirstOrDefaultAsync(d => d.Id == request.DocumentId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy chứng từ tài chính.");

        if (!string.Equals(document.ReceiptStatus, FinancialDocumentReceiptStatuses.Received, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Chỉ thêm dòng trên chứng từ đã nhận.");
        }

        if (document.RecordStatus != "active")
        {
            throw new ConflictAppException("Chứng từ không còn hiệu lực.");
        }

        if (request.BillId.HasValue)
        {
            var billExists = await _db.Bills.AsNoTracking()
                .AnyAsync(b => b.Id == request.BillId, cancellationToken);
            if (!billExists)
            {
                throw new NotFoundAppException("Không tìm thấy Bill.");
            }
        }

        var maxLine = await _db.FinancialDocumentLines
            .Where(l => l.DocumentId == document.Id)
            .Select(l => (int?)l.LineNo)
            .MaxAsync(cancellationToken) ?? 0;

        var currency = string.IsNullOrWhiteSpace(request.CurrencyCode)
            ? document.CurrencyCode
            : request.CurrencyCode.Trim().ToUpperInvariant();

        if (!string.Equals(currency, document.CurrencyCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Tiền tệ dòng phải khớp tiền tệ chứng từ (C-014).");
        }

        var line = new FinancialDocumentLine
        {
            TenantId = tenantId,
            DocumentId = document.Id,
            LineNo = maxLine + 1,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Amount = decimal.Round(request.Amount, 4, MidpointRounding.AwayFromZero),
            MatchedAmount = 0m,
            CurrencyCode = currency,
            BillId = request.BillId ?? document.BillId,
            CostTypeCode = string.IsNullOrWhiteSpace(request.CostTypeCode) ? null : request.CostTypeCode.Trim(),
            RevenueTypeCode = string.IsNullOrWhiteSpace(request.RevenueTypeCode) ? null : request.RevenueTypeCode.Trim()
        };

        _db.FinancialDocumentLines.Add(line);
        await _db.SaveChangesAsync(cancellationToken);
        return line.Id;
    }
}
