using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Audit;
using LCMS.Application.BusinessParties;
using LCMS.Application.Common;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Revenues;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Revenues.Commands;

public sealed record CreateRevenueCommand(
    Guid BillId,
    decimal Amount,
    string CurrencyCode,
    DateOnly? EffectiveDate,
    string? RevenueTypeCode,
    Guid? CustomerPartyId,
    string? SourceType,
    Guid? SourceId,
    string? RecognitionPolicyVersion,
    string? ActualRevenueOwner = null,
    string? IdempotencyKey = null) : IRequest<Guid>;

public sealed class CreateRevenueCommandValidator : AbstractValidator<CreateRevenueCommand>
{
    public CreateRevenueCommandValidator()
    {
        RuleFor(x => x.BillId)
            .NotEmpty().WithMessage("Doanh thu bắt buộc gắn Bill.");
        RuleFor(x => x.Amount)
            .GreaterThanOrEqualTo(0).WithMessage("Số tiền doanh thu không được âm.");
        RuleFor(x => x.CurrencyCode)
            .NotEmpty().WithMessage("Mã tiền tệ không được để trống.")
            .Length(3).WithMessage("Mã tiền tệ phải gồm 3 ký tự.")
            .Matches(@"^[A-Za-z]{3}$").WithMessage("Mã tiền tệ phải là 3 chữ cái ISO 4217.");
        RuleFor(x => x.RevenueTypeCode).MaximumLength(64).When(x => x.RevenueTypeCode is not null);
        RuleFor(x => x.SourceType).MaximumLength(64).When(x => x.SourceType is not null);
        RuleFor(x => x.RecognitionPolicyVersion).MaximumLength(64).When(x => x.RecognitionPolicyVersion is not null);
        // C-004: document/AR must not invent a second economic revenue row.
        RuleFor(x => x.SourceType)
            .Must(s => !IsForbiddenEconomicSource(s))
            .WithMessage("Không tạo doanh thu kinh tế mới chỉ từ chứng từ/AR (C-004).");
    }

    internal static bool IsForbiddenEconomicSource(string? sourceType)
    {
        if (string.IsNullOrWhiteSpace(sourceType))
        {
            return false;
        }

        var normalized = sourceType.Trim().ToLowerInvariant();
        return normalized is RevenueSourceTypes.Document
            or RevenueSourceTypes.AccountsReceivable
            or "ar"
            or "accounts-receivable"
            or "doc"
            or "financial_document";
    }
}

