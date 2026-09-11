using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Exceptions.Commands;

public sealed record OpenExceptionCommand(
    string RuleCode,
    string Severity,
    string Title,
    string? Description,
    Guid? OwnerId,
    DateTimeOffset? DueAt,
    Guid? BillId,
    Guid? ReconciliationId,
    Guid? VarianceId) : IRequest<Guid>;

public sealed class OpenExceptionCommandValidator : AbstractValidator<OpenExceptionCommand>
{
    private static readonly HashSet<string> Severities = new(StringComparer.OrdinalIgnoreCase)
    {
        ExceptionSeverities.Low,
        ExceptionSeverities.Medium,
        ExceptionSeverities.High,
        ExceptionSeverities.Critical
    };

    public OpenExceptionCommandValidator()
    {
        RuleFor(x => x.RuleCode)
            .NotEmpty().WithMessage("Mã quy tắc ngoại lệ không được để trống.")
            .MaximumLength(128).WithMessage("Mã quy tắc ngoại lệ tối đa 128 ký tự.");
        RuleFor(x => x.Severity)
            .NotEmpty().WithMessage("Mức độ nghiêm trọng không được để trống.")
            .Must(s => Severities.Contains(s.Trim()))
            .WithMessage("Mức độ nghiêm trọng không hợp lệ (low|medium|high|critical).");
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Tiêu đề ngoại lệ không được để trống.")
            .MaximumLength(256).WithMessage("Tiêu đề ngoại lệ tối đa 256 ký tự.");
        RuleFor(x => x.Description).MaximumLength(4096).When(x => x.Description is not null);
    }
}

/// <summary>
/// Opens an exception work item (severity/owner/SLA). Does not invent Variance (Variance ≠ Exception).
/// </summary>
public sealed class OpenExceptionCommandHandler : IRequestHandler<OpenExceptionCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public OpenExceptionCommandHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<Guid> Handle(OpenExceptionCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;

        if (request.BillId.HasValue)
        {
            var billExists = await _db.Bills.AsNoTracking().AnyAsync(b => b.Id == request.BillId, cancellationToken);
            if (!billExists)
            {
                throw new NotFoundAppException("Không tìm thấy Bill.");
            }
        }

        if (request.ReconciliationId.HasValue)
        {
            var reconExists = await _db.Reconciliations.AsNoTracking()
                .AnyAsync(r => r.Id == request.ReconciliationId, cancellationToken);
            if (!reconExists)
            {
                throw new NotFoundAppException("Không tìm thấy phiên đối soát.");
            }
        }

        Variance? variance = null;
        if (request.VarianceId.HasValue)
        {
            variance = await _db.Variances
                .FirstOrDefaultAsync(v => v.Id == request.VarianceId, cancellationToken)
                ?? throw new NotFoundAppException("Không tìm thấy chênh lệch.");
        }

        var varianceCountBefore = await _db.Variances.CountAsync(cancellationToken);

        var entity = new FinancialException
        {
            TenantId = tenantId,
            RuleCode = request.RuleCode.Trim(),
            Severity = request.Severity.Trim().ToLowerInvariant(),
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            OwnerId = request.OwnerId,
            DueAt = request.DueAt,
            BillId = request.BillId,
            ReconciliationId = request.ReconciliationId,
            VarianceId = request.VarianceId,
            Status = ExceptionStatuses.Open
        };

        _db.Exceptions.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        if (variance is not null)
        {
            variance.ExceptionId = entity.Id;
            await _db.SaveChangesAsync(cancellationToken);
        }

        var varianceCountAfter = await _db.Variances.CountAsync(cancellationToken);
        if (varianceCountAfter != varianceCountBefore)
        {
            throw new ConflictAppException(
                "Mở Ngoại lệ không được tạo Chênh lệch mới — Chênh lệch ≠ Ngoại lệ.");
        }

        return entity.Id;
    }
}
