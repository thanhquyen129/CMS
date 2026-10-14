using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using LCMS.Domain.Pricing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.RateCards.Commands;

public sealed record BasicRateRuleInput(
    string Code,
    string Name,
    string CalcMethod,
    decimal UnitAmount,
    string CurrencyCode,
    string? ServiceTypeCode,
    string? CommodityCode,
    string? TransportMode,
    string? PartyTypeCode,
    string? Applicability,
    decimal? MinAmount,
    decimal? MaxAmount,
    string? RouteCode = null);

public sealed record SaveRateCardDeclarationCommand(
    Guid? RateCardId,
    string? Code,
    string Name,
    string PartyType,
    string CurrencyCode,
    string? Description,
    string? TransportMode,
    Guid? RouteId,
    string? CarrierName,
    DateTimeOffset? EffectiveFrom,
    string? Note,
    decimal? VatRate,
    Guid? SupplierPartyId,
    Guid? CustomerPartyId,
    string? CustomerGroupCode,
    string DeclarationMode,
    bool ConfirmModeChange,
    IReadOnlyList<BasicRateRuleInput> Rules,
    IReadOnlyList<ComposeTariffColumn> Columns,
    IReadOnlyList<ComposeTariffBand> Bands,
    decimal? MinimumQuantity,
    ComposeDeliveryFee? Delivery,
    ComposeRemoteFee? Remote,
    Guid? RateVersionId,
    string? RowVersion = null) : IRequest<ComposeTariffResult>;

public sealed class SaveRateCardDeclarationCommandValidator : AbstractValidator<SaveRateCardDeclarationCommand>
{
    public SaveRateCardDeclarationCommandValidator()
    {
        RuleFor(x => x.DeclarationMode).Must(RateDeclarationModes.IsKnown).WithMessage(RateDeclarationPolicy.UnknownModeMessage);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64).When(x => x.RateCardId is null);
        RuleFor(x => x.PartyType).Must(p => p is "customer" or "vendor").WithMessage("Loại giá phải là giá mua hoặc giá bán.");
        RuleFor(x => x.CurrencyCode).NotEmpty().Length(3);
        RuleFor(x => x.VatRate).Must(DeclaredVat.IsValid).WithMessage("Thuế suất VAT phải từ 0 đến 100, hoặc để trống nếu chưa khai báo.");
        RuleFor(x => x.CustomerGroupCode).MaximumLength(64).When(x => x.CustomerGroupCode is not null);
        RuleFor(x => x).Must(x => !RateDeclarationPolicy.IsDual(x.Rules.Count > 0, x.Columns.Count > 0 || x.Bands.Count > 0))
            .WithMessage(RateDeclarationPolicy.DualPayloadMessage);
    }
}

