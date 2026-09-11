using LCMS.Application.Abstractions;
using LCMS.Domain.Entities;

namespace LCMS.Infrastructure.Audit;

public sealed class AuditWriter : IAuditWriter
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;
    private readonly ICorrelationContext _correlation;

    public AuditWriter(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user,
        ICorrelationContext correlation)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
        _correlation = correlation;
    }

    public void Append(
        string action,
        string objectType,
        Guid objectId,
        string? beforeJson = null,
        string? afterJson = null,
        string? reason = null)
    {
        if (!_tenantContext.HasTenant)
        {
            return;
        }

        _db.AuditEvents.Add(new AuditEvent
        {
            TenantId = _tenantContext.TenantId!.Value,
            ActorId = _user.UserId,
            Action = action,
            ObjectType = objectType,
            ObjectId = objectId,
            BeforeJson = beforeJson,
            AfterJson = afterJson,
            Reason = reason,
            CorrelationId = _correlation.CorrelationId,
            OccurredAt = DateTimeOffset.UtcNow
        });
    }
}
