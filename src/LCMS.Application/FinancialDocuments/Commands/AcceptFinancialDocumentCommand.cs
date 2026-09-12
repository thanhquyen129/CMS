using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Audit;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.FinancialDocuments.Commands;

/// <summary>
/// Accept a received document. Sets AcceptanceStatus only — does not change Receipt or Matching.
/// </summary>
public sealed record AcceptFinancialDocumentCommand(Guid DocumentId) : IRequest;

public sealed class AcceptFinancialDocumentCommandValidator : AbstractValidator<AcceptFinancialDocumentCommand>
{
    public AcceptFinancialDocumentCommandValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty().WithMessage("Chứng từ không hợp lệ.");
    }
}

public sealed class AcceptFinancialDocumentCommandHandler : IRequestHandler<AcceptFinancialDocumentCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;
    private readonly IAuditWriter _audit;

    public AcceptFinancialDocumentCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user,
        IAuditWriter audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
        _audit = audit;
    }

    public async Task Handle(AcceptFinancialDocumentCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var document = await _db.FinancialDocuments
            .FirstOrDefaultAsync(d => d.Id == request.DocumentId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy chứng từ tài chính.");

        if (!string.Equals(document.ReceiptStatus, FinancialDocumentReceiptStatuses.Received, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Chỉ chấp nhận chứng từ đã nhận.");
        }

        if (string.Equals(document.AcceptanceStatus, FinancialDocumentAcceptanceStatuses.Accepted, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Chứng từ đã được chấp nhận.");
        }

        if (string.Equals(document.AcceptanceStatus, FinancialDocumentAcceptanceStatuses.Rejected, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Chứng từ đã bị từ chối; không thể chấp nhận.");
        }

        if (!string.Equals(document.RecordStatus, FinancialDocumentRecordStatuses.Active, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Không chấp nhận chứng từ đã hủy hoặc vô hiệu.");
        }

        var beforeJson = AuditJson.Serialize(new
        {
            id = document.Id,
            billId = document.BillId,
            receiptStatus = document.ReceiptStatus,
            acceptanceStatus = document.AcceptanceStatus,
            matchingStatus = document.MatchingStatus,
            documentNo = document.DocumentNo,
            currency = document.CurrencyCode,
            totalAmount = document.TotalAmount
        });

        document.AcceptanceStatus = FinancialDocumentAcceptanceStatuses.Accepted;
        document.AcceptedAt = DateTimeOffset.UtcNow;
        document.AcceptedBy = _user.UserId;
        // MatchingStatus and ReceiptStatus remain unchanged (AC-005).

        _audit.Append(
            AuditActions.FinancialDocumentAccept,
            AuditObjectTypes.FinancialDocument,
            document.Id,
            beforeJson: beforeJson,
            afterJson: AuditJson.Serialize(new
            {
                id = document.Id,
                billId = document.BillId,
                receiptStatus = document.ReceiptStatus,
                acceptanceStatus = FinancialDocumentAcceptanceStatuses.Accepted,
                matchingStatus = document.MatchingStatus,
                documentNo = document.DocumentNo,
                currency = document.CurrencyCode,
                totalAmount = document.TotalAmount,
                acceptedAt = document.AcceptedAt
            }));

        await _db.SaveChangesAsync(cancellationToken);
    }
}
