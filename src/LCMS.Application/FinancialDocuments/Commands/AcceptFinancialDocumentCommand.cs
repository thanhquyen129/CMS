using FluentValidation;
using LCMS.Application.Abstractions;
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

    public AcceptFinancialDocumentCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
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

        document.AcceptanceStatus = FinancialDocumentAcceptanceStatuses.Accepted;
        document.AcceptedAt = DateTimeOffset.UtcNow;
        document.AcceptedBy = _user.UserId;
        // MatchingStatus and ReceiptStatus remain unchanged (AC-005).

        await _db.SaveChangesAsync(cancellationToken);
    }
}
