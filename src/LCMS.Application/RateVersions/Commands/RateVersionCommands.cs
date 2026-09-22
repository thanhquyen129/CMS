using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.RateVersions.Commands;

public sealed record CreateRateVersionCommand(
    Guid RateCardId,
    DateTimeOffset? EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string? Note) : IRequest<Guid>;

public sealed class CreateRateVersionCommandValidator : AbstractValidator<CreateRateVersionCommand>
{
    public CreateRateVersionCommandValidator()
    {
        RuleFor(x => x.RateCardId).NotEmpty().WithMessage("Bảng giá không hợp lệ.");
        RuleFor(x => x.Note)
            .MaximumLength(1024)
            .When(x => x.Note is not null);
        RuleFor(x => x)
            .Must(x => x.EffectiveTo is null || x.EffectiveFrom is null || x.EffectiveTo >= x.EffectiveFrom)
            .WithMessage("Ngày hiệu lực đến phải sau hoặc bằng ngày hiệu lực từ.");
    }
}

public sealed class CreateRateVersionCommandHandler : IRequestHandler<CreateRateVersionCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public CreateRateVersionCommandHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<Guid> Handle(CreateRateVersionCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;

        var cardExists = await _db.RateCards.AnyAsync(c => c.Id == request.RateCardId, cancellationToken);
        if (!cardExists)
        {
            throw new NotFoundAppException("Không tìm thấy bảng giá.");
        }

        var nextNo = await _db.RateVersions
            .Where(v => v.RateCardId == request.RateCardId)
            .Select(v => (int?)v.VersionNo)
            .MaxAsync(cancellationToken) ?? 0;

        var version = new RateVersion
        {
            TenantId = tenantId,
            RateCardId = request.RateCardId,
            VersionNo = nextNo + 1,
            Status = RateVersionStatuses.Draft,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim()
        };

        _db.RateVersions.Add(version);
        await _db.SaveChangesAsync(cancellationToken);
        return version.Id;
    }
}

public sealed record PublishRateVersionCommand(Guid Id) : IRequest;

public sealed class PublishRateVersionCommandHandler : IRequestHandler<PublishRateVersionCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;

    public PublishRateVersionCommandHandler(ILcmsDbContext db, ITenantContext tenantContext, IPermissionService permissions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
    }

    public async Task Handle(PublishRateVersionCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var version = await _db.RateVersions.FirstOrDefaultAsync(v => v.Id == request.Id, cancellationToken);
        if (version is null)
        {
            throw new NotFoundAppException("Không tìm thấy phiên bản bảng giá.");
        }

        if (version.IsPublished)
        {
            throw new ConflictAppException("Phiên bản bảng giá đã được phát hành.");
        }

        var hasRules = await _db.PricingRules.AnyAsync(r => r.RateVersionId == version.Id, cancellationToken);
        if (!hasRules)
        {
            throw new ConflictAppException("Phiên bản bảng giá phải có ít nhất một quy tắc tính giá trước khi phát hành.");
        }

        var partyType = await _db.RateCards.AsNoTracking()
            .Where(c => c.Id == version.RateCardId)
            .Select(c => c.PartyType)
            .FirstAsync(cancellationToken);
        var publishCode = string.Equals(partyType, "customer", StringComparison.OrdinalIgnoreCase)
            ? PermissionCodes.RateSellPublish
            : PermissionCodes.RateBuyPublish;
        await _permissions.EnsureAsync(publishCode, "Bạn không có quyền phát hành bảng giá này.", cancellationToken);

        version.Status = RateVersionStatuses.Published;
        version.PublishedAt = DateTimeOffset.UtcNow;
        version.EffectiveFrom ??= version.PublishedAt;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