public sealed class SaveRateCardDeclarationCommandHandler : IRequestHandler<SaveRateCardDeclarationCommand, ComposeTariffResult>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;
    private readonly IAuditWriter _audit;
    private readonly ISender _sender;
    private readonly IRowVersionGuard _versions;

    public SaveRateCardDeclarationCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions,
        IAuditWriter audit,
        ISender sender,
        IRowVersionGuard versions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
        _audit = audit;
        _sender = sender;
        _versions = versions;
    }

    public async Task<ComposeTariffResult> Handle(SaveRateCardDeclarationCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var hasMatrixPayload = request.Columns.Count > 0 || request.Bands.Count > 0;
        RateCardCatalog.RejectDual(request.Rules.Count > 0, hasMatrixPayload);
        var mode = request.DeclarationMode.Trim().ToLowerInvariant();
        if (mode == RateDeclarationModes.Basic && hasMatrixPayload)
        {
            throw new ConflictAppException(RateDeclarationPolicy.DualPayloadMessage, "invalid_declaration_mode");
        }

        if (mode == RateDeclarationModes.Advanced && request.Rules.Count > 0)
        {
            throw new ConflictAppException(RateDeclarationPolicy.DualPayloadMessage, "invalid_declaration_mode");
        }

        if (mode == RateDeclarationModes.Advanced && request.RateCardId is null)
        {
            return await _sender.Send(MapCompose(request, null), cancellationToken);
        }

        var tenantId = _tenantContext.TenantId!.Value;
        var card = await LoadOrCreateCardAsync(request, tenantId, cancellationToken);
        if (request.RateCardId is not null)
        {
            _versions.EnsureCurrent(card, request.RowVersion);
        }

        var version = await LoadDraftAsync(card, request, tenantId, cancellationToken);
        var previousMode = card.DeclarationMode;
        await ApplyHeaderAsync(card, version, request, tenantId, cancellationToken);

        var draftRules = await _db.PricingRules.Where(r => r.RateVersionId == version.Id).ToListAsync(cancellationToken);
        var hasMatrix = draftRules.Any(r => RateDeclarationPolicy.IsMatrixMethod(r.CalcMethod));
        var hasBasic = draftRules.Any(r => !RateDeclarationPolicy.IsMatrixMethod(r.CalcMethod));
        if (hasMatrix && hasBasic)
        {
            throw new ConflictAppException(RateDeclarationPolicy.MixedLegacyMessage, "invalid_declaration_mode");
        }

        var hasPublished = await _db.RateVersions.AnyAsync(
            v => v.RateCardId == card.Id && v.Status == RateVersionStatuses.Published,
            cancellationToken);
        var otherShape = mode == RateDeclarationModes.Basic ? hasMatrix : hasBasic;
        var decision = RateDeclarationPolicy.DecideMode(previousMode, mode, request.ConfirmModeChange, hasPublished, otherShape);
        if (decision.BlockMessage is not null)
        {
            throw new ConflictAppException(decision.BlockMessage, "invalid_declaration_mode");
        }

        if (decision.RetireOtherShape)
        {
            var other = draftRules.Where(r => RateDeclarationPolicy.IsMatrixMethod(r.CalcMethod) != (mode == RateDeclarationModes.Advanced)).ToList();
            await RateCardCatalog.RetireRulesAsync(_db, other, cancellationToken);
        }

        card.DeclarationMode = decision.Mode;
        if (mode == RateDeclarationModes.Advanced)
        {
            return await _sender.Send(MapCompose(request, version.Id) with { ReplaceExistingMatrix = true }, cancellationToken);
        }

        await ReplaceBasicRulesAsync(card, version, request.Rules, tenantId, cancellationToken);
        _audit.Append(
            AuditActions.RateCardDeclaration,
            AuditObjectTypes.RateCard,
            card.Id,
            previousMode,
            RateDeclarationModes.Basic,
            null);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ConflictAppException("Không lưu được bảng giá. Mã bảng hoặc mã quy tắc đã tồn tại.");
        }

        return new ComposeTariffResult(card.Id, version.Id);
    }

    private async Task<RateCard> LoadOrCreateCardAsync(
        SaveRateCardDeclarationCommand request,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (request.RateCardId is Guid id)
        {
            return await _db.RateCards.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
                ?? throw new NotFoundAppException("Không tìm thấy bảng giá.");
        }

        var partyType = request.PartyType.Trim().ToLowerInvariant();
        await EnsureWriteAsync(partyType, cancellationToken);
        var code = request.Code!.Trim();
        if (await _db.RateCards.AnyAsync(r => r.TenantId == tenantId && r.Code == code, cancellationToken))
        {
            throw new ConflictAppException("Mã bảng giá đã tồn tại trong thuê bao này.");
        }

        var card = new RateCard
        {
            TenantId = tenantId,
            Code = code,
            Name = request.Name.Trim(),
            PartyType = partyType,
            CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant(),
            IsActive = true
        };
        _db.RateCards.Add(card);
        return card;
    }

    private async Task<RateVersion> LoadDraftAsync(
        RateCard card,
        SaveRateCardDeclarationCommand request,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (request.RateVersionId is Guid versionId)
        {
            var chosen = await _db.RateVersions.FirstOrDefaultAsync(
                v => v.Id == versionId && v.RateCardId == card.Id,
                cancellationToken) ?? throw new NotFoundAppException("Không tìm thấy phiên bản bảng giá.");
            if (chosen.IsPublished)
            {
                throw new ConflictAppException("Phiên bản đã phát hành không được sửa. Hãy tạo phiên bản mới.");
            }

            return chosen;
        }

        var draft = await _db.RateVersions
            .Where(v => v.RateCardId == card.Id && v.Status == RateVersionStatuses.Draft)
            .OrderByDescending(v => v.VersionNo)
            .FirstOrDefaultAsync(cancellationToken);
        if (draft is not null)
        {
            return draft;
        }

        if (request.RateCardId is not null)
        {
            throw new ConflictAppException("Phiên bản đã phát hành không được sửa. Hãy tạo phiên bản mới.");
        }

        var created = new RateVersion
        {
            TenantId = tenantId,
            RateCardId = card.Id,
            VersionNo = 1,
            Status = RateVersionStatuses.Draft
        };
        _db.RateVersions.Add(created);
        return created;
    }

    private async Task ApplyHeaderAsync(
        RateCard card,
        RateVersion version,
        SaveRateCardDeclarationCommand request,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var partyType = request.PartyType.Trim().ToLowerInvariant();
        await EnsureWriteAsync(partyType, cancellationToken);
        if (!PricingPartnerRules.MatchesDirection(partyType, request.SupplierPartyId, request.CustomerPartyId, request.CustomerGroupCode))
        {
            throw new ConflictAppException("Giá mua chỉ gắn nhà cung cấp. Giá bán gắn khách hàng hoặc nhóm khách hàng, không cả hai.");
        }

        await PricingPartnerRules.EnsureRoleOnChangeAsync(
            _db,
            request.SupplierPartyId,
            request.CustomerPartyId,
            card.SupplierPartyId,
            card.CustomerPartyId,
            cancellationToken);
        var nextCurrency = request.CurrencyCode.Trim().ToUpperInvariant();
        var unchangedCurrency = request.RateCardId is null ? null : card.CurrencyCode;
        await RateCardCatalog.EnsureCurrencyAsync(_db, nextCurrency, unchangedCurrency, cancellationToken);
        card.Name = request.Name.Trim();
        card.PartyType = partyType;
        card.CurrencyCode = nextCurrency;
        card.Description = Clean(request.Description);
        card.CarrierName = Clean(request.CarrierName);
        if (!string.IsNullOrWhiteSpace(request.TransportMode))
        {
            card.TransportMode = request.TransportMode.Trim().ToLowerInvariant();
        }

        card.SupplierPartyId = request.SupplierPartyId;
        card.CustomerPartyId = request.CustomerPartyId;
        card.CustomerGroupCode = Clean(request.CustomerGroupCode);
        await RateCardCatalog.ApplyRouteAsync(_db, card, request.RouteId, tenantId, cancellationToken);
        version.EffectiveFrom = request.EffectiveFrom;
        version.Note = Clean(request.Note);
        version.VatRate = request.VatRate;
    }

    private async Task ReplaceBasicRulesAsync(
        RateCard card,
        RateVersion version,
        IReadOnlyList<BasicRateRuleInput> rules,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var normalized = new List<BasicRateRuleInput>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in rules)
        {
            var code = rule.Code.Trim();
            var name = rule.Name.Trim();
            var method = rule.CalcMethod.Trim().ToLowerInvariant();
            if (code.Length == 0 || name.Length == 0)
            {
                throw new ConflictAppException("Nhập mã và tên quy tắc.");
            }

            if (!seen.Add(code))
            {
                throw new ConflictAppException("Mã quy tắc bị trùng trên bảng giá.");
            }

            if (RateDeclarationPolicy.IsMatrixMethod(method) || !PricingCalcMethods.All.Contains(method))
            {
                throw new ConflictAppException("Cách tính không có trong cấu hình tính giá hiện hành.");
            }

            if (rule.UnitAmount < 0 || rule.MinAmount < 0 || rule.MaxAmount < 0)
            {
                throw new ConflictAppException("Số tiền / đơn giá không được âm.", "invalid_amount");
            }

            if (method == PricingCalcMethods.MinMaxClamp && rule.MinAmount is null && rule.MaxAmount is null)
            {
                throw new ConflictAppException("Cách tính kẹp min/max cần số tiền tối thiểu hoặc tối đa.");
            }

            if (rule.MinAmount is decimal min && rule.MaxAmount is decimal max && min > max)
            {
                throw new ConflictAppException("Số tiền tối thiểu không được lớn hơn số tiền tối đa.");
            }

            await RateCardCatalog.EnsureCurrencyAsync(_db, rule.CurrencyCode, null, cancellationToken);
            normalized.Add(rule with
            {
                Code = code,
                Name = name,
                CalcMethod = method,
                CurrencyCode = rule.CurrencyCode.Trim().ToUpperInvariant()
            });
        }

        var existing = await _db.PricingRules.Where(r => r.RateVersionId == version.Id).ToListAsync(cancellationToken);
        await RateCardCatalog.RetireRulesAsync(
            _db,
            existing.Where(r => !RateDeclarationPolicy.IsMatrixMethod(r.CalcMethod)).ToList(),
            cancellationToken);

        var sort = 10;
        foreach (var rule in normalized)
        {
            _db.PricingRules.Add(new PricingRule
            {
                TenantId = tenantId,
                RateVersionId = version.Id,
                Code = rule.Code,
                Name = rule.Name,
                CalcMethod = rule.CalcMethod,
                UnitAmount = rule.UnitAmount,
                CurrencyCode = rule.CurrencyCode,
                Applicability = Clean(rule.Applicability),
                ServiceTypeCode = Clean(rule.ServiceTypeCode),
                PartyTypeCode = Clean(rule.PartyTypeCode)?.ToLowerInvariant(),
                CommodityCode = Clean(rule.CommodityCode)?.ToUpperInvariant(),
                TransportMode = Clean(rule.TransportMode)?.ToLowerInvariant(),
                RouteCode = Clean(rule.RouteCode),
                MinAmount = rule.MinAmount,
                MaxAmount = rule.MaxAmount,
                SortOrder = sort,
                IsActive = true
            });
            sort += 10;
        }
    }

    private static ComposeTariffCommand MapCompose(SaveRateCardDeclarationCommand request, Guid? versionId) =>
        new(
            request.Code,
            request.Name,
            request.PartyType,
            request.CurrencyCode,
            request.Description,
            request.TransportMode,
            null,
            request.CarrierName,
            request.EffectiveFrom,
            request.Note,
            versionId,
            request.MinimumQuantity,
            request.Columns,
            request.Bands,
            request.Delivery,
            request.Remote,
            request.SupplierPartyId,
            request.CustomerPartyId,
            request.CustomerGroupCode,
            request.VatRate,
            request.RouteId,
            versionId is not null);

    private async Task EnsureWriteAsync(string partyType, CancellationToken cancellationToken)
    {
        var writeCode = partyType == "customer" ? PermissionCodes.RateSellWrite : PermissionCodes.RateBuyWrite;
        await _permissions.EnsureAsync(writeCode, "Bạn không có quyền sửa bảng giá này.", cancellationToken);
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
