using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LCMS.Application.FinancialControl;

/// <summary>
/// Optional block on Cost/Revenue confirm when a critical exception is still open on the same object.
/// </summary>
public interface ICriticalExceptionConfirmGate
{
    Task EnsureConfirmAllowedAsync(string objectType, Guid objectId, CancellationToken cancellationToken);
}

public sealed class CriticalExceptionConfirmGate : ICriticalExceptionConfirmGate
{
    private static readonly HashSet<string> BlockingStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        ExceptionStatuses.Open,
        ExceptionStatuses.InProgress,
        ExceptionStatuses.Escalated
    };

    private readonly ILcmsDbContext _db;
    private readonly FinancialControlOptions _options;

    public CriticalExceptionConfirmGate(ILcmsDbContext db, IOptions<FinancialControlOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task EnsureConfirmAllowedAsync(
        string objectType,
        Guid objectId,
        CancellationToken cancellationToken)
    {
        if (!_options.BlockConfirmOnCriticalException)
        {
            return;
        }

        var type = objectType.Trim().ToLowerInvariant();
        var hasCritical = await _db.Exceptions.AsNoTracking()
            .AnyAsync(
                e => e.ObjectType == type
                     && e.ObjectId == objectId
                     && e.Severity == ExceptionSeverities.Critical
                     && BlockingStatuses.Contains(e.Status),
                cancellationToken);

        if (hasCritical)
        {
            throw new ConflictAppException(
                "Không thể xác nhận khi còn ngoại lệ nghiêm trọng (critical) đang mở trên đối tượng này.");
        }
    }
}
