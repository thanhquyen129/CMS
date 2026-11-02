using System.Text.Json;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;

namespace LCMS.Application.Surcharges;

public sealed record ActivationPredicate(
    string SourceField,
    string Operator,
    string? ExpectedValue,
    string UnknownBehavior = "BLOCK_NEEDS_CONTEXT");

public sealed record ActivationConfig(
    string? ServiceCode,
    string? SelectionScope,
    IReadOnlyList<ActivationPredicate> Predicates,
    string? PolicyNotes = null);

public sealed record ServiceSelectionSnapshot(
    string ContextType,
    Guid ContextId,
    string ServiceCode,
    string Scope,
    decimal Quantity,
    bool IsSelected,
    int Revision,
    Guid? ConfirmedByUserId,
    DateTimeOffset? ConfirmedAt);

public static class SurchargeActivationConfigParser
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static ActivationConfig Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ActivationConfig(null, null, []);
        }

        try
        {
            var dto = JsonSerializer.Deserialize<ActivationConfigDto>(json, Json);
            if (dto is null)
            {
                return new ActivationConfig(null, null, []);
            }

            var predicates = (dto.Predicates ?? [])
                .Where(p => !string.IsNullOrWhiteSpace(p.SourceField))
                .Select(p => new ActivationPredicate(
                    p.SourceField!.Trim(),
                    string.IsNullOrWhiteSpace(p.Operator) ? "eq" : p.Operator.Trim(),
                    p.ExpectedValue,
                    string.IsNullOrWhiteSpace(p.UnknownBehavior) ? "BLOCK_NEEDS_CONTEXT" : p.UnknownBehavior.Trim()))
                .ToList();
            return new ActivationConfig(
                string.IsNullOrWhiteSpace(dto.ServiceCode) ? null : dto.ServiceCode.Trim(),
                string.IsNullOrWhiteSpace(dto.SelectionScope) ? null : dto.SelectionScope.Trim().ToUpperInvariant(),
                predicates,
                dto.PolicyNotes);
        }
        catch (JsonException)
        {
            throw ValidationAppException.Coded(
                "Cấu hình kích hoạt phụ phí không hợp lệ.",
                "ACTIVATION_CONFIG_INVALID");
        }
    }

    public static string Serialize(ActivationConfig config) =>
        JsonSerializer.Serialize(
            new ActivationConfigDto(
                config.ServiceCode,
                config.SelectionScope,
                config.Predicates.Select(p => new ActivationPredicateDto(
                    p.SourceField, p.Operator, p.ExpectedValue, p.UnknownBehavior)).ToList(),
                config.PolicyNotes),
            Json);

    private sealed record ActivationConfigDto(
        string? ServiceCode,
        string? SelectionScope,
        List<ActivationPredicateDto>? Predicates,
        string? PolicyNotes);

    private sealed record ActivationPredicateDto(
        string? SourceField,
        string? Operator,
        string? ExpectedValue,
        string? UnknownBehavior);
}

public static class SurchargeActivationValidator
{
    public static void EnsurePublishable(SurchargeVersion version, ActivationConfig config)
    {
        var policy = Normalize(version.ActivationPolicy);
        if (string.Equals(policy, SurchargeActivationPolicies.UnclassifiedLegacy, StringComparison.OrdinalIgnoreCase))
        {
            throw ValidationAppException.Coded(
                "Không được phát hành phụ phí ở trạng thái UNCLASSIFIED_LEGACY. Hãy chọn chính sách kích hoạt hợp lệ.",
                "ACTIVATION_UNCLASSIFIED");
        }

        if (!SurchargeActivationPolicies.Publishable.Contains(policy))
        {
            throw ValidationAppException.Coded(
                "Phiên bản phát hành phải có chính sách kích hoạt hợp lệ (Bắt buộc / Tự động theo dữ liệu / Tùy chọn).",
                "ACTIVATION_POLICY_REQUIRED");
        }

        if (string.Equals(policy, SurchargeActivationPolicies.MandatoryByPolicy, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(version.ApprovalRef))
            {
                throw ValidationAppException.Coded(
                    "Phụ phí bắt buộc theo chính sách cần mã phê duyệt / căn cứ chính sách (approval_ref).",
                    "MANDATORY_APPROVAL_REQUIRED");
            }
        }

        if (string.Equals(policy, SurchargeActivationPolicies.AutoConditional, StringComparison.OrdinalIgnoreCase))
        {
            if (config.Predicates.Count == 0)
            {
                throw ValidationAppException.Coded(
                    "Chính sách tự động theo dữ liệu cần ít nhất một điều kiện kích hoạt (predicate).",
                    "AUTO_PREDICATE_REQUIRED");
            }
        }

        if (string.Equals(policy, SurchargeActivationPolicies.OptionalOptIn, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(config.ServiceCode))
            {
                throw ValidationAppException.Coded(
                    "Phụ phí tùy chọn cần mã dịch vụ liên kết (service_code).",
                    "OPTIONAL_SERVICE_CODE_REQUIRED");
            }
        }
    }

