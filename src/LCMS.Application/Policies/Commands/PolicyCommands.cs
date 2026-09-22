using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Policies.Commands;

public sealed record EnsurePolicyCatalogCommand : IRequest<int>;

public sealed class EnsurePolicyCatalogCommandHandler : IRequestHandler<EnsurePolicyCatalogCommand, int>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ICurrentUserContext _user;

    public EnsurePolicyCatalogCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenant,
        ICurrentUserContext user)
    {
        _db = db;
        _tenant = tenant;
        _user = user;
    }

    public async Task<int> Handle(EnsurePolicyCatalogCommand request, CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenant.TenantId!.Value;
        var existing = await _db.Policies.AsNoTracking()
            .Select(p => p.PolicyKey)
            .Distinct()
            .ToListAsync(cancellationToken);
        var missing = PolicyKeys.All
            .Where(k => !existing.Any(e => string.Equals(e, k, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (missing.Count == 0)
        {
            return 0;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        foreach (var key in missing)
        {
            _db.Policies.Add(new Policy
            {
                TenantId = tenantId,
                PolicyKey = key,
                Title = PolicyKeys.DefaultTitles.TryGetValue(key, out var title) ? title : key,
                OwnerUserId = _user.UserId,
                Version = 1,
                EffectiveFrom = today,
                Status = PolicyStatuses.Active,
                BodyJson = "{}",
                Notes = "Khởi tạo sổ chính sách (FR-011 / POL-01)."
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return missing.Count;
    }
}

public sealed record UpsertPolicyCommand(
    string PolicyKey,
    string? Title,
    Guid? OwnerUserId,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    string Status,
    string? BodyJson,
    string? Notes,
    bool CreateNewVersion) : IRequest<Guid>;

public sealed class UpsertPolicyCommandValidator : AbstractValidator<UpsertPolicyCommand>
{
    public UpsertPolicyCommandValidator()
    {
        RuleFor(x => x.PolicyKey)
            .NotEmpty().WithMessage("Khóa chính sách không được để trống.")
            .Must(k => PolicyKeys.All.Any(x => string.Equals(x, k, StringComparison.OrdinalIgnoreCase)))
            .WithMessage("Khóa chính sách không thuộc bộ 13 khóa chuẩn.");
        RuleFor(x => x.Status)
            .NotEmpty()
            .Must(s => s is PolicyStatuses.Draft or PolicyStatuses.Active or PolicyStatuses.Superseded or PolicyStatuses.Retired)
            .WithMessage("Trạng thái phải là draft, active, superseded hoặc retired.");
        RuleFor(x => x.Title).MaximumLength(256).When(x => x.Title is not null);
        RuleFor(x => x.Notes).MaximumLength(2048).When(x => x.Notes is not null);
        RuleFor(x => x)
            .Must(x => x.EffectiveTo is null || x.EffectiveTo >= x.EffectiveFrom)
            .WithMessage("Ngày hết hiệu lực phải sau hoặc bằng ngày hiệu lực.");
    }
}

public sealed class UpsertPolicyCommandHandler : IRequestHandler<UpsertPolicyCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ICurrentUserContext _user;

    public UpsertPolicyCommandHandler(ILcmsDbContext db, ITenantContext tenant, ICurrentUserContext user)
    {
        _db = db;
        _tenant = tenant;
        _user = user;
    }

    public async Task<Guid> Handle(UpsertPolicyCommand request, CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenant.TenantId!.Value;
        var key = request.PolicyKey.Trim().ToUpperInvariant();
        var status = request.Status.Trim().ToLowerInvariant();
        var title = string.IsNullOrWhiteSpace(request.Title)
            ? (PolicyKeys.DefaultTitles.TryGetValue(key, out var t) ? t : key)
            : request.Title.Trim();

        if (request.CreateNewVersion)
        {
            var currentMax = await _db.Policies
                .Where(p => p.PolicyKey == key)
                .Select(p => (int?)p.Version)
                .MaxAsync(cancellationToken) ?? 0;

            if (status == PolicyStatuses.Active)
            {
                var actives = await _db.Policies
                    .Where(p => p.PolicyKey == key && p.Status == PolicyStatuses.Active)
                    .ToListAsync(cancellationToken);
                foreach (var row in actives)
                {
                    row.Status = PolicyStatuses.Superseded;
                    row.EffectiveTo ??= request.EffectiveFrom.AddDays(-1);
                }
            }

            var created = new Policy
            {
                TenantId = tenantId,
                PolicyKey = key,
                Title = title,
                OwnerUserId = request.OwnerUserId ?? _user.UserId,
                Version = currentMax + 1,
                EffectiveFrom = request.EffectiveFrom,
                EffectiveTo = request.EffectiveTo,
                Status = status,
                BodyJson = string.IsNullOrWhiteSpace(request.BodyJson) ? "{}" : request.BodyJson.Trim(),
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
            };
            _db.Policies.Add(created);
            await _db.SaveChangesAsync(cancellationToken);
            return created.Id;
        }

        var latest = await _db.Policies
            .Where(p => p.PolicyKey == key)
            .OrderByDescending(p => p.Version)
            .FirstOrDefaultAsync(cancellationToken);
        if (latest is null)
        {
            throw new NotFoundAppException("Không tìm thấy chính sách. Hãy khởi tạo sổ trước.");
        }

        if (status == PolicyStatuses.Active && latest.Status != PolicyStatuses.Active)
        {
            var actives = await _db.Policies
                .Where(p => p.PolicyKey == key && p.Status == PolicyStatuses.Active && p.Id != latest.Id)
                .ToListAsync(cancellationToken);
            foreach (var row in actives)
            {
                row.Status = PolicyStatuses.Superseded;
                row.EffectiveTo ??= request.EffectiveFrom.AddDays(-1);
            }
        }

        latest.Title = title;
        latest.OwnerUserId = request.OwnerUserId ?? latest.OwnerUserId ?? _user.UserId;
        latest.EffectiveFrom = request.EffectiveFrom;
        latest.EffectiveTo = request.EffectiveTo;
        latest.Status = status;
        latest.BodyJson = string.IsNullOrWhiteSpace(request.BodyJson) ? latest.BodyJson ?? "{}" : request.BodyJson.Trim();
        latest.Notes = string.IsNullOrWhiteSpace(request.Notes) ? latest.Notes : request.Notes.Trim();
        await _db.SaveChangesAsync(cancellationToken);
        return latest.Id;
    }
}
