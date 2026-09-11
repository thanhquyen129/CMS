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
        builder.Property(e => e.Amount).HasPrecision(18, 4);
        builder.Property(e => e.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(e => e.FinancialMaturity).HasMaxLength(32).IsRequired();
        builder.Property(e => e.RecordStatus).HasMaxLength(32).IsRequired();
        builder.HasIndex(e => new { e.TenantId, e.BillId, e.FinancialMaturity });
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
