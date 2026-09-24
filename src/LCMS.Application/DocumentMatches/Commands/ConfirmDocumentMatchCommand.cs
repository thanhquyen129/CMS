using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Audit;
using LCMS.Application.Common;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.DocumentMatches.Commands;

/// <summary>
/// Confirms a draft match session (locks adding details). Does not invent Cost/Revenue.
/// </summary>
public sealed record ConfirmDocumentMatchCommand(Guid MatchId, string? IfMatch = null) : IRequest;

public sealed class ConfirmDocumentMatchCommandValidator : AbstractValidator<ConfirmDocumentMatchCommand>
{
    public ConfirmDocumentMatchCommandValidator()
    {
        RuleFor(x => x.MatchId).NotEmpty().WithMessage("Phiên khớp không hợp lệ.");
    }
}

public sealed class ConfirmDocumentMatchCommandHandler : IRequestHandler<ConfirmDocumentMatchCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;
    private readonly IAuditWriter _audit;
    private readonly IRowVersionGuard _versions;

    public ConfirmDocumentMatchCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user,
        IAuditWriter audit,
        IRowVersionGuard versions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
        _audit = audit;
        _versions = versions;
    }

    public async Task Handle(ConfirmDocumentMatchCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var match = await _db.DocumentMatches
            .FirstOrDefaultAsync(m => m.Id == request.MatchId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy phiên khớp chứng từ.");
        _versions.EnsureCurrent(match, request.IfMatch);

        if (string.Equals(match.MatchStatus, DocumentMatchStatuses.Confirmed, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Phiên khớp đã được xác nhận.");
        }

        if (string.Equals(match.MatchStatus, DocumentMatchStatuses.Cancelled, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Không xác nhận phiên khớp đã hủy.");
        }

        if (!string.Equals(match.MatchStatus, DocumentMatchStatuses.Draft, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Chỉ xác nhận phiên khớp ở trạng thái nháp.");
        }

        var hasActiveDetails = await _db.DocumentMatchDetails.AsNoTracking()
            .AnyAsync(
                d => d.MatchId == match.Id && d.DetailStatus == DocumentMatchDetailStatuses.Active,
                cancellationToken);
        if (!hasActiveDetails)
        {
            throw new ConflictAppException("Cần ít nhất một chi tiết khớp hiệu lực trước khi xác nhận phiên.");
        }

        var beforeJson = AuditJson.Serialize(new
        {
            id = match.Id,
            matchStatus = match.MatchStatus,
            matchMethod = match.MatchMethod,
            primaryDocumentId = match.PrimaryDocumentId,
            versionNo = match.VersionNo
        });

        match.MatchStatus = DocumentMatchStatuses.Confirmed;
        match.ConfirmedAt = DateTimeOffset.UtcNow;
        match.ConfirmedBy = _user.UserId;

        _audit.Append(
            AuditActions.DocumentMatchConfirm,
            AuditObjectTypes.DocumentMatch,
            match.Id,
            beforeJson: beforeJson,
            afterJson: AuditJson.Serialize(new
            {
                id = match.Id,
                matchStatus = DocumentMatchStatuses.Confirmed,
                matchMethod = match.MatchMethod,
                primaryDocumentId = match.PrimaryDocumentId,
                versionNo = match.VersionNo,
                confirmedAt = match.ConfirmedAt
            }));

        await _db.SaveChangesAsync(cancellationToken);
    }
}
