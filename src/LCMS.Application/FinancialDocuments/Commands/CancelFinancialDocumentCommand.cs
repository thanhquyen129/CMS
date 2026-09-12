using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.FinancialDocuments.Commands;

public sealed record CancelFinancialDocumentCommand(
    Guid DocumentId,
    string Reason,
    bool Void = false) : IRequest;

public sealed class CancelFinancialDocumentCommandValidator
    : AbstractValidator<CancelFinancialDocumentCommand>
{
    public CancelFinancialDocumentCommandValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty().WithMessage("Chứng từ không hợp lệ.");
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Lý do hủy chứng từ không được để trống.")
            .MaximumLength(512).WithMessage("Lý do hủy chứng từ không được vượt quá 512 ký tự.");
    }
}

/// <summary>
/// Soft cancel/void a financial document (RecordStatus). No hard delete (C-013).
/// Blocks when active match details still reference the document's lines.
/// </summary>
public sealed class CancelFinancialDocumentCommandHandler
    : IRequestHandler<CancelFinancialDocumentCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;

    public CancelFinancialDocumentCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
    }

    public async Task Handle(CancelFinancialDocumentCommand request, CancellationToken cancellationToken)
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
            throw new ConflictAppException("Chứng từ đã hủy hoặc vô hiệu.");
        }

        var lineIds = await _db.FinancialDocumentLines.AsNoTracking()
            .Where(l => l.DocumentId == document.Id)
            .Select(l => l.Id)
            .ToListAsync(cancellationToken);

        if (lineIds.Count > 0)
        {
            var hasActiveMatch = await _db.DocumentMatchDetails.AsNoTracking()
                .AnyAsync(
                    d => d.DetailStatus == DocumentMatchDetailStatuses.Active
                         && (lineIds.Contains(d.SourceLineId)
                             || (d.TargetLineId.HasValue && lineIds.Contains(d.TargetLineId.Value))),
                    cancellationToken);
            if (hasActiveMatch)
            {
                throw new ConflictAppException(
                    "Phải đảo các chi tiết khớp đang hiệu lực trước khi hủy chứng từ.");
            }
        }

        document.RecordStatus = request.Void
            ? FinancialDocumentRecordStatuses.Voided
            : FinancialDocumentRecordStatuses.Cancelled;
        document.CancelledAt = DateTimeOffset.UtcNow;
        document.CancelledBy = _user.UserId;
        document.CancelReason = request.Reason.Trim();
        // Receipt / Acceptance / Matching dimensions remain independent (AC-005).

        await _db.SaveChangesAsync(cancellationToken);
    }
}
