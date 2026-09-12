using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Audit;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Costs;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Costs.Commands;

public sealed record CreateCostCommand(
    Guid? BillId,
    string AttributionType,
    decimal Amount,
    string CurrencyCode,
    DateOnly? EffectiveDate,
    string? CostTypeCode,
    Guid? VendorPartyId,
    string? SourceType,
    Guid? SourceId,
    Guid? OrganizationId) : IRequest<Guid>;

public sealed class CreateCostCommandValidator : AbstractValidator<CreateCostCommand>
{
    public CreateCostCommandValidator()
    {
        RuleFor(x => x.AttributionType)
            .NotEmpty().WithMessage("Loại gán chi phí không được để trống.")
            .Must(a => a is CostAttributionTypes.Direct or CostAttributionTypes.Shared)
            .WithMessage("Loại gán chi phí phải là direct hoặc shared.");
        RuleFor(x => x.Amount)
            .GreaterThanOrEqualTo(0).WithMessage("Số tiền chi phí không được âm.");
        RuleFor(x => x.CurrencyCode)
            .NotEmpty().WithMessage("Mã tiền tệ không được để trống.")
            .Length(3).WithMessage("Mã tiền tệ phải gồm 3 ký tự.")
            .Matches(@"^[A-Za-z]{3}$").WithMessage("Mã tiền tệ phải là 3 chữ cái ISO 4217.");
        RuleFor(x => x.BillId)
            .NotEmpty().WithMessage("Chi phí trực tiếp bắt buộc gắn Bill.")
            .When(x => string.Equals(x.AttributionType, CostAttributionTypes.Direct, StringComparison.OrdinalIgnoreCase));
        RuleFor(x => x.BillId)
            .Empty().WithMessage("Chi phí chung (shared) không gắn Bill trực tiếp; dùng phân bổ.")
            .When(x => string.Equals(x.AttributionType, CostAttributionTypes.Shared, StringComparison.OrdinalIgnoreCase));
        RuleFor(x => x.CostTypeCode).MaximumLength(64).When(x => x.CostTypeCode is not null);
        RuleFor(x => x.SourceType).MaximumLength(64).When(x => x.SourceType is not null);
    }
}

public sealed class CreateCostCommandHandler : IRequestHandler<CreateCostCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _userContext;
    private readonly IAuditWriter _audit;
    private readonly ICostFxStub _fx;
    private readonly ICostApprovalGate _approvalGate;

    public CreateCostCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext userContext,
        IAuditWriter audit,
        ICostFxStub fx,
        ICostApprovalGate approvalGate)
    {
        _db = db;
        _tenantContext = tenantContext;
        _userContext = userContext;
        _audit = audit;
        _fx = fx;
        _approvalGate = approvalGate;
    }

    public async Task<Guid> Handle(CreateCostCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;
        var attribution = request.AttributionType.Trim().ToLowerInvariant();
        var currency = request.CurrencyCode.Trim().ToUpperInvariant();
        var amount = decimal.Round(request.Amount, 4, MidpointRounding.AwayFromZero);

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

        Guid? organizationId = request.OrganizationId;
        if (attribution == CostAttributionTypes.Direct)
        {
            var bill = await _db.Bills.AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == request.BillId, cancellationToken);
            if (bill is null)
            {
                throw new NotFoundAppException("Không tìm thấy Bill.");
            }

            organizationId ??= bill.OrganizationId;
        }

        if (organizationId is Guid orgId)
        {
            var orgExists = await _db.Organizations.AnyAsync(o => o.Id == orgId, cancellationToken);
            if (!orgExists)
            {
                throw new NotFoundAppException("Không tìm thấy tổ chức.");
            }
        }
        else if (_userContext.HasUser)
        {
            var actor = await _db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == _userContext.UserId, cancellationToken);
            organizationId = actor?.OrganizationId;
        }

        if (request.SourceId.HasValue && !string.IsNullOrWhiteSpace(request.SourceType))
        {
            var existing = await _db.Costs.AsNoTracking()
                .FirstOrDefaultAsync(
                    c => c.SourceType == request.SourceType && c.SourceId == request.SourceId,
                    cancellationToken);
            if (existing is not null)
            {
                return existing.Id;
            }
        }

        var cost = new Cost
        {
            TenantId = tenantId,
            BillId = attribution == CostAttributionTypes.Shared ? null : request.BillId,
            AttributionType = attribution,
            FinancialMaturity = CostMaturities.Expected,
            ExpectedAmount = amount,
            Amount = amount,
            CurrencyCode = currency,
            CostTypeCode = string.IsNullOrWhiteSpace(request.CostTypeCode) ? null : request.CostTypeCode.Trim(),
            VendorPartyId = request.VendorPartyId,
            SourceType = string.IsNullOrWhiteSpace(request.SourceType) ? CostSourceTypes.Manual : request.SourceType.Trim(),
            SourceId = request.SourceId,
            RecordStatus = "active",
            ApprovalStatus = "not_required",
            EffectiveDate = request.EffectiveDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            OrganizationId = organizationId
        };

        // Shared vs Direct invariant (defense in depth).
        if (attribution == CostAttributionTypes.Direct && cost.BillId is null)
        {
            throw new ConflictAppException("Chi phí trực tiếp bắt buộc gắn Bill.");
        }

        if (attribution == CostAttributionTypes.Shared && cost.BillId is not null)
        {
            throw new ConflictAppException("Chi phí chung (shared) không gắn Bill trực tiếp; dùng phân bổ.");
        }

        await _fx.ApplyToCostAsync(cost, amount, cancellationToken);
        _approvalGate.RefreshPendingFlag(cost);

        _db.Costs.Add(cost);
        _audit.Append(
            AuditActions.CostCreate,
            AuditObjectTypes.Cost,
            cost.Id,
            afterJson: AuditJson.Serialize(new
            {
                id = cost.Id,
                billId = cost.BillId,
                amount,
                expectedAmount = amount,
                currency,
                maturity = CostMaturities.Expected,
                attribution,
                costTypeCode = cost.CostTypeCode,
                baseAmount = cost.BaseAmount
            }));

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            if (request.SourceId.HasValue)
            {
                var again = await _db.Costs.AsNoTracking()
                    .FirstOrDefaultAsync(
                        c => c.SourceType == request.SourceType && c.SourceId == request.SourceId,
                        cancellationToken);
                if (again is not null)
                {
                    return again.Id;
                }
            }

            throw new ConflictAppException("Không thể tạo chi phí do xung đột dữ liệu.");
        }

        return cost.Id;
    }
}
