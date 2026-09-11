using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.PricingRules.Commands;

public sealed record AddPricingRuleCommand(
    Guid RateVersionId,
    string Code,
    string Name,
    string CalcMethod,
    decimal UnitAmount,
    string CurrencyCode,
    string? Applicability,
    int SortOrder) : IRequest<Guid>;

public sealed class AddPricingRuleCommandValidator : AbstractValidator<AddPricingRuleCommand>
{
    public AddPricingRuleCommandValidator()
    {
        RuleFor(x => x.RateVersionId).NotEmpty().WithMessage("Phiên bản bảng giá không hợp lệ.");
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Mã quy tắc không được để trống.")
            .MaximumLength(64).WithMessage("Mã quy tắc không được vượt quá 64 ký tự.");
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên quy tắc không được để trống.")
            .MaximumLength(256).WithMessage("Tên quy tắc không được vượt quá 256 ký tự.");
        RuleFor(x => x.CalcMethod)
            .NotEmpty().WithMessage("Phương pháp tính giá không được để trống.")
            .Must(m => m is PricingCalcMethods.Fixed or PricingCalcMethods.UnitRate)
            .WithMessage("Phương pháp tính giá phải là fixed hoặc unit_rate.");
        RuleFor(x => x.UnitAmount)
            .GreaterThanOrEqualTo(0).WithMessage("Đơn giá / số tiền không được âm.");
        RuleFor(x => x.CurrencyCode)
            .NotEmpty().WithMessage("Mã tiền tệ không được để trống.")
            .Length(3).WithMessage("Mã tiền tệ phải gồm 3 ký tự.");
        RuleFor(x => x.Applicability)
            .MaximumLength(512)
            .When(x => x.Applicability is not null);
    }
}

public sealed class AddPricingRuleCommandHandler : IRequestHandler<AddPricingRuleCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public AddPricingRuleCommandHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<Guid> Handle(AddPricingRuleCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;
        var version = await _db.RateVersions.FirstOrDefaultAsync(v => v.Id == request.RateVersionId, cancellationToken);
        if (version is null)
        {
            throw new NotFoundAppException("Không tìm thấy phiên bản bảng giá.");
        }

        EnsureDraft(version);

        var code = request.Code.Trim();
        if (await _db.PricingRules.AnyAsync(
                r => r.RateVersionId == version.Id && r.Code == code, cancellationToken))
        {
            throw new ConflictAppException("Mã quy tắc đã tồn tại trên phiên bản này.");
        }

        var rule = new PricingRule
        {
            TenantId = tenantId,
            RateVersionId = version.Id,
            Code = code,
            Name = request.Name.Trim(),
            CalcMethod = request.CalcMethod.Trim().ToLowerInvariant(),
            UnitAmount = request.UnitAmount,
            CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant(),
            Applicability = string.IsNullOrWhiteSpace(request.Applicability) ? null : request.Applicability.Trim(),
            SortOrder = request.SortOrder,
            IsActive = true
        };

        _db.PricingRules.Add(rule);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ConflictAppException("Mã quy tắc đã tồn tại trên phiên bản này.");
        }

        return rule.Id;
    }

    internal static void EnsureDraft(RateVersion version)
    {
        if (version.IsPublished)
        {
            throw new ConflictAppException(
                "Phiên bản bảng giá đã phát hành không được sửa. Hãy tạo phiên bản mới.");
        }
    }
}

public sealed record AddPricingRuleComponentCommand(
    Guid PricingRuleId,
    string Code,
    string Name,
    string FinancialNature,
    string? CostTypeCode,
    string? RevenueTypeCode,
    decimal Amount,
    string CurrencyCode,
    int SortOrder) : IRequest<Guid>;

