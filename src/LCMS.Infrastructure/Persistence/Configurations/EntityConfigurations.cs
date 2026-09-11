using LCMS.Domain.Common;
using LCMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LCMS.Infrastructure.Persistence.Configurations;

internal static class EntityBaseConfiguration
{
    public static void ConfigureEntityBase<TEntity>(EntityTypeBuilder<TEntity> builder)
        where TEntity : EntityBase
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(e => e.RowVersion)
            .HasColumnType("bytea")
            .IsRequired()
            .IsConcurrencyToken();

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.DeletedAt);
        builder.Ignore(e => e.IsDeleted);
    }
}

internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");
        EntityBaseConfiguration.ConfigureEntityBase(builder);

        builder.Property(e => e.Code).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(256).IsRequired();
        builder.Property(e => e.IsActive).IsRequired();

        builder.HasIndex(e => e.Code).IsUnique();
    }
}

internal sealed class BillConfiguration : IEntityTypeConfiguration<Bill>
{
    public void Configure(EntityTypeBuilder<Bill> builder)
    {
        builder.ToTable("bills");
        EntityBaseConfiguration.ConfigureEntityBase(builder);

        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.BillNo).HasMaxLength(64).IsRequired();
        builder.Property(e => e.BillType).HasMaxLength(64).IsRequired();
        builder.Property(e => e.SourceSystem).HasMaxLength(64);
        builder.Property(e => e.ExternalId).HasMaxLength(128);
        builder.Property(e => e.ExternalVersion).HasMaxLength(64);
        builder.Property(e => e.OperationalStatus).HasMaxLength(64).IsRequired();
        builder.Property(e => e.IsActive).IsRequired();

        // IDX-001
        builder.HasIndex(e => new { e.TenantId, e.BillNo }).IsUnique();
        // IDX-002
        builder.HasIndex(e => new { e.TenantId, e.SourceSystem, e.ExternalId });

        builder.HasOne(e => e.Tenant)
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("organizations");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.Code).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(256).IsRequired();
        builder.HasIndex(e => new { e.TenantId, e.Code }).IsUnique();
    }
}

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.Email).HasMaxLength(320).IsRequired();
        builder.Property(e => e.DisplayName).HasMaxLength(256).IsRequired();
        builder.HasIndex(e => new { e.TenantId, e.Email }).IsUnique();
    }
}

internal sealed class BusinessPartyConfiguration : IEntityTypeConfiguration<BusinessParty>
{
    public void Configure(EntityTypeBuilder<BusinessParty> builder)
    {
        builder.ToTable("business_parties");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.Code).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(256).IsRequired();
        builder.HasIndex(e => new { e.TenantId, e.Code }).IsUnique();
    }
}

