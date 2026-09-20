using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.PartyContacts.Commands;

public sealed record UpsertPartyContactCommand(
    Guid PartyId,
    Guid? Id,
    string FullName,
    string? Title,
    string? FunctionCode,
    string? Phone,
    string? Email,
    bool IsPrimary,
    bool IsActive,
    string? Note) : IRequest<Guid>;

public sealed class UpsertPartyContactCommandValidator : AbstractValidator<UpsertPartyContactCommand>
{
    public UpsertPartyContactCommandValidator()
    {
        RuleFor(x => x.PartyId).NotEmpty().WithMessage("Đối tác không hợp lệ.");
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Tên người liên hệ không được để trống.")
            .MaximumLength(256);
        RuleFor(x => x.Title).MaximumLength(128);
        RuleFor(x => x.FunctionCode)
            .Must(c => string.IsNullOrWhiteSpace(c) || PartyContactFunctions.IsKnown(c))
            .WithMessage("Chức năng liên hệ phải là general, billing, ops hoặc legal.");
        RuleFor(x => x.Phone).MaximumLength(64);
        RuleFor(x => x.Email).MaximumLength(256);
        RuleFor(x => x.Note).MaximumLength(512);
    }
}

public sealed class UpsertPartyContactCommandHandler : IRequestHandler<UpsertPartyContactCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;

    public UpsertPartyContactCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
    }

    public async Task<Guid> Handle(UpsertPartyContactCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(
            PermissionCodes.MasterPartyManage,
            "Bạn không có quyền quản lý đối tác.",
            cancellationToken);

        var tenantId = _tenantContext.TenantId!.Value;
        var party = await _db.BusinessParties.FirstOrDefaultAsync(p => p.Id == request.PartyId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy đối tác.");

        PartyContact row;
        if (request.Id is Guid id)
        {
            row = await _db.PartyContacts.FirstOrDefaultAsync(
                    c => c.Id == id && c.PartyId == party.Id,
                    cancellationToken)
                ?? throw new NotFoundAppException("Không tìm thấy người liên hệ.");
        }
        else
        {
            row = new PartyContact
            {
                TenantId = tenantId,
                PartyId = party.Id
            };
            _db.PartyContacts.Add(row);
        }

        row.FullName = request.FullName.Trim();
        row.Title = string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim();
        row.FunctionCode = string.IsNullOrWhiteSpace(request.FunctionCode)
            ? PartyContactFunctions.General
            : request.FunctionCode.Trim().ToLowerInvariant();
        row.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        row.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        row.IsPrimary = request.IsPrimary;
        row.IsActive = request.IsActive;
        row.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

        if (row.IsPrimary)
        {
            var others = await _db.PartyContacts
                .Where(c => c.PartyId == party.Id && c.Id != row.Id && c.IsPrimary)
                .ToListAsync(cancellationToken);
            foreach (var other in others)
            {
                other.IsPrimary = false;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return row.Id;
    }
}

public sealed record SoftDeletePartyContactCommand(Guid PartyId, Guid ContactId) : IRequest;

public sealed class SoftDeletePartyContactCommandHandler : IRequestHandler<SoftDeletePartyContactCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;
    private readonly ICurrentUserContext _userContext;

    public SoftDeletePartyContactCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions,
        ICurrentUserContext userContext)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
        _userContext = userContext;
    }

    public async Task Handle(SoftDeletePartyContactCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(
            PermissionCodes.MasterPartyManage,
            "Bạn không có quyền quản lý đối tác.",
            cancellationToken);

        var row = await _db.PartyContacts.FirstOrDefaultAsync(
                c => c.Id == request.ContactId && c.PartyId == request.PartyId,
                cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy người liên hệ.");

        row.SoftDelete(_userContext.UserId);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
