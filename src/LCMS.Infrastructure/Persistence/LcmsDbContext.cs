using LCMS.Application.Abstractions;
using LCMS.Domain.Common;
using LCMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Infrastructure.Persistence;

public sealed class LcmsDbContext : DbContext, ILcmsDbContext
{
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _userContext;

    public LcmsDbContext(
        DbContextOptions<LcmsDbContext> options,
        ITenantContext tenantContext,
        ICurrentUserContext userContext)
        : base(options)
    {
        _tenantContext = tenantContext;
        _userContext = userContext;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Bill> Bills => Set<Bill>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<User> Users => Set<User>();
    public DbSet<BusinessParty> BusinessParties => Set<BusinessParty>();
    public DbSet<PartyRole> PartyRoles => Set<PartyRole>();
    public DbSet<PartyBankAccount> PartyBankAccounts => Set<PartyBankAccount>();
    public DbSet<PartyContact> PartyContacts => Set<PartyContact>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<FxRate> FxRates => Set<FxRate>();
    public DbSet<Cost> Costs => Set<Cost>();
    public DbSet<CostAdjustment> CostAdjustments => Set<CostAdjustment>();
    public DbSet<CostAllocation> CostAllocations => Set<CostAllocation>();
    public DbSet<CostAllocationDetail> CostAllocationDetails => Set<CostAllocationDetail>();
    public DbSet<Revenue> Revenues => Set<Revenue>();
    public DbSet<RevenueAdjustment> RevenueAdjustments => Set<RevenueAdjustment>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<OrderBillLink> OrderBillLinks => Set<OrderBillLink>();
    public DbSet<BillShipmentLink> BillShipmentLinks => Set<BillShipmentLink>();
    public DbSet<TransportLeg> TransportLegs => Set<TransportLeg>();
    public DbSet<TransportMovement> TransportMovements => Set<TransportMovement>();
    public DbSet<BillLegLink> BillLegLinks => Set<BillLegLink>();
    public DbSet<LegMovementLink> LegMovementLinks => Set<LegMovementLink>();
    public DbSet<BillMovementLink> BillMovementLinks => Set<BillMovementLink>();
    public DbSet<RateCard> RateCards => Set<RateCard>();
    public DbSet<RateVersion> RateVersions => Set<RateVersion>();
    public DbSet<PricingRule> PricingRules => Set<PricingRule>();
    public DbSet<PricingRuleComponent> PricingRuleComponents => Set<PricingRuleComponent>();
    public DbSet<Rating> Ratings => Set<Rating>();
    public DbSet<RatingDetail> RatingDetails => Set<RatingDetail>();
    public DbSet<FinancialDocument> FinancialDocuments => Set<FinancialDocument>();
    public DbSet<FinancialDocumentLine> FinancialDocumentLines => Set<FinancialDocumentLine>();
    public DbSet<DocumentMatch> DocumentMatches => Set<DocumentMatch>();
    public DbSet<DocumentMatchDetail> DocumentMatchDetails => Set<DocumentMatchDetail>();
    public DbSet<PayableExposure> PayableExposures => Set<PayableExposure>();
    public DbSet<ReceivableExposure> ReceivableExposures => Set<ReceivableExposure>();
    public DbSet<AccountsPayable> AccountsPayable => Set<AccountsPayable>();
    public DbSet<AccountsPayableAdjustment> AccountsPayableAdjustments => Set<AccountsPayableAdjustment>();
    public DbSet<AccountsReceivable> AccountsReceivable => Set<AccountsReceivable>();
    public DbSet<AccountsReceivableAdjustment> AccountsReceivableAdjustments => Set<AccountsReceivableAdjustment>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Collection> Collections => Set<Collection>();
    public DbSet<PaymentAllocation> PaymentAllocations => Set<PaymentAllocation>();
    public DbSet<CollectionAllocation> CollectionAllocations => Set<CollectionAllocation>();
    public DbSet<Reconciliation> Reconciliations => Set<Reconciliation>();
    public DbSet<ReconciliationDetail> ReconciliationDetails => Set<ReconciliationDetail>();
    public DbSet<BankFeedLine> BankFeedLines => Set<BankFeedLine>();
    public DbSet<Variance> Variances => Set<Variance>();
    public DbSet<FinancialException> Exceptions => Set<FinancialException>();
    public DbSet<Approval> Approvals => Set<Approval>();
    public DbSet<FinancialClose> FinancialCloses => Set<FinancialClose>();
    public DbSet<FinancialCloseSnapshot> FinancialCloseSnapshots => Set<FinancialCloseSnapshot>();
    public DbSet<FinancialCloseSnapshotDetail> FinancialCloseSnapshotDetails => Set<FinancialCloseSnapshotDetail>();
    public DbSet<TenantSetting> TenantSettings => Set<TenantSetting>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<IntegrationRecord> IntegrationRecords => Set<IntegrationRecord>();
    public DbSet<IntegrationError> IntegrationErrors => Set<IntegrationError>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LcmsDbContext).Assembly);
        ApplyTenantAndSoftDeleteFilters(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampAuditAndConcurrency();
        RejectHardDeletes();
        RejectImmutableCloseSnapshotMutations();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        StampAuditAndConcurrency();
        RejectHardDeletes();
        RejectImmutableCloseSnapshotMutations();
        return base.SaveChanges();
    }

    /// <summary>
    /// C-001 global tenant filter + C-013 soft-delete filter.
    /// Tenant root table filters by soft-delete only (Id is the tenant).
    /// </summary>
    private void ApplyTenantAndSoftDeleteFilters(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>().HasQueryFilter(t => t.DeletedAt == null);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.ClrType == typeof(Tenant))
            {
                continue;
            }

            if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            if (typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType))
            {
                // Captures this DbContext instance for current tenant resolution.
                var method = typeof(LcmsDbContext)
                    .GetMethod(nameof(SetTenantScopedFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .MakeGenericMethod(entityType.ClrType);
                method.Invoke(this, [modelBuilder]);
            }
            else
            {
                var method = typeof(LcmsDbContext)
                    .GetMethod(nameof(SetSoftDeleteFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .MakeGenericMethod(entityType.ClrType);
                method.Invoke(this, [modelBuilder]);
            }
        }
    }

    private void SetTenantScopedFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantScoped, ISoftDeletable
    {
        // Expression-tree safe: do not call ITenantContext.HasTenant default member.
        modelBuilder.Entity<TEntity>().HasQueryFilter(e =>
            e.DeletedAt == null
            && (_tenantContext.TenantId == null
                || _tenantContext.TenantId == Guid.Empty
                || e.TenantId == _tenantContext.TenantId));
    }

    private void SetSoftDeleteFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ISoftDeletable
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => e.DeletedAt == null);
    }

    private void StampAuditAndConcurrency()
    {
        var utc = DateTimeOffset.UtcNow;
        var actorId = _userContext.HasUser ? _userContext.UserId : null;
        foreach (var entry in ChangeTracker.Entries<EntityBase>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.TouchRowVersion();
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedAt = utc;
                    if (actorId.HasValue && entry.Entity.CreatedBy is null)
                    {
                        entry.Entity.CreatedBy = actorId;
                    }
                }
                else
                {
                    entry.Entity.UpdatedAt = utc;
                    if (actorId.HasValue)
                    {
                        entry.Entity.UpdatedBy = actorId;
                    }
                }
            }
        }
    }

    /// <summary>C-013 / TD1-DB-005: block physical DELETE; use SoftDelete().</summary>
    private void RejectHardDeletes()
    {
        var hardDeletes = ChangeTracker.Entries<ISoftDeletable>()
            .Where(e => e.State == EntityState.Deleted)
            .ToList();

        if (hardDeletes.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "Cấm xóa vật lý (hard delete). Dùng SoftDelete() hoặc cancel/reversal/adjustment theo TD1 C-013.");
    }

    /// <summary>C-010 / AC-008: closed snapshots and details are insert-only.</summary>
    private void RejectImmutableCloseSnapshotMutations()
    {
        var illegal = ChangeTracker.Entries()
            .Where(e =>
                e.Entity is FinancialCloseSnapshot or FinancialCloseSnapshotDetail
                && e.State is EntityState.Modified or EntityState.Deleted)
            .ToList();

        if (illegal.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "Cấm sửa hoặc xóa bản chốt tài chính (C-010). Mở lại / chốt lại để tạo phiên bản snapshot mới.");
    }
}