internal sealed class CostConfiguration : IEntityTypeConfiguration<Cost>
{
    public void Configure(EntityTypeBuilder<Cost> builder)
    {
        builder.ToTable("costs");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.CostTypeCode).HasMaxLength(64);
        builder.Property(e => e.FinancialMaturity).HasMaxLength(32).IsRequired();
        builder.Property(e => e.AttributionType).HasMaxLength(32).IsRequired();
        builder.Property(e => e.ExpectedAmount).HasPrecision(18, 4);
        builder.Property(e => e.ConfirmedAmount).HasPrecision(18, 4);
        builder.Property(e => e.ActualAmount).HasPrecision(18, 4);
        builder.Property(e => e.Amount).HasPrecision(18, 4);
        builder.Property(e => e.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(e => e.BaseAmount).HasPrecision(18, 4);
        builder.Property(e => e.SourceType).HasMaxLength(64);
        builder.Property(e => e.RecordStatus).HasMaxLength(32).IsRequired();
        builder.Property(e => e.ApprovalStatus).HasMaxLength(32).IsRequired();
        builder.Property(e => e.EffectiveDate).IsRequired();

        // IDX-003
        builder.HasIndex(e => new { e.TenantId, e.BillId, e.FinancialMaturity, e.EffectiveDate });
        // IDX-004
        builder.HasIndex(e => new { e.TenantId, e.VendorPartyId, e.EffectiveDate });
        // Idempotent seed from rating_detail (NULLs allowed for manual costs)
        builder.HasIndex(e => new { e.TenantId, e.SourceType, e.SourceId }).IsUnique();

        builder.HasOne(e => e.Bill)
            .WithMany()
            .HasForeignKey(e => e.BillId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CostAdjustmentConfiguration : IEntityTypeConfiguration<CostAdjustment>
{
    public void Configure(EntityTypeBuilder<CostAdjustment> builder)
    {
        builder.ToTable("cost_adjustments");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.CostId).IsRequired();
        builder.Property(e => e.AdjustmentType).HasMaxLength(32).IsRequired();
        builder.Property(e => e.DeltaAmount).HasPrecision(18, 4);
        builder.Property(e => e.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(1024).IsRequired();
        builder.Property(e => e.EffectiveDate).IsRequired();
        builder.Property(e => e.AppliedToMaturity).HasMaxLength(32).IsRequired();
        builder.Property(e => e.AmountBefore).HasPrecision(18, 4);
        builder.Property(e => e.AmountAfter).HasPrecision(18, 4);

        builder.HasIndex(e => new { e.TenantId, e.CostId, e.CreatedAt });

        builder.HasOne(e => e.Cost)
            .WithMany()
            .HasForeignKey(e => e.CostId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CostAllocationConfiguration : IEntityTypeConfiguration<CostAllocation>
{
    public void Configure(EntityTypeBuilder<CostAllocation> builder)
    {
        builder.ToTable("cost_allocations");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.CostId).IsRequired();
        builder.Property(e => e.VersionNo).IsRequired();
        builder.Property(e => e.AllocationBasis).HasMaxLength(64).IsRequired();
        builder.Property(e => e.ApplicabilityMode).HasMaxLength(64).IsRequired();
        builder.Property(e => e.AllocatableAmount).HasPrecision(18, 4);
        builder.Property(e => e.AllocatedAmount).HasPrecision(18, 4);
        builder.Property(e => e.AllocationStatus).HasMaxLength(32).IsRequired();
        builder.Property(e => e.RuleVersion).HasMaxLength(64);

        builder.HasIndex(e => new { e.TenantId, e.CostId, e.VersionNo }).IsUnique();

        builder.HasOne(e => e.Cost)
            .WithMany()
            .HasForeignKey(e => e.CostId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CostAllocationDetailConfiguration : IEntityTypeConfiguration<CostAllocationDetail>
{
    public void Configure(EntityTypeBuilder<CostAllocationDetail> builder)
    {
        builder.ToTable("cost_allocation_details");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.AllocationId).IsRequired();
        builder.Property(e => e.BillId).IsRequired();
        builder.Property(e => e.BasisValue).HasPrecision(18, 6);
        builder.Property(e => e.BasisRatio).HasPrecision(18, 8);
        builder.Property(e => e.AllocatedAmount).HasPrecision(18, 4);
        builder.Property(e => e.RoundingAdjustment).HasPrecision(18, 4);
        builder.Property(e => e.ManualOverrideAmount).HasPrecision(18, 4);
        builder.Property(e => e.OverrideReason).HasMaxLength(512);

        builder.HasIndex(e => new { e.TenantId, e.AllocationId, e.BillId }).IsUnique();

        builder.HasOne(e => e.Allocation)
            .WithMany()
            .HasForeignKey(e => e.AllocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Bill)
            .WithMany()
            .HasForeignKey(e => e.BillId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class RevenueConfiguration : IEntityTypeConfiguration<Revenue>
{
    public void Configure(EntityTypeBuilder<Revenue> builder)
    {
        builder.ToTable("revenues");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.BillId).IsRequired();
        builder.Property(e => e.Amount).HasPrecision(18, 4);
        builder.Property(e => e.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(e => e.FinancialMaturity).HasMaxLength(32).IsRequired();
        builder.Property(e => e.RecordStatus).HasMaxLength(32).IsRequired();
        builder.HasIndex(e => new { e.TenantId, e.BillId, e.FinancialMaturity });
    }
}

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.Code).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(256).IsRequired();
        builder.Property(e => e.IsSystem).IsRequired();
        builder.HasIndex(e => new { e.TenantId, e.Code }).IsUnique();
    }
}

internal sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("permissions");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.ActionCode).HasMaxLength(128).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(256).IsRequired();
        builder.HasIndex(e => e.ActionCode).IsUnique();
    }
}

internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("role_permissions");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.RoleId).IsRequired();
        builder.Property(e => e.PermissionId).IsRequired();
        builder.Property(e => e.DataScope).HasMaxLength(32).IsRequired();
        builder.HasIndex(e => new { e.TenantId, e.RoleId, e.PermissionId }).IsUnique();
    }
}

internal sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("user_roles");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.UserId).IsRequired();
        builder.Property(e => e.RoleId).IsRequired();
        builder.HasIndex(e => new { e.TenantId, e.UserId, e.RoleId }).IsUnique();
    }
}

