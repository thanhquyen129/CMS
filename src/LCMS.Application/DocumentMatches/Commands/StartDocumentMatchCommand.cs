using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.FinancialDocuments;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LCMS.Application.DocumentMatches.Commands;

public sealed record StartDocumentMatchCommand(
    Guid? PrimaryDocumentId,
    string? MatchMethod,
    string? Notes,
    decimal? ToleranceAmount,
    decimal? TolerancePercent,
    string? IdempotencyKey = null) : IRequest<Guid>;

public sealed class StartDocumentMatchCommandValidator : AbstractValidator<StartDocumentMatchCommand>
{
    public StartDocumentMatchCommandValidator()
    {
        RuleFor(x => x.MatchMethod)
            .NotEmpty().WithMessage("Phương thức khớp không được để trống.")
            .Must(m => m is not null && DocumentMatchMethods.All.Contains(m.Trim()))
            .WithMessage("Phương thức khớp phải là line_to_line, line_to_cost hoặc line_to_revenue.");
        RuleFor(x => x.Notes).MaximumLength(1024).When(x => x.Notes is not null);
        RuleFor(x => x.ToleranceAmount)
            .GreaterThanOrEqualTo(0).When(x => x.ToleranceAmount.HasValue)
            .WithMessage("Dung sai tuyệt đối không được âm.");
        RuleFor(x => x.TolerancePercent)
            .GreaterThanOrEqualTo(0).When(x => x.TolerancePercent.HasValue)
            .WithMessage("Dung sai phần trăm không được âm.")
            .LessThanOrEqualTo(100).When(x => x.TolerancePercent.HasValue)
            .WithMessage("Dung sai phần trăm không được vượt quá 100.");
    }
}

/// <summary>
/// Starts a draft match session with explicit method + tolerance policy (C-007).
/// Does not create Cost/Revenue (C-003/C-004). Requires Accept before match when configured.
/// </summary>
public sealed class StartDocumentMatchCommandHandler : IRequestHandler<StartDocumentMatchCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly DocumentOptions _options;
    private readonly IIdempotencyGate _idempotency;

    public StartDocumentMatchCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IOptions<DocumentOptions> options,
        IIdempotencyGate idempotency)
    {
        _db = db;
        _tenantContext = tenantContext;
        _options = options.Value;
        _idempotency = idempotency;
    }

    public async Task<Guid> Handle(StartDocumentMatchCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var priorId = await _idempotency.FindAsync(
            IdempotencyScopes.DocumentMatch,
            request.IdempotencyKey,
            cancellationToken);
        if (priorId.HasValue)
        {
            return priorId.Value;
        }

        var tenantId = _tenantContext.TenantId!.Value;
        int versionNo = 1;
        var method = request.MatchMethod!.Trim().ToLowerInvariant();

        if (request.PrimaryDocumentId.HasValue)
        {
            var document = await _db.FinancialDocuments.AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == request.PrimaryDocumentId, cancellationToken)
                ?? throw new NotFoundAppException("Không tìm thấy chứng từ tài chính.");

            EnsureDocumentMatchable(document);

            versionNo = (await _db.DocumentMatches
                .Where(m => m.PrimaryDocumentId == document.Id)
                .Select(m => (int?)m.VersionNo)
                .MaxAsync(cancellationToken) ?? 0) + 1;
        }

        var match = new DocumentMatch
        {
            TenantId = tenantId,
            PrimaryDocumentId = request.PrimaryDocumentId,
            MatchMethod = method,
            MatchStatus = DocumentMatchStatuses.Draft,
            VersionNo = versionNo,
            ToleranceAmount = request.ToleranceAmount
                ?? decimal.Round(_options.DefaultToleranceAbsolute, 4, MidpointRounding.AwayFromZero),
            TolerancePercent = request.TolerancePercent
                ?? decimal.Round(_options.DefaultTolerancePercent, 4, MidpointRounding.AwayFromZero),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
        };

        _db.DocumentMatches.Add(match);
        _idempotency.Remember(
            IdempotencyScopes.DocumentMatch,
            request.IdempotencyKey ?? string.Empty,
            match.Id,
            tenantId);
        await _db.SaveChangesAsync(cancellationToken);
        return match.Id;
    }

    private void EnsureDocumentMatchable(FinancialDocument document)
    {
        if (!string.Equals(document.RecordStatus, FinancialDocumentRecordStatuses.Active, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Không khớp chứng từ đã hủy hoặc vô hiệu.");
        }

        if (!string.Equals(document.ReceiptStatus, FinancialDocumentReceiptStatuses.Received, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Chỉ khớp chứng từ đã nhận.");
        }

        if (_options.RequireAcceptBeforeMatch
            && !string.Equals(document.AcceptanceStatus, FinancialDocumentAcceptanceStatuses.Accepted, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Phải chấp nhận chứng từ trước khi khớp (Received ≠ Accepted).");
        }
    }
}
