using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Bills.Queries;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.FinancialDocuments.Commands;

/// <summary>
/// Controlled header correction while the document is received and not yet accepted or matched.
/// Does not silent-overwrite accepted/matched facts.
/// </summary>
public sealed record CorrectFinancialDocumentHeaderCommand(
    Guid DocumentId,
    string Reason,
    string? CurrencyCode,
    string? BillReference) : IRequest;

public sealed class CorrectFinancialDocumentHeaderCommandValidator
    : AbstractValidator<CorrectFinancialDocumentHeaderCommand>
{
    public CorrectFinancialDocumentHeaderCommandValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty().WithMessage("Chứng từ không hợp lệ.");
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Lý do sửa chứng từ không được để trống.")
            .MaximumLength(512).WithMessage("Lý do sửa chứng từ không được vượt quá 512 ký tự.");
        RuleFor(x => x.CurrencyCode)
            .Length(3).WithMessage("Mã tiền tệ phải gồm 3 ký tự.")
            .When(x => !string.IsNullOrWhiteSpace(x.CurrencyCode));
    }
}

public sealed class CorrectFinancialDocumentHeaderCommandHandler
    : IRequestHandler<CorrectFinancialDocumentHeaderCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IAuditWriter _audit;
    private readonly ISender _sender;

    public CorrectFinancialDocumentHeaderCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IAuditWriter audit,
        ISender sender)
    {
        _db = db;
        _tenantContext = tenantContext;
        _audit = audit;
        _sender = sender;
    }

    public async Task Handle(CorrectFinancialDocumentHeaderCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var document = await _db.FinancialDocuments
            .FirstOrDefaultAsync(d => d.Id == request.DocumentId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy chứng từ tài chính.");

        if (!string.Equals(document.RecordStatus, FinancialDocumentRecordStatuses.Active, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Chứng từ đã hủy hoặc vô hiệu — không sửa header.");
        }

        if (!string.Equals(document.ReceiptStatus, FinancialDocumentReceiptStatuses.Received, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(document.AcceptanceStatus, FinancialDocumentAcceptanceStatuses.NotAccepted, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(document.MatchingStatus, FinancialDocumentMatchingStatuses.Unmatched, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException(
                "Chỉ sửa header khi chứng từ đã nhận, chưa chấp nhận và chưa khớp. Đã chấp nhận/khớp thì dùng void hoặc đảo.");
        }

        var lines = await _db.FinancialDocumentLines
            .Where(l => l.DocumentId == document.Id)
            .ToListAsync(cancellationToken);
        if (lines.Any(l => l.MatchedAmount > 0))
        {
            throw new ConflictAppException("Dòng đã khớp — không sửa header. Đảo khớp trước.");
        }

        var before = $"{{\"currency\":\"{document.CurrencyCode}\",\"billId\":\"{document.BillId}\"}}";
        var changed = false;

        if (!string.IsNullOrWhiteSpace(request.CurrencyCode))
        {
            var currency = request.CurrencyCode.Trim().ToUpperInvariant();
            var currencyRow = await _db.Currencies.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Code == currency, cancellationToken);
            if (currencyRow is null || !currencyRow.IsActive)
            {
                throw new ValidationAppException(new Dictionary<string, string[]>
                {
                    ["currencyCode"] = [$"Tiền tệ {currency} không có trong danh mục hoặc đã ngừng dùng."]
                });
            }

            if (!string.Equals(document.CurrencyCode, currency, StringComparison.OrdinalIgnoreCase))
            {
                document.CurrencyCode = currency;
                foreach (var line in lines)
                {
                    line.CurrencyCode = currency;
                }

                changed = true;
            }
        }

        if (!string.IsNullOrWhiteSpace(request.BillReference))
        {
            var billId = await _sender.Send(new ResolveBillReferenceQuery(request.BillReference), cancellationToken);
            if (document.BillId != billId)
            {
                document.BillId = billId;
                changed = true;
            }
        }

        if (!changed)
        {
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["reason"] = ["Không có thay đổi header. Nhập tiền tệ hoặc Bill cần sửa."]
            });
        }

        _audit.Append(
            AuditActions.FinancialDocumentCorrectHeader,
            AuditObjectTypes.FinancialDocument,
            document.Id,
            beforeJson: before,
            afterJson: $"{{\"currency\":\"{document.CurrencyCode}\",\"billId\":\"{document.BillId}\"}}",
            reason: request.Reason.Trim());
        await _db.SaveChangesAsync(cancellationToken);
    }
}
