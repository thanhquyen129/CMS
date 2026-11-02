using LCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LCMS.Infrastructure.Persistence.Migrations;

[DbContext(typeof(LcmsDbContext))]
[Migration("20261102170000_SurchargeActivationPolicy")]
public class SurchargeActivationPolicy : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "activation_policy",
            table: "surcharge_versions",
            type: "character varying(64)",
            maxLength: 64,
            nullable: false,
            defaultValue: "UNCLASSIFIED_LEGACY");

        migrationBuilder.AddColumn<string>(
            name: "activation_config_json",
            table: "surcharge_versions",
            type: "jsonb",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "approval_ref",
            table: "surcharge_versions",
            type: "character varying(128)",
            maxLength: 128,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "approved_by_user_id",
            table: "surcharge_versions",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "approved_at",
            table: "surcharge_versions",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "ix_surcharge_versions_tenant_id_activation_policy",
            table: "surcharge_versions",
            columns: new[] { "tenant_id", "activation_policy" });

        migrationBuilder.AddColumn<string>(
            name: "application_bucket",
            table: "rating_details",
            type: "character varying(32)",
            maxLength: 32,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "activation_policy_snapshot",
            table: "rating_details",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "activation_evidence_ref",
            table: "rating_details",
            type: "character varying(256)",
            maxLength: 256,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "reason_code",
            table: "rating_details",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "reason_message_vi",
            table: "rating_details",
            type: "character varying(512)",
            maxLength: 512,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "calculation_basis",
            table: "rating_details",
            type: "character varying(32)",
            maxLength: 32,
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "applied_quantity",
            table: "rating_details",
            type: "numeric(18,4)",
            precision: 18,
            scale: 4,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "source_rule_id",
            table: "rating_details",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "ix_rating_details_tenant_id_application_bucket",
            table: "rating_details",
            columns: new[] { "tenant_id", "application_bucket" });

        migrationBuilder.CreateTable(
            name: "service_selections",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                context_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                context_id = table.Column<Guid>(type: "uuid", nullable: false),
                service_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                scope = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                uom = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                is_selected = table.Column<bool>(type: "boolean", nullable: false),
                source_ref = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                confirmed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                revision = table.Column<int>(type: "integer", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                created_by = table.Column<Guid>(type: "uuid", nullable: true),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                row_version = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true)
            },
            constraints: table => table.PrimaryKey("pk_service_selections", x => x.id));

        migrationBuilder.CreateIndex(
            name: "ix_service_selections_tenant_context_service",
            table: "service_selections",
            columns: new[] { "tenant_id", "context_type", "context_id", "service_code" },
            unique: true,
            filter: "deleted_at IS NULL");

        migrationBuilder.CreateTable(
            name: "service_selection_audits",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                service_selection_id = table.Column<Guid>(type: "uuid", nullable: false),
                context_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                context_id = table.Column<Guid>(type: "uuid", nullable: false),
                service_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                old_is_selected = table.Column<bool>(type: "boolean", nullable: false),
                new_is_selected = table.Column<bool>(type: "boolean", nullable: false),
                old_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                new_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                revision = table.Column<int>(type: "integer", nullable: false),
                actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                acted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                notes = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                created_by = table.Column<Guid>(type: "uuid", nullable: true),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                row_version = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true)
            },
            constraints: table => table.PrimaryKey("pk_service_selection_audits", x => x.id));

        migrationBuilder.CreateIndex(
            name: "ix_service_selection_audits_selection_revision",
            table: "service_selection_audits",
            columns: new[] { "tenant_id", "service_selection_id", "revision" });

        migrationBuilder.CreateIndex(
            name: "ix_service_selection_audits_context",
            table: "service_selection_audits",
            columns: new[] { "tenant_id", "context_type", "context_id", "service_code" });

        migrationBuilder.CreateTable(
            name: "surcharge_activation_migration_audits",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                surcharge_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                direction = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                old_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                new_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                mapping_policy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                approval_ref = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                approver = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                executed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                impact_count = table.Column<int>(type: "integer", nullable: false),
                notes = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                created_by = table.Column<Guid>(type: "uuid", nullable: true),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                row_version = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true)
            },
            constraints: table => table.PrimaryKey("pk_surcharge_activation_migration_audits", x => x.id));

        migrationBuilder.CreateIndex(
            name: "ix_surcharge_activation_migration_audits_lookup",
            table: "surcharge_activation_migration_audits",
            columns: new[] { "tenant_id", "surcharge_code", "direction", "executed_at" });

        migrationBuilder.Sql(
            """
            UPDATE surcharge_versions
            SET activation_policy = 'UNCLASSIFIED_LEGACY'
            WHERE activation_policy IS NULL OR btrim(activation_policy) = '';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "surcharge_activation_migration_audits");
        migrationBuilder.DropTable(name: "service_selection_audits");
        migrationBuilder.DropTable(name: "service_selections");
        migrationBuilder.DropIndex(name: "ix_rating_details_tenant_id_application_bucket", table: "rating_details");
        migrationBuilder.DropColumn(name: "application_bucket", table: "rating_details");
        migrationBuilder.DropColumn(name: "activation_policy_snapshot", table: "rating_details");
        migrationBuilder.DropColumn(name: "activation_evidence_ref", table: "rating_details");
        migrationBuilder.DropColumn(name: "reason_code", table: "rating_details");
        migrationBuilder.DropColumn(name: "reason_message_vi", table: "rating_details");
        migrationBuilder.DropColumn(name: "calculation_basis", table: "rating_details");
        migrationBuilder.DropColumn(name: "applied_quantity", table: "rating_details");
        migrationBuilder.DropColumn(name: "source_rule_id", table: "rating_details");
        migrationBuilder.DropIndex(name: "ix_surcharge_versions_tenant_id_activation_policy", table: "surcharge_versions");
        migrationBuilder.DropColumn(name: "activation_policy", table: "surcharge_versions");
        migrationBuilder.DropColumn(name: "activation_config_json", table: "surcharge_versions");
        migrationBuilder.DropColumn(name: "approval_ref", table: "surcharge_versions");
        migrationBuilder.DropColumn(name: "approved_by_user_id", table: "surcharge_versions");
        migrationBuilder.DropColumn(name: "approved_at", table: "surcharge_versions");
    }
}
