using LCMS.Domain.Common;
using LCMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LCMS.Infrastructure.Persistence.Configurations;

internal sealed class SurchargeConfiguration : IEntityTypeConfiguration<Surcharge>
{
    public void Configure(EntityTypeBuilder<Surcharge> builder)
    {
        builder.ToTable("surcharges");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.Code).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(256).IsRequired();
        builder.Property(e => e.Direction).HasMaxLength(16).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(32).IsRequired();
        builder.HasIndex(e => new { e.TenantId, e.Code }).IsUnique().HasFilter("deleted_at IS NULL");
        builder.HasIndex(e => new { e.TenantId, e.SourceLegacyComponentId })
            .IsUnique()
            .HasFilter("deleted_at IS NULL AND source_legacy_component_id IS NOT NULL");
    }
}

internal sealed class SurchargeVersionConfiguration : IEntityTypeConfiguration<SurchargeVersion>
{
    public void Configure(EntityTypeBuilder<SurchargeVersion> builder)
    {
        builder.ToTable("surcharge_versions");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.SurchargeId).IsRequired();
        builder.Property(e => e.VersionNo).IsRequired();
        builder.Property(e => e.PublishStatus).HasMaxLength(32).IsRequired();
        builder.HasIndex(e => new { e.TenantId, e.SurchargeId, e.VersionNo }).IsUnique().HasFilter("deleted_at IS NULL");
        builder.HasIndex(e => new { e.TenantId, e.PublishStatus, e.ValidFrom });
        builder.HasOne(e => e.Surcharge).WithMany().HasForeignKey(e => e.SurchargeId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SurchargeRuleConfiguration : IEntityTypeConfiguration<SurchargeRule>
{
    public void Configure(EntityTypeBuilder<SurchargeRule> builder)
    {
        builder.ToTable("surcharge_rules");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.SurchargeVersionId).IsRequired();
        builder.Property(e => e.CalculationMode).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Basis).HasMaxLength(32);
        builder.Property(e => e.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(e => e.RateAmountPercent).HasPrecision(18, 4);
        builder.Property(e => e.MinAmount).HasPrecision(18, 4);
        builder.Property(e => e.MaxAmount).HasPrecision(18, 4);
        builder.Property(e => e.ContainerType).HasMaxLength(16);
        builder.HasIndex(e => new { e.TenantId, e.SurchargeVersionId });
        builder.HasOne(e => e.SurchargeVersion).WithMany().HasForeignKey(e => e.SurchargeVersionId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SurchargeConditionConfiguration : IEntityTypeConfiguration<SurchargeCondition>
{
    public void Configure(EntityTypeBuilder<SurchargeCondition> builder)
    {
        builder.ToTable("surcharge_conditions");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.SurchargeRuleId).IsRequired();
        builder.Property(e => e.Dimension).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Operator).HasMaxLength(16).IsRequired();
        builder.Property(e => e.ValueText).HasMaxLength(256);
        builder.Property(e => e.ValueFrom).HasPrecision(18, 4);
        builder.Property(e => e.ValueTo).HasPrecision(18, 4);
        builder.HasIndex(e => new { e.TenantId, e.SurchargeRuleId });
        builder.HasOne(e => e.SurchargeRule).WithMany().HasForeignKey(e => e.SurchargeRuleId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SurchargeScopeConfiguration : IEntityTypeConfiguration<SurchargeScope>
{
    public void Configure(EntityTypeBuilder<SurchargeScope> builder)
    {
        builder.ToTable("surcharge_scopes");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.SurchargeRuleId).IsRequired();
        builder.Property(e => e.ServiceTypeCode).HasMaxLength(64);
        builder.Property(e => e.RouteCode).HasMaxLength(64);
        builder.Property(e => e.TransportMode).HasMaxLength(32);
        builder.HasIndex(e => new { e.TenantId, e.SurchargeRuleId });
        builder.HasIndex(e => new { e.TenantId, e.RateCardId });
        builder.HasOne(e => e.SurchargeRule).WithMany().HasForeignKey(e => e.SurchargeRuleId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SurchargeBreakConfiguration : IEntityTypeConfiguration<SurchargeBreak>
{
    public void Configure(EntityTypeBuilder<SurchargeBreak> builder)
    {
        builder.ToTable("surcharge_breaks");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.SurchargeRuleId).IsRequired();
        builder.Property(e => e.MinQuantity).HasPrecision(18, 4);
        builder.Property(e => e.MaxQuantity).HasPrecision(18, 4);
        builder.Property(e => e.UnitAmount).HasPrecision(18, 4);
        builder.HasIndex(e => new { e.TenantId, e.SurchargeRuleId, e.SequenceNo }).IsUnique().HasFilter("deleted_at IS NULL");
        builder.HasOne(e => e.SurchargeRule).WithMany().HasForeignKey(e => e.SurchargeRuleId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SurchargeMigrationLogConfiguration : IEntityTypeConfiguration<SurchargeMigrationLog>
{
    public void Configure(EntityTypeBuilder<SurchargeMigrationLog> builder)
    {
        builder.ToTable("surcharge_migration_logs");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.PricingRuleComponentId).IsRequired();
        builder.Property(e => e.Classification).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Outcome).HasMaxLength(32).IsRequired();
        builder.HasIndex(e => new { e.TenantId, e.PricingRuleComponentId }).IsUnique().HasFilter("deleted_at IS NULL");
    }
}
