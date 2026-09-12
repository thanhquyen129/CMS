using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Audit;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.FinancialDocuments.Commands;

public sealed record DeleteFinancialDocumentLineCommand(Guid DocumentId, Guid LineId) : IRequest;

public sealed class DeleteFinancialDocumentLineCommandValidator : AbstractValidator<DeleteFinancialDocumentLineCommand>
{
    public DeleteFinancialDocumentLineCommandValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty().WithMessage("Chứng từ không hợp lệ.");
        RuleFor(x => x.LineId).NotEmpty().WithMessage("Dòng chứng từ không hợp lệ.");
    }
}

public sealed class DeleteFinancialDocumentLineCommandHandler : IRequestHandler<DeleteFinancialDocumentLineCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;
    private readonly IAuditWriter _audit;

    public DeleteFinancialDocumentLineCommandHandler(
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

    public async Task Handle(DeleteFinancialDocumentLineCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var document = await _db.FinancialDocuments
            .FirstOrDefaultAsync(d => d.Id == request.DocumentId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy chứng từ tài chính.");

        DocumentLineIntegrity.EnsureDocumentAllowsLineDraft(document);

        if (DocumentLineIntegrity.IsAccepted(document))
        {
            throw new ConflictAppException("Chứng từ đã chấp nhận — không xóa dòng (ADR-0012).");
        }

        var line = await _db.FinancialDocumentLines
            .FirstOrDefaultAsync(l => l.Id == request.LineId && l.DocumentId == document.Id, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy dòng chứng từ.");

        DocumentLineIntegrity.EnsureLineUnmatched(line);

        var hasMatchHistory = await _db.DocumentMatchDetails.AsNoTracking()
            .AnyAsync(
                d => d.SourceLineId == line.Id || d.TargetLineId == line.Id,
                cancellationToken);
        if (hasMatchHistory)
        {
            throw new ConflictAppException("Dòng đã từng tham gia khớp — không xóa.");
        }

        var beforeJson = AuditJson.Serialize(new
        {
            id = line.Id,
            documentId = document.Id,
            lineNo = line.LineNo,
            amount = line.Amount,
            description = line.Description
        });

        line.SoftDelete(_user.UserId);

        _audit.Append(
            AuditActions.FinancialDocumentLineDelete,
            AuditObjectTypes.FinancialDocumentLine,
            line.Id,
            beforeJson: beforeJson,
            reason: "Xóa dòng nháp chưa khớp (ADR-0012 / C-013 soft).");

        await _db.SaveChangesAsync(cancellationToken);
    }
}