    public static string Normalize(string? policy)
    {
        var value = (policy ?? "").Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return SurchargeActivationPolicies.UnclassifiedLegacy;
        }

        return value.ToUpperInvariant() switch
        {
            "MANDATORY" or "MANDATORY_BY_POLICY" => SurchargeActivationPolicies.MandatoryByPolicy,
            "AUTO" or "AUTO_CONDITIONAL" => SurchargeActivationPolicies.AutoConditional,
            "OPTIONAL" or "OPTIONAL_OPT_IN" => SurchargeActivationPolicies.OptionalOptIn,
            "UNCLASSIFIED" or "UNCLASSIFIED_LEGACY" or "LEGACY" => SurchargeActivationPolicies.UnclassifiedLegacy,
            _ => value
        };
    }
}

public static class SurchargeActivationResolver
{
    public sealed record Result(
        bool Activated,
        string? Bucket,
        string ReasonCode,
        string ReasonMessageVi,
        string? EvidenceRef,
        decimal? Quantity);

    public static Result Resolve(
        string activationPolicy,
        string? approvalRef,
        ActivationConfig config,
        SurchargeMatchContext ctx,
        IReadOnlyList<ServiceSelectionSnapshot> selections)
    {
        var policy = SurchargeActivationValidator.Normalize(activationPolicy);
        if (string.Equals(policy, SurchargeActivationPolicies.UnclassifiedLegacy, StringComparison.OrdinalIgnoreCase))
        {
            return new Result(
                false,
                "excluded",
                SurchargeReasonCodes.ExcludedUnclassified,
                "Phụ phí lịch sử chưa phân loại chính sách kích hoạt — hệ thống không tự động cộng vào tính giá mới.",
                null,
                null);
        }

        if (string.Equals(policy, SurchargeActivationPolicies.MandatoryByPolicy, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(approvalRef))
            {
                return new Result(
                    false,
                    "excluded",
                    SurchargeReasonCodes.ExcludedUnclassified,
                    "Thiếu căn cứ phê duyệt bắt buộc — không cộng phụ phí.",
                    null,
                    null);
            }

            return new Result(
                true,
                "applied",
                SurchargeReasonCodes.Applied,
                "Bắt buộc theo chính sách đã phê duyệt.",
                approvalRef,
                1m);
        }

        if (string.Equals(policy, SurchargeActivationPolicies.AutoConditional, StringComparison.OrdinalIgnoreCase))
        {
            if (config.Predicates.Count == 0)
            {
                return new Result(
                    false,
                    "eligible_not_activated",
                    SurchargeReasonCodes.NeedsContext,
                    "Chưa cấu hình điều kiện kích hoạt tự động.",
                    null,
                    null);
            }

            foreach (var predicate in config.Predicates)
            {
                var outcome = EvaluatePredicate(predicate, ctx);
                if (outcome is not null)
                {
                    return outcome;
                }
            }

            return new Result(
                true,
                "applied",
                SurchargeReasonCodes.Applied,
                "Đã thỏa điều kiện kích hoạt tự động theo dữ liệu.",
                string.Join("+", config.Predicates.Select(p => p.SourceField)),
                1m);
        }

        if (string.Equals(policy, SurchargeActivationPolicies.OptionalOptIn, StringComparison.OrdinalIgnoreCase))
        {
            var serviceCode = config.ServiceCode ?? string.Empty;
            if (string.IsNullOrWhiteSpace(serviceCode))
            {
                return new Result(
                    false,
                    "eligible_not_activated",
                    SurchargeReasonCodes.NotSelected,
                    "Phụ phí tùy chọn thiếu mã dịch vụ liên kết.",
                    null,
                    null);
            }

            var matches = selections
                .Where(s => string.Equals(s.ServiceCode, serviceCode, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (matches.Count == 0 || matches.All(s => !s.IsSelected))
            {
                return new Result(
                    false,
                    "eligible_not_activated",
                    SurchargeReasonCodes.NotSelected,
                    $"Chưa đăng ký dịch vụ {serviceCode} trên ngữ cảnh tính giá.",
                    null,
                    null);
            }

            var selected = matches.Where(s => s.IsSelected).ToList();
            if (selected.Select(s => s.IsSelected).Distinct().Count() > 1
                || HasConflict(selected))
            {
                return new Result(
                    false,
                    "eligible_not_activated",
                    SurchargeReasonCodes.DataConflict,
                    "Trạng thái dịch vụ xung đột giữa Bill / Shipment / Leg — không tự suy diễn.",
                    null,
                    null);
            }

            var pick = Prefer(selected, config.SelectionScope);
            return new Result(
                true,
                "applied",
                SurchargeReasonCodes.Applied,
                $"Đã lưu xác nhận dịch vụ {serviceCode}.",
                $"{pick.ContextType}:{pick.ContextId}:{pick.Revision}",
                pick.Quantity <= 0 ? 1m : pick.Quantity);
        }

        return new Result(
            false,
            "excluded",
            SurchargeReasonCodes.ExcludedUnclassified,
            "Chính sách kích hoạt không nhận diện được.",
            null,
            null);
    }

    public static IReadOnlyList<string> UnpricedSelectedServices(
        string direction,
        IReadOnlyList<ServiceSelectionSnapshot> selections,
        IReadOnlyList<SurchargeCandidate> eligibleOrApplied)
    {
        var priced = eligibleOrApplied
            .Select(c => SurchargeActivationConfigParser.Parse(c.ActivationConfigJson).ServiceCode ?? c.Code)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return selections
            .Where(s => s.IsSelected)
            .Select(s => s.ServiceCode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(code => !priced.Contains(code))
            .Select(code => $"{SurchargeReasonCodes.Unpriced}:{code}:{direction}")
            .ToList();
    }

    private static Result? EvaluatePredicate(ActivationPredicate predicate, SurchargeMatchContext ctx)
    {
        var actual = ReadField(predicate.SourceField, ctx);
        if (actual is null)
        {
            return new Result(
                false,
                "eligible_not_activated",
                SurchargeReasonCodes.NeedsContext,
                $"Chưa xác định trường dữ liệu {predicate.SourceField} — bổ sung ngữ cảnh trước khi tính phụ phí.",
                predicate.SourceField,
                null);
        }

        var expected = predicate.ExpectedValue?.Trim() ?? "true";
        var op = predicate.Operator.Trim().ToLowerInvariant();
        var hit = op switch
        {
            "neq" or "ne" or "!=" => !string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
            "in" => expected.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(v => string.Equals(v, actual, StringComparison.OrdinalIgnoreCase)),
            _ => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase)
        };

        if (!hit)
        {
            return new Result(
                false,
                "eligible_not_activated",
                SurchargeReasonCodes.NotActivatedConditionFalse,
                $"Điều kiện kích hoạt {predicate.SourceField} không thỏa.",
                predicate.SourceField,
                null);
        }

        return null;
    }

    private static string? ReadField(string sourceField, SurchargeMatchContext ctx)
    {
        var key = sourceField.Trim().ToLowerInvariant().Replace('-', '_');
        return key switch
        {
            "dangerous_goods" or "isdangerousgoods" or "dg" => ctx.DangerousGoods switch
            {
                true => "true",
                false => "false",
                null => null
            },
            "transport_mode" => ctx.TransportMode,
            "service" or "service_type" or "servicetypecode" => ctx.ServiceTypeCode,
            "route" or "routecode" => ctx.RouteCode,
            "origin" => ctx.OriginCode,
            "destination" => ctx.DestinationCode,
            "commodity" => ctx.CommodityCode,
            "delivery_outside_hours" or "deliveryoutsidehours" => ctx.DeliveryOutsideHours switch
            {
                true => "true",
                false => "false",
                null => null
            },
            _ => null
        };
    }

    private static bool HasConflict(IReadOnlyList<ServiceSelectionSnapshot> selected)
    {
        if (selected.Count <= 1)
        {
            return false;
        }

        var quantities = selected.Select(s => s.Quantity).Distinct().Count();
        return quantities > 1;
    }

    private static ServiceSelectionSnapshot Prefer(IReadOnlyList<ServiceSelectionSnapshot> selected, string? preferredScope)
    {
        if (!string.IsNullOrWhiteSpace(preferredScope))
        {
            var match = selected.FirstOrDefault(s =>
                string.Equals(s.Scope, preferredScope, StringComparison.OrdinalIgnoreCase)
                || string.Equals(s.ContextType, preferredScope, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }
        }

        return selected
            .OrderBy(s => ScopeRank(s.ContextType))
            .ThenByDescending(s => s.Revision)
            .First();
    }

    private static int ScopeRank(string contextType) =>
        contextType.ToUpperInvariant() switch
        {
            "BILL" => 0,
            "LEG" => 1,
            "SHIPMENT" => 2,
            _ => 9
        };
}

public static class SurchargeActivationPermissions
{
    public static async Task EnsureMandatoryApproveAsync(
        Abstractions.IPermissionService permissions,
        CancellationToken cancellationToken) =>
        await permissions.EnsureAsync(
            PermissionCodes.RateSurchargeMandatoryApprove,
            "Bạn không có quyền duyệt phụ phí bắt buộc theo chính sách.",
            cancellationToken);

    public static async Task EnsureServiceSelectAsync(
        Abstractions.IPermissionService permissions,
        CancellationToken cancellationToken) =>
        await permissions.EnsureAsync(
            PermissionCodes.RateServiceSelect,
            "Bạn không có quyền chọn dịch vụ phụ phí tùy chọn.",
            cancellationToken);
}
