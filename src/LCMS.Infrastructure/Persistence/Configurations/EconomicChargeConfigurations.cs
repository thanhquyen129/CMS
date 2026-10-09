using LCMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LCMS.Infrastructure.Persistence.Configurations;

internal sealed class EconomicChargeTypeConfiguration : IEntityTypeConfiguration<EconomicChargeType>
{
    public void Configure(EntityTypeBuilder<EconomicChargeType> builder)
    {
        builder.ToTable("economic_charge_types");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.Code).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(256).IsRequired();
        builder.Property(e => e.IsActive).IsRequired();
        builder.HasIndex(e => new { e.TenantId, e.Code }).IsUnique().HasFilter("deleted_at IS NULL");
    }
}

internal sealed class ChargeTypeMappingConfiguration : IEntityTypeConfiguration<ChargeTypeMapping>
{
    public void Configure(EntityTypeBuilder<ChargeTypeMapping> builder)
    {
        builder.ToTable("charge_type_mappings");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.SourceKind).HasMaxLength(32).IsRequired();
        builder.Property(e => e.SourceCode).HasMaxLength(64).IsRequired();
        builder.Property(e => e.EconomicChargeTypeId).IsRequired();
        builder.HasIndex(e => new { e.TenantId, e.SourceKind, e.SourceCode }).IsUnique().HasFilter("deleted_at IS NULL");
        builder.HasOne(e => e.EconomicChargeType)
            .WithMany()
            .HasForeignKey(e => e.EconomicChargeTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
