using LCMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LCMS.Infrastructure.Persistence.Configurations;

internal sealed class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        builder.ToTable("locations");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.Code).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(256).IsRequired();
        builder.Property(e => e.LocationType).HasMaxLength(32).IsRequired();
        builder.Property(e => e.CountryCode).HasMaxLength(2);
        builder.Property(e => e.Subdivision).HasMaxLength(128);
        builder.Property(e => e.City).HasMaxLength(128);
        builder.Property(e => e.IataCode).HasMaxLength(3);
        builder.Property(e => e.Unlocode).HasMaxLength(5);
        builder.Property(e => e.TerminalCode).HasMaxLength(32);
        builder.HasIndex(e => new { e.TenantId, e.Code }).IsUnique().HasFilter("deleted_at IS NULL");
        builder.HasIndex(e => new { e.TenantId, e.IataCode });
        builder.HasIndex(e => new { e.TenantId, e.Unlocode });
        builder.HasIndex(e => new { e.TenantId, e.LocationType, e.IsActive });
    }
}

internal sealed class LocationAliasConfiguration : IEntityTypeConfiguration<LocationAlias>
{
    public void Configure(EntityTypeBuilder<LocationAlias> builder)
    {
        builder.ToTable("location_aliases");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.LocationId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.AliasCode).HasMaxLength(64).IsRequired();
        builder.Property(e => e.SourceSystem).HasMaxLength(64);
        builder.HasIndex(e => new { e.TenantId, e.AliasCode }).IsUnique().HasFilter("deleted_at IS NULL");
        builder.HasIndex(e => new { e.TenantId, e.LocationId });
        builder.HasOne(e => e.Location)
            .WithMany()
            .HasForeignKey(e => e.LocationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class RouteMasterConfiguration : IEntityTypeConfiguration<RouteMaster>
{
    public void Configure(EntityTypeBuilder<RouteMaster> builder)
    {
        builder.ToTable("routes");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.Code).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(256).IsRequired();
        builder.Property(e => e.OriginLocationId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.DestinationLocationId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.TransportModeCode).HasMaxLength(32);
        builder.Property(e => e.ServiceTypeCode).HasMaxLength(64);
        builder.HasIndex(e => new { e.TenantId, e.Code }).IsUnique().HasFilter("deleted_at IS NULL");
        builder.HasIndex(e => new { e.TenantId, e.OriginLocationId, e.DestinationLocationId });
        builder.HasOne(e => e.OriginLocation)
            .WithMany()
            .HasForeignKey(e => e.OriginLocationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.DestinationLocation)
            .WithMany()
            .HasForeignKey(e => e.DestinationLocationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class RouteStopConfiguration : IEntityTypeConfiguration<RouteStop>
{
    public void Configure(EntityTypeBuilder<RouteStop> builder)
    {
        builder.ToTable("route_stops");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.RouteId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.LocationId).HasColumnType("uuid").IsRequired();
        builder.HasIndex(e => new { e.TenantId, e.RouteId, e.SequenceNo });
        builder.HasOne(e => e.Route)
            .WithMany()
            .HasForeignKey(e => e.RouteId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Location)
            .WithMany()
            .HasForeignKey(e => e.LocationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CommodityTypeConfiguration : IEntityTypeConfiguration<CommodityType>
{
    public void Configure(EntityTypeBuilder<CommodityType> builder)
    {
        builder.ToTable("commodity_types");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.Code).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(256).IsRequired();
        builder.Property(e => e.Category).HasMaxLength(128);
        builder.Property(e => e.ParentId).HasColumnType("uuid");
        builder.Property(e => e.SpecialHandling).HasMaxLength(512);
        builder.HasIndex(e => new { e.TenantId, e.Code }).IsUnique().HasFilter("deleted_at IS NULL");
        builder.HasIndex(e => new { e.TenantId, e.ParentId });
        builder.HasOne(e => e.Parent)
            .WithMany()
            .HasForeignKey(e => e.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class OperationalPartySnapshotConfiguration : IEntityTypeConfiguration<OperationalPartySnapshot>
{
    public void Configure(EntityTypeBuilder<OperationalPartySnapshot> builder)
    {
        builder.ToTable("operational_party_snapshots");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.ObjectType).HasMaxLength(32).IsRequired();
        builder.Property(e => e.ObjectId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.RoleCode).HasMaxLength(64).IsRequired();
        builder.Property(e => e.PartyId).HasColumnType("uuid");
        builder.Property(e => e.DisplayName).HasMaxLength(256).IsRequired();
        builder.Property(e => e.LegalName).HasMaxLength(256);
        builder.Property(e => e.TaxId).HasMaxLength(64);
        builder.Property(e => e.Phone).HasMaxLength(64);
        builder.Property(e => e.Email).HasMaxLength(256);
        builder.Property(e => e.AddressLine1).HasMaxLength(256);
        builder.Property(e => e.City).HasMaxLength(128);
        builder.Property(e => e.CountryCode).HasMaxLength(2);
        builder.Property(e => e.ContactName).HasMaxLength(256);
        builder.Property(e => e.ContactPhone).HasMaxLength(64);
        builder.Property(e => e.ContactEmail).HasMaxLength(256);
        builder.Property(e => e.SourceChannel).HasMaxLength(32).IsRequired();
        builder.HasIndex(e => new { e.TenantId, e.ObjectType, e.ObjectId, e.RoleCode, e.SupersededAt });
    }
}
