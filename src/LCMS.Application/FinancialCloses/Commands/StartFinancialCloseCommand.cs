using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.FinancialCloses.Commands;

public sealed record StartFinancialCloseCommand(
    string? ScopeType,
    Guid? ScopeId,
    DateOnly? PeriodFrom,
    DateOnly? PeriodTo,
    string? PolicyVersion,
    string? BaseCurrency,
    string? Notes,
    Guid? SupersedesCloseId,
    string? IdempotencyKey = null) : IRequest<Guid>;

public sealed class StartFinancialCloseCommandValidator : AbstractValidator<StartFinancialCloseCommand>
{
    private static readonly HashSet<string> AllowedScopes = new(StringComparer.OrdinalIgnoreCase)
    {
        FinancialCloseScopeTypes.Period,
        FinancialCloseScopeTypes.Bill,
        FinancialCloseScopeTypes.Tenant
    };

    private static readonly HashSet<string> AllowedPolicies = new(StringComparer.OrdinalIgnoreCase)
    {
        FinancialClosePolicies.Controlled,
        FinancialClosePolicies.Strict
    };

    public StartFinancialCloseCommandValidator()
    {
        RuleFor(x => x.ScopeType)
            .Must(t => t is null || AllowedScopes.Contains(t.Trim()))
            .WithMessage("Phạm vi chốt tài chính không hợp lệ.");
        RuleFor(x => x.PolicyVersion)
            .Must(p => p is null || AllowedPolicies.Contains(p.Trim()))
            .WithMessage("Chính sách chốt không hợp lệ.");
        RuleFor(x => x.BaseCurrency)
            .Length(3)
            .When(x => !string.IsNullOrWhiteSpace(x.BaseCurrency))
            .WithMessage("Mã tiền tệ phải gồm 3 ký tự.");
        RuleFor(x => x.Notes).MaximumLength(2048).When(x => x.Notes is not null);
        RuleFor(x => x)
            .Must(x => x.PeriodTo is null || x.PeriodFrom is null || x.PeriodTo >= x.PeriodFrom)
            .WithMessage("Kỳ chốt đến phải sau hoặc bằng kỳ chốt từ.");
        RuleFor(x => x)
            .Must(x =>
            {
                var scope = string.IsNullOrWhiteSpace(x.ScopeType)
                    ? FinancialCloseScopeTypes.Period
                    : x.ScopeType.Trim().ToLowerInvariant();
                return scope != FinancialCloseScopeTypes.Bill || x.ScopeId.HasValue;
            })
            .WithMessage("Chốt theo Bill yêu cầu ScopeId (Bill).");
    }
}

