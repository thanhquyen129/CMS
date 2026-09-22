using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.OperationalReferences;

public sealed record AddCargoPackageCommand(
    string ObjectType,
    Guid ObjectId,
    int SequenceNo,
    int PackageCount,
    decimal? LengthCm = null,
    decimal? WidthCm = null,
    decimal? HeightCm = null,
    decimal? WeightKg = null) : IRequest<Guid>;

public sealed class AddCargoPackageCommandValidator : AbstractValidator<AddCargoPackageCommand>
{
    public AddCargoPackageCommandValidator()
    {
        RuleFor(x => x.ObjectId).NotEmpty();
        RuleFor(x => x.SequenceNo).GreaterThan(0);
        RuleFor(x => x.PackageCount).GreaterThan(0);
    }
}

public sealed record AddCargoContainerCommand(
    string ObjectType,
    Guid ObjectId,
    int SequenceNo,
    string ContainerType,
    int Quantity,
    decimal Teu,
    string? ContainerNo = null) : IRequest<Guid>;

public sealed class AddCargoContainerCommandValidator : AbstractValidator<AddCargoContainerCommand>
{
    public AddCargoContainerCommandValidator()
    {
        RuleFor(x => x.ObjectId).NotEmpty();
        RuleFor(x => x.SequenceNo).GreaterThan(0);
        RuleFor(x => x.ContainerType).NotEmpty().MaximumLength(16);
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.Teu).GreaterThanOrEqualTo(0);
    }
}

public sealed class AddCargoPackageCommandHandler : IRequestHandler<AddCargoPackageCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;

    public AddCargoPackageCommandHandler(ILcmsDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<Guid> Handle(AddCargoPackageCommand request, CancellationToken cancellationToken)
    {
        var type = await CargoParentGuard.RequireAsync(_db, _tenant, request.ObjectType, request.ObjectId, cancellationToken);
        var row = new CargoPackage
        {
            TenantId = _tenant.TenantId!.Value,
            ObjectType = type,
            ObjectId = request.ObjectId,
            SequenceNo = request.SequenceNo,
            PackageCount = request.PackageCount,
            LengthCm = request.LengthCm,
            WidthCm = request.WidthCm,
            HeightCm = request.HeightCm,
            WeightKg = request.WeightKg
        };
        _db.CargoPackages.Add(row);
        await _db.SaveChangesAsync(cancellationToken);
        return row.Id;
    }
}

public sealed class AddCargoContainerCommandHandler : IRequestHandler<AddCargoContainerCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;

    public AddCargoContainerCommandHandler(ILcmsDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<Guid> Handle(AddCargoContainerCommand request, CancellationToken cancellationToken)
    {
        var type = await CargoParentGuard.RequireAsync(_db, _tenant, request.ObjectType, request.ObjectId, cancellationToken);
        var row = new CargoContainer
        {
            TenantId = _tenant.TenantId!.Value,
            ObjectType = type,
            ObjectId = request.ObjectId,
            SequenceNo = request.SequenceNo,
            ContainerType = request.ContainerType.Trim(),
            ContainerNo = string.IsNullOrWhiteSpace(request.ContainerNo) ? null : request.ContainerNo.Trim(),
            Quantity = request.Quantity,
            Teu = request.Teu
        };
        _db.CargoContainers.Add(row);
        await _db.SaveChangesAsync(cancellationToken);
        return row.Id;
    }
}

internal static class CargoParentGuard
{
    public static async Task<string> RequireAsync(
        ILcmsDbContext db,
        ITenantContext tenant,
        string objectType,
        Guid objectId,
        CancellationToken cancellationToken)
    {
        if (!tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var type = objectType.Trim().ToLowerInvariant();
        if (!OperationalObjectTypes.CargoParents.Contains(type))
        {
            throw new ConflictAppException("Kiện và container chỉ gắn trên Bill, đơn hàng hoặc Shipment.");
        }

        var exists = type switch
        {
            OperationalObjectTypes.Bill => await db.Bills.AnyAsync(b => b.Id == objectId, cancellationToken),
            OperationalObjectTypes.Order => await db.Orders.AnyAsync(o => o.Id == objectId, cancellationToken),
            _ => await db.Shipments.AnyAsync(s => s.Id == objectId, cancellationToken)
        };
        if (!exists)
        {
            throw new NotFoundAppException("Không tìm thấy chứng từ nghiệp vụ.");
        }

        return type;
    }
}
