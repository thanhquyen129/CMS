using LCMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Abstractions;

public interface ILcmsDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<Bill> Bills { get; }
    DbSet<Organization> Organizations { get; }
    DbSet<User> Users { get; }
    DbSet<BusinessParty> BusinessParties { get; }
    DbSet<Role> Roles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<Currency> Currencies { get; }
    DbSet<Cost> Costs { get; }
    DbSet<CostAdjustment> CostAdjustments { get; }
    DbSet<CostAllocation> CostAllocations { get; }
    DbSet<CostAllocationDetail> CostAllocationDetails { get; }
    DbSet<Revenue> Revenues { get; }
    DbSet<RevenueAdjustment> RevenueAdjustments { get; }
    DbSet<Order> Orders { get; }
    DbSet<Shipment> Shipments { get; }
    DbSet<OrderBillLink> OrderBillLinks { get; }
    DbSet<BillShipmentLink> BillShipmentLinks { get; }
    DbSet<RateCard> RateCards { get; }
    DbSet<RateVersion> RateVersions { get; }
    DbSet<PricingRule> PricingRules { get; }
    DbSet<PricingRuleComponent> PricingRuleComponents { get; }
    DbSet<Rating> Ratings { get; }
    DbSet<RatingDetail> RatingDetails { get; }
    DbSet<FinancialDocument> FinancialDocuments { get; }
    DbSet<FinancialDocumentLine> FinancialDocumentLines { get; }
    DbSet<DocumentMatch> DocumentMatches { get; }
    DbSet<DocumentMatchDetail> DocumentMatchDetails { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