public sealed class CreateRevenueCommandHandler : IRequestHandler<CreateRevenueCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IAuditWriter _audit;
    private readonly IRevenueFxStub _fx;
    private readonly IRevenueApprovalGate _approvalGate;
    private readonly IPartyDirectoryService _parties;
    private readonly IIdempotencyGate _idempotency;
    private readonly IPermissionService _permissions;

    public CreateRevenueCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IAuditWriter audit,
        IRevenueFxStub fx,
        IRevenueApprovalGate approvalGate,
        IPartyDirectoryService parties,
        IIdempotencyGate idempotency,
        IPermissionService permissions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _audit = audit;
        _fx = fx;
        _approvalGate = approvalGate;
        _parties = parties;
        _idempotency = idempotency;
        _permissions = permissions;
    }

    public async Task<Guid> Handle(CreateRevenueCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(
            PermissionCodes.RevenueCreate,
            "Bạn không có quyền tạo doanh thu.",
            cancellationToken);

        var tenantId = _tenantContext.TenantId!.Value;
        var priorId = await _idempotency.FindAsync(
            IdempotencyScopes.Revenue,
            request.IdempotencyKey,
            cancellationToken);
        if (priorId.HasValue)
        {
            return priorId.Value;
        }

        var currency = request.CurrencyCode.Trim().ToUpperInvariant();
        var amount = decimal.Round(request.Amount, 4, MidpointRounding.AwayFromZero);

        // Defense in depth for C-004 (validator already rejects).
        if (CreateRevenueCommandValidator.IsForbiddenEconomicSource(request.SourceType))
        {
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["SourceType"] = ["Không tạo doanh thu kinh tế mới chỉ từ chứng từ/AR (C-004)."]
            });
        }

        var currencyRow = await _db.Currencies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Code == currency, cancellationToken);
        if (currencyRow is null)
        {
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["CurrencyCode"] = ["Mã tiền tệ chưa có trong danh mục. Vui lòng khai báo trước."]
            });
        }

        if (!currencyRow.IsActive)
        {
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["CurrencyCode"] = ["Mã tiền tệ đã ngừng hiệu lực."]
            });
        }

        var bill = await _db.Bills.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == request.BillId, cancellationToken);
        if (bill is null)
        {
            throw new NotFoundAppException("Không tìm thấy Bill.");
        }

        if (request.SourceId.HasValue && !string.IsNullOrWhiteSpace(request.SourceType))
        {
            var existing = await _db.Revenues.AsNoTracking()
                .FirstOrDefaultAsync(
                    r => r.SourceType == request.SourceType && r.SourceId == request.SourceId,
                    cancellationToken);
            if (existing is not null)
            {
                return existing.Id;
            }
        }

        if (request.CustomerPartyId is Guid customerId)
        {
            await _parties.EnsureUsableAsync(
                customerId,
                PartyRoleCodes.CustomerSide,
                "ghi nhận doanh thu",
                cancellationToken);
        }

        var revenue = new Revenue
        {
            TenantId = tenantId,
            BillId = request.BillId,
            FinancialMaturity = RevenueMaturities.Expected,
            ExpectedAmount = amount,
            Amount = amount,
            CurrencyCode = currency,
            RevenueTypeCode = string.IsNullOrWhiteSpace(request.RevenueTypeCode) ? null : request.RevenueTypeCode.Trim(),
            CustomerPartyId = request.CustomerPartyId,
            SourceType = string.IsNullOrWhiteSpace(request.SourceType) ? RevenueSourceTypes.Manual : request.SourceType.Trim(),
            SourceId = request.SourceId,
            RecognitionPolicyVersion = string.IsNullOrWhiteSpace(request.RecognitionPolicyVersion)
                ? null
                : request.RecognitionPolicyVersion.Trim(),
            RecordStatus = "active",
            ApprovalStatus = "not_required",
            EffectiveDate = request.EffectiveDate ?? DateOnly.FromDateTime(DateTime.UtcNow)
        };

        await _fx.ApplyToRevenueAsync(revenue, amount, cancellationToken);
        _approvalGate.RefreshPendingFlag(revenue);

        _db.Revenues.Add(revenue);
        _idempotency.Remember(
            IdempotencyScopes.Revenue,
            request.IdempotencyKey ?? string.Empty,
            revenue.Id,
            tenantId);
        var owner = string.IsNullOrWhiteSpace(request.ActualRevenueOwner)
            ? null
            : request.ActualRevenueOwner.Trim();
        if (owner is not null
            && !string.Equals(owner, OperationalSourceSystems.LcmsManual, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(owner, RevenueSourceTypes.Manual, StringComparison.OrdinalIgnoreCase))
        {
            _db.FieldOwnerships.Add(new FieldOwnership
            {
                TenantId = tenantId,
                ObjectType = "revenue",
                ObjectId = revenue.Id,
                FieldName = "actual_revenue",
                OwnerSystem = owner
            });
        }
        _audit.Append(
            AuditActions.RevenueCreate,
            AuditObjectTypes.Revenue,
            revenue.Id,
            afterJson: AuditJson.Serialize(new
            {
                id = revenue.Id,
                billId = request.BillId,
                amount,
                expectedAmount = amount,
                currency,
                maturity = RevenueMaturities.Expected,
                revenueTypeCode = revenue.RevenueTypeCode,
                baseAmount = revenue.BaseAmount
            }));

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            if (request.SourceId.HasValue)
            {
                var again = await _db.Revenues.AsNoTracking()
                    .FirstOrDefaultAsync(
                        r => r.SourceType == request.SourceType && r.SourceId == request.SourceId,
                        cancellationToken);
                if (again is not null)
                {
                    return again.Id;
                }
            }

            throw new ConflictAppException("Không thể tạo doanh thu do xung đột dữ liệu.");
        }

        return revenue.Id;
    }
}
