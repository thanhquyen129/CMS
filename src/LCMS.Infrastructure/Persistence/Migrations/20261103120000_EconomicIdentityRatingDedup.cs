using LCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LCMS.Infrastructure.Persistence.Migrations;

[DbContext(typeof(LcmsDbContext))]
[Migration("20261103120000_EconomicIdentityRatingDedup")]
public class EconomicIdentityRatingDedup : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "economic_identity_key",
            table: "costs",
            type: "character varying(256)",
            maxLength: 256,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "service_occurrence_key",
            table: "costs",
            type: "character varying(128)",
            maxLength: 128,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "applied_from_rating_id",
            table: "costs",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "economic_identity_key",
            table: "revenues",
            type: "character varying(256)",
            maxLength: 256,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "service_occurrence_key",
            table: "revenues",
            type: "character varying(128)",
            maxLength: 128,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "applied_from_rating_id",
            table: "revenues",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "ix_costs_tenant_id_economic_identity_key_record_status",
            table: "costs",
            columns: new[] { "tenant_id", "economic_identity_key", "record_status" });

        migrationBuilder.CreateIndex(
            name: "ix_revenues_tenant_id_economic_identity_key_record_status",
            table: "revenues",
            columns: new[] { "tenant_id", "economic_identity_key", "record_status" });

        // Best-effort backfill for rating-sourced direct costs (MG-08).
        migrationBuilder.Sql("""
            UPDATE costs c
            SET economic_identity_key = lower(replace(c.tenant_id::text, '-', ''))
                || '|' || lower(replace(coalesce(c.bill_id::text, 'bill:none'), '-', ''))
                || '|BUY|'
                || upper(coalesce(c.cost_type_code, 'UNKNOWN'))
                || '|' || CASE
                    WHEN c.transport_leg_id IS NOT NULL THEN 'leg:' || lower(replace(c.transport_leg_id::text, '-', ''))
                    WHEN c.shipment_id IS NOT NULL THEN 'shp:' || lower(replace(c.shipment_id::text, '-', ''))
                    WHEN c.bill_id IS NOT NULL THEN 'bill:' || lower(replace(c.bill_id::text, '-', ''))
                    ELSE 'scope:none'
                END
                || '|' || CASE
                    WHEN c.vendor_party_id IS NOT NULL THEN lower(replace(c.vendor_party_id::text, '-', ''))
                    ELSE 'cp:none'
                END,
                service_occurrence_key = CASE
                    WHEN c.transport_leg_id IS NOT NULL THEN 'leg:' || lower(replace(c.transport_leg_id::text, '-', ''))
                    WHEN c.bill_id IS NOT NULL THEN 'bill:' || lower(replace(c.bill_id::text, '-', ''))
                    ELSE 'scope:none'
                END
            WHERE c.economic_identity_key IS NULL
              AND c.deleted_at IS NULL;
            """);

        migrationBuilder.Sql("""
            UPDATE revenues r
            SET economic_identity_key = lower(replace(r.tenant_id::text, '-', ''))
                || '|' || lower(replace(r.bill_id::text, '-', ''))
                || '|SELL|'
                || upper(coalesce(r.revenue_type_code, 'UNKNOWN'))
                || '|bill:' || lower(replace(r.bill_id::text, '-', '')) || ':' || upper(coalesce(r.revenue_type_code, 'UNKNOWN'))
                || '|' || CASE
                    WHEN r.customer_party_id IS NOT NULL THEN lower(replace(r.customer_party_id::text, '-', ''))
                    ELSE 'cp:none'
                END,
                service_occurrence_key = 'bill:' || lower(replace(r.bill_id::text, '-', ''))
                    || ':' || upper(coalesce(r.revenue_type_code, 'UNKNOWN'))
            WHERE r.economic_identity_key IS NULL
              AND r.deleted_at IS NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_costs_tenant_id_economic_identity_key_record_status",
            table: "costs");
        migrationBuilder.DropIndex(
            name: "ix_revenues_tenant_id_economic_identity_key_record_status",
            table: "revenues");

        migrationBuilder.DropColumn(name: "economic_identity_key", table: "costs");
        migrationBuilder.DropColumn(name: "service_occurrence_key", table: "costs");
        migrationBuilder.DropColumn(name: "applied_from_rating_id", table: "costs");
        migrationBuilder.DropColumn(name: "economic_identity_key", table: "revenues");
        migrationBuilder.DropColumn(name: "service_occurrence_key", table: "revenues");
        migrationBuilder.DropColumn(name: "applied_from_rating_id", table: "revenues");
    }
}
