using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.PricingRules.Commands;

public sealed record AddRateBreakCommand(
    Guid PricingRuleId,
    int SequenceNo,
    decimal MinQuantity,
    decimal? MaxQuantity,
    decimal UnitAmount) : IRequest<Guid>;

public sealed class AddRateBreakCommandValidator : AbstractValidator<AddRateBreakCommand>
{
    public AddRateBreakCommandValidator()
    {
        RuleFor(x => x.PricingRuleId).NotEmpty();
        RuleFor(x => x.SequenceNo).GreaterThan(0);
        RuleFor(x => x.MinQuantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.UnitAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x)
            .Must(x => x.MaxQuantity is null || x.MaxQuantity >= x.MinQuantity)
            .WithMessage("Đến mức phải lớn hơn hoặc bằng từ mức.");
    }
}

public sealed class AddRateBreakCommandHandler : IRequestHandler<AddRateBreakCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;

    public AddRateBreakCommandHandler(ILcmsDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<Guid> Handle(AddRateBreakCommand request, CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var rule = await _db.PricingRules.FirstOrDefaultAsync(r => r.Id == request.PricingRuleId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy quy tắc tính giá.");
        var version = await _db.RateVersions.AsNoTracking().FirstAsync(v => v.Id == rule.RateVersionId, cancellationToken);
        if (version.IsPublished)
        {
            throw new ConflictAppException("Phiên bản đã phát hành không được sửa bậc giá.");
        }

        var existing = await _db.RateBreaks
            .Where(b => b.PricingRuleId == rule.Id)
            .ToListAsync(cancellationToken);
        var overlaps = existing.Any(b =>
            request.MinQuantity <= (b.MaxQuantity ?? decimal.MaxValue)
            && b.MinQuantity <= (request.MaxQuantity ?? decimal.MaxValue));
        if (overlaps || existing.Any(b => b.SequenceNo == request.SequenceNo))
        {
            throw new ConflictAppException("Bậc trọng lượng bị trùng hoặc chồng khoảng.");
        }

        var row = new RateBreak
        {
            TenantId = _tenant.TenantId!.Value,
            PricingRuleId = rule.Id,
            SequenceNo = request.SequenceNo,
            MinQuantity = request.MinQuantity,
            MaxQuantity = request.MaxQuantity,
            UnitAmount = request.UnitAmount
        };
        _db.RateBreaks.Add(row);
        await _db.SaveChangesAsync(cancellationToken);
        return row.Id;
    }
}

public sealed record AddContainerRateCommand(
    Guid PricingRuleId,
    string ContainerType,
    decimal UnitAmount) : IRequest<Guid>;

public sealed class AddContainerRateCommandValidator : AbstractValidator<AddContainerRateCommand>
{
    public AddContainerRateCommandValidator()
    {
        RuleFor(x => x.PricingRuleId).NotEmpty();
        RuleFor(x => x.ContainerType).NotEmpty().MaximumLength(16);
        RuleFor(x => x.UnitAmount).GreaterThanOrEqualTo(0);
    }
}

public sealed class AddContainerRateCommandHandler : IRequestHandler<AddContainerRateCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;

    public AddContainerRateCommandHandler(ILcmsDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<Guid> Handle(AddContainerRateCommand request, CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var rule = await _db.PricingRules.FirstOrDefaultAsync(r => r.Id == request.PricingRuleId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy quy tắc tính giá.");
        var exists = await _db.ContainerRatePrices.AnyAsync(
            p => p.PricingRuleId == rule.Id && p.ContainerType == request.ContainerType.Trim(),
            cancellationToken);
        if (exists)
        {
            throw new ConflictAppException("Loại container đã có giá trên quy tắc này.");
        }

        var row = new ContainerRatePrice
        {
            TenantId = _tenant.TenantId!.Value,
            PricingRuleId = rule.Id,
            ContainerType = request.ContainerType.Trim(),
            UnitAmount = request.UnitAmount
        };
        _db.ContainerRatePrices.Add(row);
        await _db.SaveChangesAsync(cancellationToken);
        return row.Id;
    }
}
