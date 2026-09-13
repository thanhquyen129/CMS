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
        builder.Property(e => e.OrganizationId).HasColumnType("uuid");
        builder.HasIndex(e => new { e.TenantId, e.OrganizationId });

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
        builder.Property(e => e.PasswordHash).HasMaxLength(500);
        builder.Property(e => e.OrganizationId).HasColumnType("uuid");
        builder.HasIndex(e => new { e.TenantId, e.Email }).IsUnique();
        builder.HasIndex(e => new { e.TenantId, e.OrganizationId });
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

internal sealed class PartyRoleConfiguration : IEntityTypeConfiguration<PartyRole>
{
    public void Configure(EntityTypeBuilder<PartyRole> builder)
    {
        builder.ToTable("party_roles");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.PartyId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.RoleCode).HasMaxLength(64).IsRequired();
        builder.Property(e => e.IsActive).IsRequired();
        builder.HasIndex(e => new { e.TenantId, e.PartyId, e.RoleCode }).IsUnique();
        builder.HasIndex(e => new { e.TenantId, e.RoleCode });
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
        builder.Property(e => e.OrganizationId).HasColumnType("uuid");
        builder.HasIndex(e => new { e.TenantId, e.OrganizationId });

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
        builder.Property(e => e.RevenueTypeCode).HasMaxLength(64);
        builder.Property(e => e.FinancialMaturity).HasMaxLength(32).IsRequired();
        builder.Property(e => e.ExpectedAmount).HasPrecision(18, 4);
        builder.Property(e => e.ConfirmedAmount).HasPrecision(18, 4);
        builder.Property(e => e.ActualAmount).HasPrecision(18, 4);
        builder.Property(e => e.Amount).HasPrecision(18, 4);
        builder.Property(e => e.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(e => e.BaseAmount).HasPrecision(18, 4);
        builder.Property(e => e.SourceType).HasMaxLength(64);
        builder.Property(e => e.RecognitionPolicyVersion).HasMaxLength(64);
        builder.Property(e => e.RecordStatus).HasMaxLength(32).IsRequired();
        builder.Property(e => e.ApprovalStatus).HasMaxLength(32).IsRequired();
        builder.Property(e => e.EffectiveDate).IsRequired();

        // IDX-005
        builder.HasIndex(e => new { e.TenantId, e.BillId, e.FinancialMaturity, e.EffectiveDate });
        // Idempotent create by source (C-004 — no duplicate economic revenue from same source)
        builder.HasIndex(e => new { e.TenantId, e.SourceType, e.SourceId }).IsUnique();

        builder.HasOne(e => e.Bill)
            .WithMany()
            .HasForeignKey(e => e.BillId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class RevenueAdjustmentConfiguration : IEntityTypeConfiguration<RevenueAdjustment>
{
    public void Configure(EntityTypeBuilder<RevenueAdjustment> builder)
    {
        builder.ToTable("revenue_adjustments");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.RevenueId).IsRequired();
        builder.Property(e => e.AdjustmentType).HasMaxLength(32).IsRequired();
        builder.Property(e => e.DeltaAmount).HasPrecision(18, 4);
        builder.Property(e => e.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(1024).IsRequired();
        builder.Property(e => e.EffectiveDate).IsRequired();
        builder.Property(e => e.AppliedToMaturity).HasMaxLength(32).IsRequired();
        builder.Property(e => e.AmountBefore).HasPrecision(18, 4);
        builder.Property(e => e.AmountAfter).HasPrecision(18, 4);

        builder.HasIndex(e => new { e.TenantId, e.RevenueId, e.CreatedAt });

        builder.HasOne(e => e.Revenue)
            .WithMany()
            .HasForeignKey(e => e.RevenueId)
            .OnDelete(DeleteBehavior.Restrict);
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

internal sealed class FxRateConfiguration : IEntityTypeConfiguration<FxRate>
{
    public void Configure(EntityTypeBuilder<FxRate> builder)
    {
        builder.ToTable("fx_rates");
        EntityBaseConfiguration.ConfigureEntityBase(builder);

        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.FromCurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(e => e.ToCurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(e => e.RateDate).IsRequired();
        builder.Property(e => e.Rate).HasPrecision(18, 8).IsRequired();
        builder.Property(e => e.Source).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Version).IsRequired();
        builder.Property(e => e.Note).HasMaxLength(512);

        builder.HasIndex(e => new
            {
                e.TenantId,
                e.FromCurrencyCode,
                e.ToCurrencyCode,
                e.RateDate,
                e.Version
            })
            .IsUnique();

        builder.HasIndex(e => new
        {
            e.TenantId,
            e.FromCurrencyCode,
            e.ToCurrencyCode,
            e.RateDate
        });
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

internal sealed class TransportLegConfiguration : IEntityTypeConfiguration<TransportLeg>
{
    public void Configure(EntityTypeBuilder<TransportLeg> builder)
    {
        builder.ToTable("transport_legs");
        EntityBaseConfiguration.ConfigureEntityBase(builder);

        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.LegNo).HasMaxLength(64).IsRequired();
        builder.Property(e => e.ShipmentId).IsRequired();
        builder.Property(e => e.SourceSystem).HasMaxLength(64).IsRequired();
        builder.Property(e => e.ExternalId).HasMaxLength(128).IsRequired();
        builder.Property(e => e.ExternalVersion).HasMaxLength(64);
        builder.Property(e => e.OperationalStatus).HasMaxLength(64).IsRequired();
        builder.Property(e => e.IsActive).IsRequired();

        // C-002
        builder.HasIndex(e => new { e.TenantId, e.SourceSystem, e.ExternalId }).IsUnique();
        builder.HasIndex(e => new { e.TenantId, e.ShipmentId });
        builder.HasIndex(e => new { e.TenantId, e.LegNo });

        builder.HasOne(e => e.Tenant)
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Shipment)
            .WithMany()
            .HasForeignKey(e => e.ShipmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class TransportMovementConfiguration : IEntityTypeConfiguration<TransportMovement>
{
    public void Configure(EntityTypeBuilder<TransportMovement> builder)
    {
        builder.ToTable("transport_movements");
        EntityBaseConfiguration.ConfigureEntityBase(builder);

        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.MovementNo).HasMaxLength(64).IsRequired();
        builder.Property(e => e.SourceSystem).HasMaxLength(64).IsRequired();
        builder.Property(e => e.ExternalId).HasMaxLength(128).IsRequired();
        builder.Property(e => e.ExternalVersion).HasMaxLength(64);
        builder.Property(e => e.OperationalStatus).HasMaxLength(64).IsRequired();
        builder.Property(e => e.IsActive).IsRequired();

        // C-002
        builder.HasIndex(e => new { e.TenantId, e.SourceSystem, e.ExternalId }).IsUnique();
        builder.HasIndex(e => new { e.TenantId, e.MovementNo });

        builder.HasOne(e => e.Tenant)
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class BillLegLinkConfiguration : IEntityTypeConfiguration<BillLegLink>
{
    public void Configure(EntityTypeBuilder<BillLegLink> builder)
    {
        builder.ToTable("bill_leg_links");
        EntityBaseConfiguration.ConfigureEntityBase(builder);

        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.BillId).IsRequired();
        builder.Property(e => e.TransportLegId).IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.BillId, e.TransportLegId }).IsUnique();
        builder.HasIndex(e => new { e.TenantId, e.BillId });
        builder.HasIndex(e => new { e.TenantId, e.TransportLegId });

        builder.HasOne(e => e.Bill)
            .WithMany()
            .HasForeignKey(e => e.BillId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.TransportLeg)
            .WithMany()
            .HasForeignKey(e => e.TransportLegId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class LegMovementLinkConfiguration : IEntityTypeConfiguration<LegMovementLink>
{
    public void Configure(EntityTypeBuilder<LegMovementLink> builder)
    {
        builder.ToTable("leg_movement_links");
        EntityBaseConfiguration.ConfigureEntityBase(builder);

        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.TransportLegId).IsRequired();
        builder.Property(e => e.TransportMovementId).IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.TransportLegId, e.TransportMovementId }).IsUnique();
        builder.HasIndex(e => new { e.TenantId, e.TransportLegId });
        builder.HasIndex(e => new { e.TenantId, e.TransportMovementId });

        builder.HasOne(e => e.TransportLeg)
            .WithMany()
            .HasForeignKey(e => e.TransportLegId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.TransportMovement)
            .WithMany()
            .HasForeignKey(e => e.TransportMovementId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class BillMovementLinkConfiguration : IEntityTypeConfiguration<BillMovementLink>
{
    public void Configure(EntityTypeBuilder<BillMovementLink> builder)
    {
        builder.ToTable("bill_movement_links");
        EntityBaseConfiguration.ConfigureEntityBase(builder);

        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.BillId).IsRequired();
        builder.Property(e => e.TransportMovementId).IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.BillId, e.TransportMovementId }).IsUnique();
        builder.HasIndex(e => new { e.TenantId, e.BillId });
        builder.HasIndex(e => new { e.TenantId, e.TransportMovementId });

        builder.HasOne(e => e.Bill)
            .WithMany()
            .HasForeignKey(e => e.BillId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.TransportMovement)
            .WithMany()
            .HasForeignKey(e => e.TransportMovementId)
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
        builder.Property(e => e.ServiceTypeCode).HasMaxLength(64);
        builder.Property(e => e.PartyTypeCode).HasMaxLength(32);
        builder.Property(e => e.RouteCode).HasMaxLength(64);
        builder.Property(e => e.MinAmount).HasPrecision(18, 4);
        builder.Property(e => e.MaxAmount).HasPrecision(18, 4);
        builder.Property(e => e.SortOrder).IsRequired();
        builder.Property(e => e.IsActive).IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.RateVersionId, e.Code }).IsUnique();
        builder.HasIndex(e => new { e.TenantId, e.RateVersionId, e.ServiceTypeCode });

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
        builder.Property(e => e.Weight).HasPrecision(18, 4);
        builder.Property(e => e.ServiceTypeCode).HasMaxLength(64);
        builder.Property(e => e.PartyTypeCode).HasMaxLength(32);
        builder.Property(e => e.RouteCode).HasMaxLength(64);
        builder.Property(e => e.BaseAmount).HasPrecision(18, 4);
        builder.Property(e => e.Status).HasMaxLength(32).IsRequired();
        builder.Property(e => e.SupersedesRatingId);

        builder.HasIndex(e => new { e.TenantId, e.BillId, e.RatedAt });
        builder.HasIndex(e => new { e.TenantId, e.RateVersionId });
        builder.HasIndex(e => new { e.TenantId, e.SupersedesRatingId });

        builder.HasOne(e => e.Bill)
            .WithMany()
            .HasForeignKey(e => e.BillId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.RateVersion)
            .WithMany()
            .HasForeignKey(e => e.RateVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.SupersedesRating)
            .WithMany()
            .HasForeignKey(e => e.SupersedesRatingId)
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

internal sealed class FinancialDocumentConfiguration : IEntityTypeConfiguration<FinancialDocument>
{
    public void Configure(EntityTypeBuilder<FinancialDocument> builder)
    {
        builder.ToTable("financial_documents");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.DocumentType).HasMaxLength(32).IsRequired();
        builder.Property(e => e.DocumentNo).HasMaxLength(128).IsRequired();
        builder.Property(e => e.Direction).HasMaxLength(32).IsRequired();
        builder.Property(e => e.TotalAmount).HasPrecision(18, 4);
        builder.Property(e => e.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(e => e.DocumentDate).IsRequired();
        builder.Property(e => e.ReceiptStatus).HasMaxLength(32).IsRequired();
        builder.Property(e => e.AcceptanceStatus).HasMaxLength(32).IsRequired();
        builder.Property(e => e.MatchingStatus).HasMaxLength(32).IsRequired();
        builder.Property(e => e.RecordStatus).HasMaxLength(32).IsRequired();
        builder.Property(e => e.CancelReason).HasMaxLength(512);
        builder.Property(e => e.Notes).HasMaxLength(2048);
        builder.Property(e => e.SourceSystem).HasMaxLength(64);
        builder.Property(e => e.ExternalId).HasMaxLength(128);

        // IDX-006
        builder.HasIndex(e => new { e.TenantId, e.DocumentType, e.DocumentNo, e.CounterpartyId });
        builder.HasIndex(e => new { e.TenantId, e.SourceSystem, e.ExternalId });

        builder.HasOne(e => e.Bill)
            .WithMany()
            .HasForeignKey(e => e.BillId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class FinancialDocumentLineConfiguration : IEntityTypeConfiguration<FinancialDocumentLine>
{
    public void Configure(EntityTypeBuilder<FinancialDocumentLine> builder)
    {
        builder.ToTable("financial_document_lines");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.DocumentId).IsRequired();
        builder.Property(e => e.LineNo).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(512);
        builder.Property(e => e.Amount).HasPrecision(18, 4);
        builder.Property(e => e.MatchedAmount).HasPrecision(18, 4);
        builder.Property(e => e.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(e => e.CostTypeCode).HasMaxLength(64);
        builder.Property(e => e.RevenueTypeCode).HasMaxLength(64);

        builder.HasIndex(e => new { e.TenantId, e.DocumentId, e.LineNo }).IsUnique();

        builder.HasOne(e => e.Document)
            .WithMany()
            .HasForeignKey(e => e.DocumentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DocumentMatchConfiguration : IEntityTypeConfiguration<DocumentMatch>
{
    public void Configure(EntityTypeBuilder<DocumentMatch> builder)
    {
        builder.ToTable("document_matches");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.MatchMethod).HasMaxLength(32).IsRequired();
        builder.Property(e => e.MatchStatus).HasMaxLength(32).IsRequired();
        builder.Property(e => e.VersionNo).IsRequired();
        builder.Property(e => e.ToleranceAmount).HasPrecision(18, 4);
        builder.Property(e => e.TolerancePercent).HasPrecision(18, 4);
        builder.Property(e => e.Notes).HasMaxLength(1024);
        builder.Property(e => e.CancelReason).HasMaxLength(512);

        builder.HasIndex(e => new { e.TenantId, e.PrimaryDocumentId, e.VersionNo });

        builder.HasOne(e => e.PrimaryDocument)
            .WithMany()
            .HasForeignKey(e => e.PrimaryDocumentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DocumentMatchDetailConfiguration : IEntityTypeConfiguration<DocumentMatchDetail>
{
    public void Configure(EntityTypeBuilder<DocumentMatchDetail> builder)
    {
        builder.ToTable("document_match_details");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.MatchId).IsRequired();
        builder.Property(e => e.SourceLineId).IsRequired();
        builder.Property(e => e.MatchedAmount).HasPrecision(18, 4);
        builder.Property(e => e.DetailStatus).HasMaxLength(32).IsRequired();
        builder.Property(e => e.ReverseReason).HasMaxLength(512);

        builder.HasIndex(e => new { e.TenantId, e.MatchId });
        builder.HasIndex(e => new { e.TenantId, e.SourceLineId });
        builder.HasIndex(e => new { e.TenantId, e.TargetLineId });
        builder.HasIndex(e => new { e.TenantId, e.DetailStatus });

        builder.HasOne(e => e.Match)
            .WithMany()
            .HasForeignKey(e => e.MatchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.SourceLine)
            .WithMany()
            .HasForeignKey(e => e.SourceLineId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.TargetLine)
            .WithMany()
            .HasForeignKey(e => e.TargetLineId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.TargetCost)
            .WithMany()
            .HasForeignKey(e => e.TargetCostId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.TargetRevenue)
            .WithMany()
            .HasForeignKey(e => e.TargetRevenueId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PayableExposureConfiguration : IEntityTypeConfiguration<PayableExposure>
{
    public void Configure(EntityTypeBuilder<PayableExposure> builder)
    {
        builder.ToTable("payable_exposures");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.Amount).HasPrecision(18, 4);
        builder.Property(e => e.RecognizedAmount).HasPrecision(18, 4);
        builder.Property(e => e.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(2048);
        builder.Property(e => e.SourceType).HasMaxLength(64);
        builder.Property(e => e.RecordStatus).HasMaxLength(32).IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.Status, e.EffectiveDate });
        builder.HasIndex(e => new { e.TenantId, e.CounterpartyId, e.DueDate });
        builder.HasIndex(e => new { e.TenantId, e.BillId });

        builder.HasOne(e => e.Bill)
            .WithMany()
            .HasForeignKey(e => e.BillId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Cost)
            .WithMany()
            .HasForeignKey(e => e.CostId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.FinancialDocument)
            .WithMany()
            .HasForeignKey(e => e.FinancialDocumentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ReceivableExposureConfiguration : IEntityTypeConfiguration<ReceivableExposure>
{
    public void Configure(EntityTypeBuilder<ReceivableExposure> builder)
    {
        builder.ToTable("receivable_exposures");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.Amount).HasPrecision(18, 4);
        builder.Property(e => e.RecognizedAmount).HasPrecision(18, 4);
        builder.Property(e => e.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(2048);
        builder.Property(e => e.SourceType).HasMaxLength(64);
        builder.Property(e => e.RecordStatus).HasMaxLength(32).IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.Status, e.EffectiveDate });
        builder.HasIndex(e => new { e.TenantId, e.CounterpartyId, e.DueDate });
        builder.HasIndex(e => new { e.TenantId, e.BillId });

        builder.HasOne(e => e.Bill)
            .WithMany()
            .HasForeignKey(e => e.BillId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Revenue)
            .WithMany()
            .HasForeignKey(e => e.RevenueId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.FinancialDocument)
            .WithMany()
            .HasForeignKey(e => e.FinancialDocumentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AccountsPayableConfiguration : IEntityTypeConfiguration<AccountsPayable>
{
    public void Configure(EntityTypeBuilder<AccountsPayable> builder)
    {
        builder.ToTable("accounts_payable");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.PayableExposureId).IsRequired();
        builder.Property(e => e.RecognizedAmount).HasPrecision(18, 4);
        builder.Property(e => e.AdjustmentAmount).HasPrecision(18, 4);
        builder.Property(e => e.FinalizedSettledAmount).HasPrecision(18, 4);
        builder.Property(e => e.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(e => e.SettlementStatus).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(2048);
        builder.Property(e => e.RecordStatus).HasMaxLength(32).IsRequired();

        // IDX-007
        builder.HasIndex(e => new { e.TenantId, e.CounterpartyId, e.DueDate, e.SettlementStatus });
        builder.HasIndex(e => new { e.TenantId, e.PayableExposureId });

        builder.HasOne(e => e.PayableExposure)
            .WithMany()
            .HasForeignKey(e => e.PayableExposureId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Bill)
            .WithMany()
            .HasForeignKey(e => e.BillId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AccountsReceivableConfiguration : IEntityTypeConfiguration<AccountsReceivable>
{
    public void Configure(EntityTypeBuilder<AccountsReceivable> builder)
    {
        builder.ToTable("accounts_receivable");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.ReceivableExposureId).IsRequired();
        builder.Property(e => e.RecognizedAmount).HasPrecision(18, 4);
        builder.Property(e => e.AdjustmentAmount).HasPrecision(18, 4);
        builder.Property(e => e.FinalizedSettledAmount).HasPrecision(18, 4);
        builder.Property(e => e.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(e => e.SettlementStatus).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(2048);
        builder.Property(e => e.RecordStatus).HasMaxLength(32).IsRequired();

        // IDX-008
        builder.HasIndex(e => new { e.TenantId, e.CounterpartyId, e.DueDate, e.SettlementStatus });
        builder.HasIndex(e => new { e.TenantId, e.ReceivableExposureId });

        builder.HasOne(e => e.ReceivableExposure)
            .WithMany()
            .HasForeignKey(e => e.ReceivableExposureId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Bill)
            .WithMany()
            .HasForeignKey(e => e.BillId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AccountsPayableAdjustmentConfiguration : IEntityTypeConfiguration<AccountsPayableAdjustment>
{
    public void Configure(EntityTypeBuilder<AccountsPayableAdjustment> builder)
    {
        builder.ToTable("accounts_payable_adjustments");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.AccountsPayableId).IsRequired();
        builder.Property(e => e.AdjustmentType).HasMaxLength(32).IsRequired();
        builder.Property(e => e.DeltaAmount).HasPrecision(18, 4);
        builder.Property(e => e.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(1024).IsRequired();
        builder.Property(e => e.EffectiveDate).IsRequired();
        builder.Property(e => e.AdjustmentAmountBefore).HasPrecision(18, 4);
        builder.Property(e => e.AdjustmentAmountAfter).HasPrecision(18, 4);
        builder.Property(e => e.OutstandingBefore).HasPrecision(18, 4);
        builder.Property(e => e.OutstandingAfter).HasPrecision(18, 4);

        builder.HasIndex(e => new { e.TenantId, e.AccountsPayableId, e.CreatedAt });

        builder.HasOne(e => e.AccountsPayable)
            .WithMany()
            .HasForeignKey(e => e.AccountsPayableId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AccountsReceivableAdjustmentConfiguration : IEntityTypeConfiguration<AccountsReceivableAdjustment>
{
    public void Configure(EntityTypeBuilder<AccountsReceivableAdjustment> builder)
    {
        builder.ToTable("accounts_receivable_adjustments");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.AccountsReceivableId).IsRequired();
        builder.Property(e => e.AdjustmentType).HasMaxLength(32).IsRequired();
        builder.Property(e => e.DeltaAmount).HasPrecision(18, 4);
        builder.Property(e => e.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(1024).IsRequired();
        builder.Property(e => e.EffectiveDate).IsRequired();
        builder.Property(e => e.AdjustmentAmountBefore).HasPrecision(18, 4);
        builder.Property(e => e.AdjustmentAmountAfter).HasPrecision(18, 4);
        builder.Property(e => e.OutstandingBefore).HasPrecision(18, 4);
        builder.Property(e => e.OutstandingAfter).HasPrecision(18, 4);

        builder.HasIndex(e => new { e.TenantId, e.AccountsReceivableId, e.CreatedAt });

        builder.HasOne(e => e.AccountsReceivable)
            .WithMany()
            .HasForeignKey(e => e.AccountsReceivableId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class TenantSettingConfiguration : IEntityTypeConfiguration<TenantSetting>
{
    public void Configure(EntityTypeBuilder<TenantSetting> builder)
    {
        builder.ToTable("tenant_settings");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.UiJson).HasColumnType("text");
        builder.Property(e => e.FinancialJson).HasColumnType("text");
        builder.HasIndex(e => e.TenantId).IsUnique();
    }
}

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.Amount).HasPrecision(18, 4);
        builder.Property(e => e.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(e => e.BaseAmount).HasPrecision(18, 4);
        builder.Property(e => e.ReferenceNo).HasMaxLength(128);
        builder.Property(e => e.Notes).HasMaxLength(2048);
        builder.Property(e => e.Status).HasMaxLength(32).IsRequired();
        builder.Property(e => e.RecordStatus).HasMaxLength(32).IsRequired();

        // IDX-009
        builder.HasIndex(e => new { e.TenantId, e.ValueDate, e.CounterpartyId });
        builder.HasIndex(e => new { e.TenantId, e.BillId });

        builder.HasOne(e => e.Bill)
            .WithMany()
            .HasForeignKey(e => e.BillId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CollectionConfiguration : IEntityTypeConfiguration<Collection>
{
    public void Configure(EntityTypeBuilder<Collection> builder)
    {
        builder.ToTable("collections");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.Amount).HasPrecision(18, 4);
        builder.Property(e => e.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(e => e.BaseAmount).HasPrecision(18, 4);
        builder.Property(e => e.ReferenceNo).HasMaxLength(128);
        builder.Property(e => e.Notes).HasMaxLength(2048);
        builder.Property(e => e.Status).HasMaxLength(32).IsRequired();
        builder.Property(e => e.RecordStatus).HasMaxLength(32).IsRequired();

        // IDX-010
        builder.HasIndex(e => new { e.TenantId, e.ValueDate, e.CounterpartyId });
        builder.HasIndex(e => new { e.TenantId, e.BillId });

        builder.HasOne(e => e.Bill)
            .WithMany()
            .HasForeignKey(e => e.BillId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PaymentAllocationConfiguration : IEntityTypeConfiguration<PaymentAllocation>
{
    public void Configure(EntityTypeBuilder<PaymentAllocation> builder)
    {
        builder.ToTable("payment_allocations");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.PaymentId).IsRequired();
        builder.Property(e => e.AccountsPayableId).IsRequired();
        builder.Property(e => e.Amount).HasPrecision(18, 4);
        builder.Property(e => e.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(e => e.BaseAmount).HasPrecision(18, 4);
        builder.Property(e => e.AllocationStatus).HasMaxLength(32).IsRequired();
        builder.Property(e => e.ReverseReason).HasMaxLength(1024);
        builder.Property(e => e.Notes).HasMaxLength(2048);

        builder.HasIndex(e => new { e.TenantId, e.PaymentId, e.AllocationStatus });
        builder.HasIndex(e => new { e.TenantId, e.AccountsPayableId, e.AllocationStatus });

        builder.HasOne(e => e.Payment)
            .WithMany()
            .HasForeignKey(e => e.PaymentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.AccountsPayable)
            .WithMany()
            .HasForeignKey(e => e.AccountsPayableId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CollectionAllocationConfiguration : IEntityTypeConfiguration<CollectionAllocation>
{
    public void Configure(EntityTypeBuilder<CollectionAllocation> builder)
    {
        builder.ToTable("collection_allocations");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.CollectionId).IsRequired();
        builder.Property(e => e.AccountsReceivableId).IsRequired();
        builder.Property(e => e.Amount).HasPrecision(18, 4);
        builder.Property(e => e.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(e => e.BaseAmount).HasPrecision(18, 4);
        builder.Property(e => e.AllocationStatus).HasMaxLength(32).IsRequired();
        builder.Property(e => e.ReverseReason).HasMaxLength(1024);
        builder.Property(e => e.Notes).HasMaxLength(2048);

        builder.HasIndex(e => new { e.TenantId, e.CollectionId, e.AllocationStatus });
        builder.HasIndex(e => new { e.TenantId, e.AccountsReceivableId, e.AllocationStatus });

        builder.HasOne(e => e.Collection)
            .WithMany()
            .HasForeignKey(e => e.CollectionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.AccountsReceivable)
            .WithMany()
            .HasForeignKey(e => e.AccountsReceivableId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ReconciliationConfiguration : IEntityTypeConfiguration<Reconciliation>
{
    public void Configure(EntityTypeBuilder<Reconciliation> builder)
    {
        builder.ToTable("reconciliations");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.ReconciliationType).HasMaxLength(64).IsRequired();
        builder.Property(e => e.RuleCode).HasMaxLength(128);
        builder.Property(e => e.VersionNo).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(2048);

        builder.HasIndex(e => new { e.TenantId, e.Status, e.ReconciliationType });
        builder.HasIndex(e => new { e.TenantId, e.BillId, e.VersionNo });

        builder.HasOne(e => e.Bill)
            .WithMany()
            .HasForeignKey(e => e.BillId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ReconciliationDetailConfiguration : IEntityTypeConfiguration<ReconciliationDetail>
{
    public void Configure(EntityTypeBuilder<ReconciliationDetail> builder)
    {
        builder.ToTable("reconciliation_details");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.ReconciliationId).IsRequired();
        builder.Property(e => e.SourceType).HasMaxLength(64).IsRequired();
        builder.Property(e => e.SourceId).IsRequired();
        builder.Property(e => e.TargetType).HasMaxLength(64);
        builder.Property(e => e.SourceAmount).HasPrecision(18, 4);
        builder.Property(e => e.TargetAmount).HasPrecision(18, 4);
        builder.Property(e => e.MatchedAmount).HasPrecision(18, 4);
        builder.Property(e => e.VarianceAmount).HasPrecision(18, 4);
        builder.Property(e => e.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(e => e.LineStatus).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(2048);

        builder.HasIndex(e => new { e.TenantId, e.ReconciliationId });
        builder.HasIndex(e => new { e.TenantId, e.SourceType, e.SourceId });
        builder.HasIndex(e => new { e.TenantId, e.VarianceId });

        builder.HasOne(e => e.Reconciliation)
            .WithMany()
            .HasForeignKey(e => e.ReconciliationId)
            .OnDelete(DeleteBehavior.Restrict);

        // VarianceId is a soft link stamped after variance insert; no required FK cycle.
        builder.Ignore(e => e.Variance);
    }
}

internal sealed class VarianceConfiguration : IEntityTypeConfiguration<Variance>
{
    public void Configure(EntityTypeBuilder<Variance> builder)
    {
        builder.ToTable("variances");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.VarianceType).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Amount).HasPrecision(18, 4);
        builder.Property(e => e.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(e => e.SourceType).HasMaxLength(64).IsRequired();
        builder.Property(e => e.TargetType).HasMaxLength(64);
        builder.Property(e => e.Status).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Severity).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Explanation).HasMaxLength(2048);

        builder.HasIndex(e => new { e.TenantId, e.Status, e.VarianceType });
        builder.HasIndex(e => new { e.TenantId, e.Severity, e.Status });
        builder.HasIndex(e => new { e.TenantId, e.ReconciliationId });
        builder.HasIndex(e => new { e.TenantId, e.ExceptionId });

        builder.HasOne(e => e.Reconciliation)
            .WithMany()
            .HasForeignKey(e => e.ReconciliationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ReconciliationDetail)
            .WithMany()
            .HasForeignKey(e => e.ReconciliationDetailId)
            .OnDelete(DeleteBehavior.Restrict);

        // ExceptionId soft link — avoid FK cycle with exceptions.variance_id.
        builder.Ignore(e => e.Exception);
    }
}

internal sealed class FinancialExceptionConfiguration : IEntityTypeConfiguration<FinancialException>
{
    public void Configure(EntityTypeBuilder<FinancialException> builder)
    {
        builder.ToTable("exceptions");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.RuleCode).HasMaxLength(128).IsRequired();
        builder.Property(e => e.Severity).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Title).HasMaxLength(256).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(4096);
        builder.Property(e => e.ResolutionNotes).HasMaxLength(2048);
        builder.Property(e => e.ObjectType).HasMaxLength(64);
        builder.Property(e => e.EscalationReason).HasMaxLength(2048);

        // IDX-011
        builder.HasIndex(e => new { e.TenantId, e.Status, e.Severity, e.OwnerId, e.DueAt });
        builder.HasIndex(e => new { e.TenantId, e.VarianceId });
        builder.HasIndex(e => new { e.TenantId, e.ReconciliationId });
        builder.HasIndex(e => new { e.TenantId, e.ObjectType, e.ObjectId, e.Status });

        builder.HasOne(e => e.Bill)
            .WithMany()
            .HasForeignKey(e => e.BillId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Reconciliation)
            .WithMany()
            .HasForeignKey(e => e.ReconciliationId)
            .OnDelete(DeleteBehavior.Restrict);

        // VarianceId soft link — avoid FK cycle with variances.exception_id.
        builder.Ignore(e => e.Variance);
    }
}

internal sealed class ApprovalConfiguration : IEntityTypeConfiguration<Approval>
{
    public void Configure(EntityTypeBuilder<Approval> builder)
    {
        builder.ToTable("approvals");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.ObjectType).HasMaxLength(64).IsRequired();
        builder.Property(e => e.ObjectId).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(32).IsRequired();
        builder.Property(e => e.RequiredLevel).IsRequired();
        builder.Property(e => e.CurrentLevel).IsRequired();
        builder.Property(e => e.RequestReason).HasMaxLength(2048);
        builder.Property(e => e.DecisionReason).HasMaxLength(2048);
        builder.Property(e => e.Notes).HasMaxLength(2048);
        builder.Property(e => e.RequestedAt).IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.ObjectType, e.ObjectId, e.Status });
        builder.HasIndex(e => new { e.TenantId, e.Status, e.RequestedAt });
        builder.HasIndex(e => new { e.TenantId, e.RequiredLevel, e.CurrentLevel, e.Status });
    }
}

internal sealed class FinancialCloseConfiguration : IEntityTypeConfiguration<FinancialClose>
{
    public void Configure(EntityTypeBuilder<FinancialClose> builder)
    {
        builder.ToTable("financial_closes");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.ScopeType).HasMaxLength(32).IsRequired();
        builder.Property(e => e.VersionNo).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(32).IsRequired();
        builder.Property(e => e.PolicyVersion).HasMaxLength(64).IsRequired();
        builder.Property(e => e.BaseCurrency).HasMaxLength(3).IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(2048);
        builder.Property(e => e.ReopenReason).HasMaxLength(2048);

        builder.HasIndex(e => new { e.TenantId, e.ScopeType, e.ScopeId, e.VersionNo });
        builder.HasIndex(e => new { e.TenantId, e.Status, e.PeriodFrom, e.PeriodTo });

        builder.HasOne(e => e.Bill)
            .WithMany()
            .HasForeignKey(e => e.ScopeId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}

internal sealed class FinancialCloseSnapshotConfiguration : IEntityTypeConfiguration<FinancialCloseSnapshot>
{
    public void Configure(EntityTypeBuilder<FinancialCloseSnapshot> builder)
    {
        builder.ToTable("financial_close_snapshots");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.FinancialCloseId).IsRequired();
        builder.Property(e => e.ScopeType).HasMaxLength(32).IsRequired();
        builder.Property(e => e.SnapshotVersion).IsRequired();
        builder.Property(e => e.ClosedAt).IsRequired();
        builder.Property(e => e.PolicyVersion).HasMaxLength(64).IsRequired();
        builder.Property(e => e.BaseCurrency).HasMaxLength(3).IsRequired();
        builder.Property(e => e.ImmutableHash).HasMaxLength(128).IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.FinancialCloseId, e.SnapshotVersion }).IsUnique();
        builder.HasIndex(e => new { e.TenantId, e.ScopeType, e.ScopeId, e.ClosedAt });
        builder.HasIndex(e => new { e.TenantId, e.ImmutableHash });

        builder.HasOne(e => e.FinancialClose)
            .WithMany()
            .HasForeignKey(e => e.FinancialCloseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class FinancialCloseSnapshotDetailConfiguration : IEntityTypeConfiguration<FinancialCloseSnapshotDetail>
{
    public void Configure(EntityTypeBuilder<FinancialCloseSnapshotDetail> builder)
    {
        builder.ToTable("financial_close_snapshot_details");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.SnapshotId).IsRequired();
        builder.Property(e => e.LineNo).IsRequired();
        builder.Property(e => e.MetricKey).HasMaxLength(64).IsRequired();
        builder.Property(e => e.MetricValue).HasPrecision(18, 4);
        builder.Property(e => e.CurrencyCode).HasMaxLength(3);
        builder.Property(e => e.SourceType).HasMaxLength(64);
        builder.Property(e => e.Notes).HasMaxLength(2048);

        builder.HasIndex(e => new { e.TenantId, e.SnapshotId, e.LineNo }).IsUnique();
        builder.HasIndex(e => new { e.TenantId, e.SnapshotId, e.MetricKey });

        builder.HasOne(e => e.Snapshot)
            .WithMany()
            .HasForeignKey(e => e.SnapshotId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("audit_events");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.Action).HasMaxLength(64).IsRequired();
        builder.Property(e => e.ObjectType).HasMaxLength(64).IsRequired();
        builder.Property(e => e.ObjectId).IsRequired();
        builder.Property(e => e.BeforeJson);
        builder.Property(e => e.AfterJson);
        builder.Property(e => e.Reason).HasMaxLength(2048);
        builder.Property(e => e.CorrelationId).HasMaxLength(128);
        builder.Property(e => e.OccurredAt).IsRequired();

        // IDX-013
        builder.HasIndex(e => new { e.TenantId, e.ObjectType, e.ObjectId, e.OccurredAt });
        builder.HasIndex(e => new { e.TenantId, e.Action, e.OccurredAt });
        builder.HasIndex(e => new { e.TenantId, e.CorrelationId });
    }
}

internal sealed class IntegrationRecordConfiguration : IEntityTypeConfiguration<IntegrationRecord>
{
    public void Configure(EntityTypeBuilder<IntegrationRecord> builder)
    {
        builder.ToTable("integration_records");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.SourceSystem).HasMaxLength(64).IsRequired();
        builder.Property(e => e.ExternalObjectType).HasMaxLength(64).IsRequired();
        builder.Property(e => e.ExternalId).HasMaxLength(128).IsRequired();
        builder.Property(e => e.ExternalVersion).HasMaxLength(64);
        builder.Property(e => e.Status).HasMaxLength(32).IsRequired();
        builder.Property(e => e.LocalObjectType).HasMaxLength(64);
        builder.Property(e => e.PayloadHash).HasMaxLength(128);
        builder.Property(e => e.Notes).HasMaxLength(2048);
        builder.Property(e => e.ReceivedAt).IsRequired();

        // C-002 / IDX-012
        builder.HasIndex(e => new { e.TenantId, e.SourceSystem, e.ExternalObjectType, e.ExternalId })
            .IsUnique();
        builder.HasIndex(e => new { e.TenantId, e.Status, e.ReceivedAt });
    }
}

internal sealed class IntegrationErrorConfiguration : IEntityTypeConfiguration<IntegrationError>
{
    public void Configure(EntityTypeBuilder<IntegrationError> builder)
    {
        builder.ToTable("integration_errors");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.IntegrationRecordId).IsRequired();
        builder.Property(e => e.ErrorCode).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Message).HasMaxLength(2048).IsRequired();
        builder.Property(e => e.Detail).HasMaxLength(8192);
        builder.Property(e => e.AttemptNo).IsRequired();
        builder.Property(e => e.OccurredAt).IsRequired();
        builder.Property(e => e.RecoveryStatus).HasMaxLength(32).IsRequired();
        builder.Property(e => e.RecoveryNote).HasMaxLength(2048);

        builder.HasIndex(e => new { e.TenantId, e.IntegrationRecordId, e.AttemptNo });
        builder.HasIndex(e => new { e.TenantId, e.OccurredAt });
        builder.HasIndex(e => new { e.TenantId, e.RecoveryStatus, e.OccurredAt });

        builder.HasOne(e => e.IntegrationRecord)
            .WithMany(r => r.Errors)
            .HasForeignKey(e => e.IntegrationRecordId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.Topic).HasMaxLength(128).IsRequired();
        builder.Property(e => e.PayloadJson).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(32).IsRequired();
        builder.Property(e => e.LastError).HasMaxLength(2048);
        builder.Property(e => e.EnqueuedAt).IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.Status, e.EnqueuedAt });
        builder.HasIndex(e => new { e.TenantId, e.Topic, e.EnqueuedAt });
    }
}

internal sealed class BankFeedLineConfiguration : IEntityTypeConfiguration<BankFeedLine>
{
    public void Configure(EntityTypeBuilder<BankFeedLine> builder)
    {
        builder.ToTable("bank_feed_lines");
        EntityBaseConfiguration.ConfigureEntityBase(builder);
        builder.Property(e => e.TenantId).HasColumnType("uuid").IsRequired();
        builder.Property(e => e.ValueDate).IsRequired();
        builder.Property(e => e.Amount).HasPrecision(18, 4);
        builder.Property(e => e.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(e => e.Direction).HasMaxLength(16).IsRequired();
        builder.Property(e => e.BankReference).HasMaxLength(128);
        builder.Property(e => e.CounterpartyName).HasMaxLength(256);
        builder.Property(e => e.Description).HasMaxLength(2048);
        builder.Property(e => e.Status).HasMaxLength(32).IsRequired();
        builder.Property(e => e.IgnoreReason).HasMaxLength(2048);

        builder.HasIndex(e => new { e.TenantId, e.Status, e.ValueDate });
        builder.HasIndex(e => new { e.TenantId, e.BankReference });
        builder.HasIndex(e => new { e.TenantId, e.MatchedReconciliationDetailId });
    }
}
