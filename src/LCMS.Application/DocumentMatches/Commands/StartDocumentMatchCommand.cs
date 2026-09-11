using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.DocumentMatches.Commands;

public sealed record StartDocumentMatchCommand(
    Guid? PrimaryDocumentId,
    string? MatchMethod,
    string? Notes) : IRequest<Guid>;

public sealed class StartDocumentMatchCommandValidator : AbstractValidator<StartDocumentMatchCommand>
{
    public StartDocumentMatchCommandValidator()
    {
        RuleFor(x => x.MatchMethod)
            .Must(m => m is null || string.Equals(m, DocumentMatchMethods.Manual, StringComparison.OrdinalIgnoreCase))
            .WithMessage("Phương thức khớp Pass 1 chỉ hỗ trợ manual.");
        RuleFor(x => x.Notes).MaximumLength(1024).When(x => x.Notes is not null);
    }
}

/// <summary>
/// Starts a draft match session. Tolerance stub = 0 (C-007). Does not create Cost/Revenue.
/// </summary>
public sealed class StartDocumentMatchCommandHandler : IRequestHandler<StartDocumentMatchCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public StartDocumentMatchCommandHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<Guid> Handle(StartDocumentMatchCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;
        int versionNo = 1;

        if (request.PrimaryDocumentId.HasValue)
        {
            var document = await _db.FinancialDocuments.AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == request.PrimaryDocumentId, cancellationToken)
                ?? throw new NotFoundAppException("Không tìm thấy chứng từ tài chính.");

            if (!string.Equals(document.ReceiptStatus, FinancialDocumentReceiptStatuses.Received, StringComparison.OrdinalIgnoreCase))
            {
                throw new ConflictAppException("Chỉ khớp chứng từ đã nhận.");
            }

            versionNo = (await _db.DocumentMatches
                .Where(m => m.PrimaryDocumentId == document.Id)
                .Select(m => (int?)m.VersionNo)
                .MaxAsync(cancellationToken) ?? 0) + 1;
        }

        var match = new DocumentMatch
        {
            TenantId = tenantId,
            PrimaryDocumentId = request.PrimaryDocumentId,
            MatchMethod = DocumentMatchMethods.Manual,
            MatchStatus = DocumentMatchStatuses.Draft,
            VersionNo = versionNo,
            ToleranceAmount = 0m, // C-007 Pass 1 stub
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
        };

        _db.DocumentMatches.Add(match);
        await _db.SaveChangesAsync(cancellationToken);
        return match.Id;
    }
}