public sealed class AddPricingRuleComponentCommandValidator : AbstractValidator<AddPricingRuleComponentCommand>
{
    public AddPricingRuleComponentCommandValidator()
    {
        RuleFor(x => x.PricingRuleId).NotEmpty().WithMessage("Quy tắc tính giá không hợp lệ.");
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Mã thành phần không được để trống.")
            .MaximumLength(64).WithMessage("Mã thành phần không được vượt quá 64 ký tự.");
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên thành phần không được để trống.")
            .MaximumLength(256).WithMessage("Tên thành phần không được vượt quá 256 ký tự.");
        RuleFor(x => x.FinancialNature)
            .NotEmpty().WithMessage("Tính chất tài chính không được để trống.")
            .Must(n => n is "cost" or "revenue")
            .WithMessage("Tính chất tài chính phải là cost hoặc revenue.");
        RuleFor(x => x.Amount)
            .GreaterThanOrEqualTo(0).WithMessage("Số tiền thành phần không được âm.");
        RuleFor(x => x.CurrencyCode)
            .NotEmpty().WithMessage("Mã tiền tệ không được để trống.")
            .Length(3).WithMessage("Mã tiền tệ phải gồm 3 ký tự.");
        RuleFor(x => x.CostTypeCode)
            .MaximumLength(64)
            .When(x => x.CostTypeCode is not null);
        RuleFor(x => x.RevenueTypeCode)
            .MaximumLength(64)
            .When(x => x.RevenueTypeCode is not null);
    }
}

public sealed class AddPricingRuleComponentCommandHandler : IRequestHandler<AddPricingRuleComponentCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public AddPricingRuleComponentCommandHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<Guid> Handle(AddPricingRuleComponentCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;

        var rule = await _db.PricingRules.FirstOrDefaultAsync(r => r.Id == request.PricingRuleId, cancellationToken);
        if (rule is null)
        {
            throw new NotFoundAppException("Không tìm thấy quy tắc tính giá.");
        }

        var version = await _db.RateVersions.FirstOrDefaultAsync(v => v.Id == rule.RateVersionId, cancellationToken);
        if (version is null)
        {
            throw new NotFoundAppException("Không tìm thấy phiên bản bảng giá.");
        }

        AddPricingRuleCommandHandler.EnsureDraft(version);

        var code = request.Code.Trim();
        if (await _db.PricingRuleComponents.AnyAsync(
                c => c.PricingRuleId == rule.Id && c.Code == code, cancellationToken))
        {
            throw new ConflictAppException("Mã thành phần đã tồn tại trên quy tắc này.");
        }

        var component = new PricingRuleComponent
        {
            TenantId = tenantId,
            PricingRuleId = rule.Id,
            Code = code,
            Name = request.Name.Trim(),
            FinancialNature = request.FinancialNature.Trim().ToLowerInvariant(),
            CostTypeCode = string.IsNullOrWhiteSpace(request.CostTypeCode) ? null : request.CostTypeCode.Trim(),
            RevenueTypeCode = string.IsNullOrWhiteSpace(request.RevenueTypeCode) ? null : request.RevenueTypeCode.Trim(),
            Amount = request.Amount,
            CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant(),
            SortOrder = request.SortOrder
        };

        _db.PricingRuleComponents.Add(component);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ConflictAppException("Mã thành phần đã tồn tại trên quy tắc này.");
        }

        return component.Id;
    }
}

public sealed record PricingRuleDto(
    Guid Id,
    Guid RateVersionId,
    string Code,
    string Name,
    string CalcMethod,
    decimal UnitAmount,
    string CurrencyCode,
    string? Applicability,
    int SortOrder,
    bool IsActive);

public sealed record ListPricingRulesQuery(Guid RateVersionId) : IRequest<IReadOnlyList<PricingRuleDto>>;

public sealed class ListPricingRulesQueryHandler : IRequestHandler<ListPricingRulesQuery, IReadOnlyList<PricingRuleDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ListPricingRulesQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<PricingRuleDto>> Handle(
        ListPricingRulesQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var versionExists = await _db.RateVersions.AnyAsync(v => v.Id == request.RateVersionId, cancellationToken);
        if (!versionExists)
        {
            throw new NotFoundAppException("Không tìm thấy phiên bản bảng giá.");
        }

        return await _db.PricingRules
            .AsNoTracking()
            .Where(r => r.RateVersionId == request.RateVersionId)
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Code)
            .Select(r => new PricingRuleDto(
                r.Id, r.RateVersionId, r.Code, r.Name, r.CalcMethod,
                r.UnitAmount, r.CurrencyCode, r.Applicability, r.SortOrder, r.IsActive))
            .ToListAsync(cancellationToken);
    }
}