internal sealed class CurrencyConfiguration : IEntityTypeConfiguration<Currency>
{
    public void Configure(EntityTypeBuilder<Currency> builder)
    {
        builder.ToTable("currencies");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.Code).HasMaxLength(3).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(128).IsRequired();
        builder.Property(e => e.DecimalPlaces).IsRequired();
        builder.Property(e => e.IsActive).IsRequired();
        builder.HasIndex(e => e.Code).IsUnique();
    }
}

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");
        EntityBaseConfiguration.ConfigureEntityBase(builder);

        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.OrderNo).HasMaxLength(64).IsRequired();
        builder.Property(e => e.SourceSystem).HasMaxLength(64).IsRequired();
        builder.Property(e => e.ExternalId).HasMaxLength(128).IsRequired();
        builder.Property(e => e.ExternalVersion).HasMaxLength(64);
        builder.Property(e => e.OperationalStatus).HasMaxLength(64).IsRequired();
        builder.Property(e => e.IsActive).IsRequired();

        // C-002 / sync idempotency — unique external identity per tenant
        builder.HasIndex(e => new { e.TenantId, e.SourceSystem, e.ExternalId }).IsUnique();
        builder.HasIndex(e => new { e.TenantId, e.OrderNo });

        builder.HasOne(e => e.Tenant)
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ShipmentConfiguration : IEntityTypeConfiguration<Shipment>
{
    public void Configure(EntityTypeBuilder<Shipment> builder)
    {
        builder.ToTable("shipments");
        EntityBaseConfiguration.ConfigureEntityBase(builder);

        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.ShipmentNo).HasMaxLength(64).IsRequired();
        builder.Property(e => e.SourceSystem).HasMaxLength(64).IsRequired();
        builder.Property(e => e.ExternalId).HasMaxLength(128).IsRequired();
        builder.Property(e => e.ExternalVersion).HasMaxLength(64);
        builder.Property(e => e.OperationalStatus).HasMaxLength(64).IsRequired();
        builder.Property(e => e.IsActive).IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.SourceSystem, e.ExternalId }).IsUnique();
        builder.HasIndex(e => new { e.TenantId, e.ShipmentNo });

        builder.HasOne(e => e.Tenant)
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class OrderBillLinkConfiguration : IEntityTypeConfiguration<OrderBillLink>
{
    public void Configure(EntityTypeBuilder<OrderBillLink> builder)
    {
        builder.ToTable("order_bill_links");
        EntityBaseConfiguration.ConfigureEntityBase(builder);

        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.OrderId).IsRequired();
        builder.Property(e => e.BillId).IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.OrderId, e.BillId }).IsUnique();
        builder.HasIndex(e => new { e.TenantId, e.BillId });
        builder.HasIndex(e => new { e.TenantId, e.OrderId });

        builder.HasOne(e => e.Order)
            .WithMany()
            .HasForeignKey(e => e.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Bill)
            .WithMany()
            .HasForeignKey(e => e.BillId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class BillShipmentLinkConfiguration : IEntityTypeConfiguration<BillShipmentLink>
{
    public void Configure(EntityTypeBuilder<BillShipmentLink> builder)
    {
        builder.ToTable("bill_shipment_links");
        EntityBaseConfiguration.ConfigureEntityBase(builder);

        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.BillId).IsRequired();
        builder.Property(e => e.ShipmentId).IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.BillId, e.ShipmentId }).IsUnique();
        builder.HasIndex(e => new { e.TenantId, e.BillId });
        builder.HasIndex(e => new { e.TenantId, e.ShipmentId });

        builder.HasOne(e => e.Bill)
            .WithMany()
            .HasForeignKey(e => e.BillId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Shipment)
            .WithMany()
            .HasForeignKey(e => e.ShipmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class RateCardConfiguration : IEntityTypeConfiguration<RateCard>
{
    public void Configure(EntityTypeBuilder<RateCard> builder)
    {
        builder.ToTable("rate_cards");
        EntityBaseConfiguration.ConfigureEntityBase(builder);

        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.Code).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(256).IsRequired();
        builder.Property(e => e.PartyType).HasMaxLength(32).IsRequired();
        builder.Property(e => e.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(1024);
        builder.Property(e => e.IsActive).IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.Code }).IsUnique();
    }
}