/// <summary>Opens a financial close period/scope (status open). Reclose may supersede a prior locked close.</summary>
public sealed class StartFinancialCloseCommandHandler : IRequestHandler<StartFinancialCloseCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;
    private readonly IIdempotencyGate _idempotency;

    public StartFinancialCloseCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user,
        IIdempotencyGate idempotency)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
        _idempotency = idempotency;
    }

    public async Task<Guid> Handle(StartFinancialCloseCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var priorId = await _idempotency.FindAsync(
            IdempotencyScopes.FinancialClose,
            request.IdempotencyKey,
            cancellationToken);
        if (priorId.HasValue)
        {
            return priorId.Value;
        }

        var tenantId = _tenantContext.TenantId!.Value;
        var scopeType = string.IsNullOrWhiteSpace(request.ScopeType)
            ? FinancialCloseScopeTypes.Period
            : request.ScopeType.Trim().ToLowerInvariant();

        if (scopeType == FinancialCloseScopeTypes.Bill)
        {
            var billExists = await _db.Bills.AsNoTracking()
                .AnyAsync(b => b.Id == request.ScopeId, cancellationToken);
            if (!billExists)
            {
                throw new NotFoundAppException("Không tìm thấy Bill.");
            }
        }

        Guid? supersedesId = request.SupersedesCloseId;
        if (supersedesId.HasValue)
        {
            var prior = await _db.FinancialCloses
                .FirstOrDefaultAsync(c => c.Id == supersedesId.Value, cancellationToken)
                ?? throw new NotFoundAppException("Không tìm thấy lần chốt tài chính để thay thế.");

            if (prior.Status != FinancialCloseStatuses.Locked
                && prior.Status != FinancialCloseStatuses.Reopened)
            {
                throw new ConflictAppException("Chỉ được chốt lại từ lần chốt đã khóa hoặc đã mở lại.");
            }

            // Align scope with superseded close for reclose versioning.
            scopeType = prior.ScopeType;
        }

        var openExists = await _db.FinancialCloses.AnyAsync(
            c => c.ScopeType == scopeType
                 && c.ScopeId == request.ScopeId
                 && (c.Status == FinancialCloseStatuses.Open || c.Status == FinancialCloseStatuses.Reopened),
            cancellationToken);
        if (openExists && !supersedesId.HasValue)
        {
            // Allow starting while a locked one exists; block duplicate open/reopened for same scope.
            throw new ConflictAppException("Đã có lần chốt đang mở hoặc đã mở lại cho phạm vi này.");
        }

        // When superseding, reopen prior if still locked (does not touch snapshots).
        if (supersedesId.HasValue)
        {
            var prior = await _db.FinancialCloses.FirstAsync(c => c.Id == supersedesId.Value, cancellationToken);
            if (prior.Status == FinancialCloseStatuses.Locked)
            {
                prior.Status = FinancialCloseStatuses.Reopened;
                prior.ReopenedAt = DateTimeOffset.UtcNow;
                prior.ReopenedBy = _user.UserId;
                prior.ReopenReason ??= "Chốt lại — tạo phiên bản mới.";
            }

            openExists = await _db.FinancialCloses.AnyAsync(
                c => c.Id != prior.Id
                     && c.ScopeType == prior.ScopeType
                     && c.ScopeId == prior.ScopeId
                     && (c.Status == FinancialCloseStatuses.Open || c.Status == FinancialCloseStatuses.Reopened),
                cancellationToken);
            if (openExists)
            {
                throw new ConflictAppException("Đã có lần chốt đang mở hoặc đã mở lại cho phạm vi này.");
            }

            var versionNo = (await _db.FinancialCloses
                .Where(c => c.ScopeType == prior.ScopeType && c.ScopeId == prior.ScopeId)
                .Select(c => (int?)c.VersionNo)
                .MaxAsync(cancellationToken) ?? 0) + 1;

            var reclose = new FinancialClose
            {
                TenantId = tenantId,
                ScopeType = prior.ScopeType,
                ScopeId = prior.ScopeId,
                PeriodFrom = request.PeriodFrom ?? prior.PeriodFrom,
                PeriodTo = request.PeriodTo ?? prior.PeriodTo,
                VersionNo = versionNo,
                Status = FinancialCloseStatuses.Open,
                PolicyVersion = string.IsNullOrWhiteSpace(request.PolicyVersion)
                    ? prior.PolicyVersion
                    : request.PolicyVersion.Trim().ToLowerInvariant(),
                BaseCurrency = string.IsNullOrWhiteSpace(request.BaseCurrency)
                    ? prior.BaseCurrency
                    : request.BaseCurrency.Trim().ToUpperInvariant(),
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                StartedAt = DateTimeOffset.UtcNow,
                StartedBy = _user.UserId,
                SupersedesCloseId = prior.Id
            };

            _db.FinancialCloses.Add(reclose);
            _idempotency.Remember(
                IdempotencyScopes.FinancialClose,
                request.IdempotencyKey ?? string.Empty,
                reclose.Id,
                tenantId);
            await _db.SaveChangesAsync(cancellationToken);
            return reclose.Id;
        }

        var nextVersion = (await _db.FinancialCloses
            .Where(c => c.ScopeType == scopeType && c.ScopeId == request.ScopeId)
            .Select(c => (int?)c.VersionNo)
            .MaxAsync(cancellationToken) ?? 0) + 1;

        var close = new FinancialClose
        {
            TenantId = tenantId,
            ScopeType = scopeType,
            ScopeId = request.ScopeId,
            PeriodFrom = request.PeriodFrom,
            PeriodTo = request.PeriodTo,
            VersionNo = nextVersion,
            Status = FinancialCloseStatuses.Open,
            PolicyVersion = string.IsNullOrWhiteSpace(request.PolicyVersion)
                ? FinancialClosePolicies.Controlled
                : request.PolicyVersion.Trim().ToLowerInvariant(),
            BaseCurrency = string.IsNullOrWhiteSpace(request.BaseCurrency)
                ? "VND"
                : request.BaseCurrency.Trim().ToUpperInvariant(),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            StartedAt = DateTimeOffset.UtcNow,
            StartedBy = _user.UserId
        };

        _db.FinancialCloses.Add(close);
        _idempotency.Remember(
            IdempotencyScopes.FinancialClose,
            request.IdempotencyKey ?? string.Empty,
            close.Id,
            tenantId);
        await _db.SaveChangesAsync(cancellationToken);
        return close.Id;
    }
}
