using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Integrations.Commands;

/// <summary>
/// Upsert stub for integration_records. First insert succeeds; duplicate C-002 key → Conflict (safe reject).
/// Recovery/retry: see integration_errors mark-retried / dead-letter + optional outbox stub (Sprint 12 FULL).
/// </summary>
public sealed record UpsertIntegrationRecordCommand(
    string SourceSystem,
    string ExternalObjectType,
    string ExternalId,
    string? ExternalVersion,
    string? Status,
    string? LocalObjectType,
    Guid? LocalObjectId,
    string? PayloadHash,
    string? Notes) : IRequest<Guid>;

public sealed class UpsertIntegrationRecordCommandValidator : AbstractValidator<UpsertIntegrationRecordCommand>
{
    public UpsertIntegrationRecordCommandValidator()
    {
        RuleFor(x => x.SourceSystem)
            .NotEmpty().WithMessage("Hệ thống nguồn không được để trống.")
            .MaximumLength(64).WithMessage("Hệ thống nguồn không được vượt quá 64 ký tự.");
        RuleFor(x => x.ExternalObjectType)
            .NotEmpty().WithMessage("Loại đối tượng ngoài không được để trống.")
            .MaximumLength(64).WithMessage("Loại đối tượng ngoài không được vượt quá 64 ký tự.");
        RuleFor(x => x.ExternalId)
            .NotEmpty().WithMessage("Mã tham chiếu ngoài không được để trống.")
            .MaximumLength(128).WithMessage("Mã tham chiếu ngoài không được vượt quá 128 ký tự.");
        RuleFor(x => x.ExternalVersion).MaximumLength(64).When(x => x.ExternalVersion is not null);
        RuleFor(x => x.Status).MaximumLength(32).When(x => x.Status is not null);
        RuleFor(x => x.LocalObjectType).MaximumLength(64).When(x => x.LocalObjectType is not null);
        RuleFor(x => x.PayloadHash).MaximumLength(128).When(x => x.PayloadHash is not null);
        RuleFor(x => x.Notes).MaximumLength(2048).When(x => x.Notes is not null);
    }
}

public sealed class UpsertIntegrationRecordCommandHandler : IRequestHandler<UpsertIntegrationRecordCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public UpsertIntegrationRecordCommandHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<Guid> Handle(UpsertIntegrationRecordCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;
        var sourceSystem = request.SourceSystem.Trim();
        var externalObjectType = request.ExternalObjectType.Trim().ToLowerInvariant();
        var externalId = request.ExternalId.Trim();

        var existing = await _db.IntegrationRecords.AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.TenantId == tenantId
                     && r.SourceSystem == sourceSystem
                     && r.ExternalObjectType == externalObjectType
                     && r.ExternalId == externalId,
                cancellationToken);

        if (existing is not null)
        {
            throw new ConflictAppException(
                "Bản ghi tích hợp với mã tham chiếu ngoài này đã tồn tại trong thuê bao (C-002).");
        }

        var status = string.IsNullOrWhiteSpace(request.Status)
            ? IntegrationRecordStatuses.Received
            : request.Status.Trim().ToLowerInvariant();

        var record = new IntegrationRecord
        {
            TenantId = tenantId,
            SourceSystem = sourceSystem,
            ExternalObjectType = externalObjectType,
            ExternalId = externalId,
            ExternalVersion = string.IsNullOrWhiteSpace(request.ExternalVersion)
                ? null
                : request.ExternalVersion.Trim(),
            Status = status,
            LocalObjectType = string.IsNullOrWhiteSpace(request.LocalObjectType)
                ? null
                : request.LocalObjectType.Trim(),
            LocalObjectId = request.LocalObjectId,
            PayloadHash = string.IsNullOrWhiteSpace(request.PayloadHash) ? null : request.PayloadHash.Trim(),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            ReceivedAt = DateTimeOffset.UtcNow
        };

        _db.IntegrationRecords.Add(record);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ConflictAppException(
                "Bản ghi tích hợp với mã tham chiếu ngoài này đã tồn tại trong thuê bao (C-002).");
        }

        return record.Id;
    }
}
