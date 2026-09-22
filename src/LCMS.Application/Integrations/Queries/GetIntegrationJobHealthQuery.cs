using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Integrations.Queries;

public sealed record IntegrationJobHealthDto(
    int OutboxPending,
    int IntegrationErrorsPending,
    int IntegrationErrorsDeadLetter,
    IReadOnlyList<IntegrationActionableErrorDto> TopActionableErrors);

public sealed record IntegrationActionableErrorDto(
    Guid Id,
    string ErrorCode,
    string Message,
    string? NextAction,
    DateTimeOffset OccurredAt,
    DateTimeOffset? NextRetryAt);

public sealed record GetIntegrationJobHealthQuery : IRequest<IntegrationJobHealthDto>;

public sealed class GetIntegrationJobHealthQueryHandler
    : IRequestHandler<GetIntegrationJobHealthQuery, IntegrationJobHealthDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;

    public GetIntegrationJobHealthQueryHandler(ILcmsDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IntegrationJobHealthDto> Handle(
        GetIntegrationJobHealthQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var outboxPending = await _db.OutboxMessages.AsNoTracking()
            .CountAsync(m => m.Status == OutboxMessageStatuses.Pending, cancellationToken);
        var errorsPending = await _db.IntegrationErrors.AsNoTracking()
            .CountAsync(e => e.RecoveryStatus == IntegrationErrorRecoveryStatuses.Pending, cancellationToken);
        var deadLetter = await _db.IntegrationErrors.AsNoTracking()
            .CountAsync(e => e.RecoveryStatus == IntegrationErrorRecoveryStatuses.DeadLetter, cancellationToken);

        var top = await _db.IntegrationErrors.AsNoTracking()
            .Where(e => e.RecoveryStatus == IntegrationErrorRecoveryStatuses.Pending
                        || e.RecoveryStatus == IntegrationErrorRecoveryStatuses.DeadLetter)
            .OrderByDescending(e => e.Id)
            .Take(10)
            .Select(e => new { e.Id, e.ErrorCode, e.Message, e.OccurredAt, e.NextRetryAt, e.RecoveryStatus })
            .ToListAsync(cancellationToken);

        var actionable = top.Select(e => new IntegrationActionableErrorDto(
            e.Id,
            e.ErrorCode,
            e.Message,
            NextAction(e.ErrorCode, e.RecoveryStatus),
            e.OccurredAt,
            e.NextRetryAt)).ToList();

        return new IntegrationJobHealthDto(outboxPending, errorsPending, deadLetter, actionable);
    }

    private static string NextAction(string errorCode, string recoveryStatus)
    {
        if (string.Equals(recoveryStatus, IntegrationErrorRecoveryStatuses.DeadLetter, StringComparison.OrdinalIgnoreCase))
        {
            return "Mở hàng đợi lỗi → dead-letter → ghi chú và reprocess hoặc bỏ qua có kiểm soát.";
        }

        return errorCode.ToUpperInvariant() switch
        {
            "AUTH_FAILED" or "UNAUTHORIZED" => "Kiểm tra credential tích hợp (không dán secret vào ghi chú). Thử lại sau khi xoay khóa.",
            "TIMEOUT" or "UNAVAILABLE" => "Nguồn ngoài tạm thời; đợi NextRetryAt rồi Mark retried.",
            "VALIDATION" or "SCHEMA" => "Sửa payload nguồn; không retry mù.",
            "DUPLICATE" => "Đã ghi nhận idempotent — xác nhận local object rồi Mark retried.",
            _ => "Xem chi tiết lỗi (đã che secret) → sửa nguyên nhân → Mark retried hoặc Dead-letter."
        };
    }
}