internal sealed class RateVersionConfiguration : IEntityTypeConfiguration<RateVersion>
{
    public void Configure(EntityTypeBuilder<RateVersion> builder)
    {
        builder.ToTable("rate_versions");
        EntityBaseConfiguration.ConfigureEntityBase(builder);

        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.RateCardId).IsRequired();
        builder.Property(e => e.VersionNo).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Note).HasMaxLength(1024);
        builder.Ignore(e => e.IsPublished);

        builder.HasIndex(e => new { e.TenantId, e.RateCardId, e.VersionNo }).IsUnique();
        builder.HasIndex(e => new { e.TenantId, e.RateCardId, e.Status });

        builder.HasOne(e => e.RateCard)
            .WithMany()
            .HasForeignKey(e => e.RateCardId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PricingRuleConfiguration : IEntityTypeConfiguration<PricingRule>
{
    public void Configure(EntityTypeBuilder<PricingRule> builder)
    {
        builder.ToTable("pricing_rules");
        EntityBaseConfiguration.ConfigureEntityBase(builder);

        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.RateVersionId).IsRequired();
        builder.Property(e => e.Code).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(256).IsRequired();
        builder.Property(e => e.CalcMethod).HasMaxLength(32).IsRequired();
        builder.Property(e => e.UnitAmount).HasPrecision(18, 4);
        builder.Property(e => e.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(e => e.Applicability).HasMaxLength(512);
        builder.Property(e => e.SortOrder).IsRequired();
        builder.Property(e => e.IsActive).IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.RateVersionId, e.Code }).IsUnique();

        builder.HasOne(e => e.RateVersion)
            .WithMany()
            .HasForeignKey(e => e.RateVersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PricingRuleComponentConfiguration : IEntityTypeConfiguration<PricingRuleComponent>
{
    public void Configure(EntityTypeBuilder<PricingRuleComponent> builder)
    {
        builder.ToTable("pricing_rule_components");
        EntityBaseConfiguration.ConfigureEntityBase(builder);

        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.PricingRuleId).IsRequired();
        builder.Property(e => e.Code).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(256).IsRequired();
        builder.Property(e => e.FinancialNature).HasMaxLength(32).IsRequired();
        builder.Property(e => e.CostTypeCode).HasMaxLength(64);
        builder.Property(e => e.RevenueTypeCode).HasMaxLength(64);
        builder.Property(e => e.Amount).HasPrecision(18, 4);
        builder.Property(e => e.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(e => e.SortOrder).IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.PricingRuleId, e.Code }).IsUnique();

        builder.HasOne(e => e.PricingRule)
            .WithMany()
            .HasForeignKey(e => e.PricingRuleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class RatingConfiguration : IEntityTypeConfiguration<Rating>
{
    public void Configure(EntityTypeBuilder<Rating> builder)
    {
        builder.ToTable("ratings");
        EntityBaseConfiguration.ConfigureEntityBase(builder);

        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.BillId).IsRequired();
        builder.Property(e => e.RateVersionId).IsRequired();
        builder.Property(e => e.RatedAt).IsRequired();
        builder.Property(e => e.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(e => e.TotalAmount).HasPrecision(18, 4);
        builder.Property(e => e.Quantity).HasPrecision(18, 4);
        builder.Property(e => e.Status).HasMaxLength(32).IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.BillId, e.RatedAt });
        builder.HasIndex(e => new { e.TenantId, e.RateVersionId });

        builder.HasOne(e => e.Bill)
            .WithMany()
            .HasForeignKey(e => e.BillId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.RateVersion)
            .WithMany()
            .HasForeignKey(e => e.RateVersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class RatingDetailConfiguration : IEntityTypeConfiguration<RatingDetail>
{
    public void Configure(EntityTypeBuilder<RatingDetail> builder)
    {
        builder.ToTable("rating_details");
        EntityBaseConfiguration.ConfigureEntityBase(builder);

        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.RatingId).IsRequired();
        builder.Property(e => e.RuleCode).HasMaxLength(64).IsRequired();
        builder.Property(e => e.ComponentCode).HasMaxLength(64).IsRequired();
        builder.Property(e => e.ComponentName).HasMaxLength(256).IsRequired();
        builder.Property(e => e.FinancialNature).HasMaxLength(32).IsRequired();
        builder.Property(e => e.FinancialMaturity).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Amount).HasPrecision(18, 4);
        builder.Property(e => e.CurrencyCode).HasMaxLength(3).IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.RatingId });

        builder.HasOne(e => e.Rating)
            .WithMany()
            .HasForeignKey(e => e.RatingId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
