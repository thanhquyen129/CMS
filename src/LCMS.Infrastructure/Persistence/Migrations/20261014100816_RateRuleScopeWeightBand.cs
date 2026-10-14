using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RateRuleScopeWeightBand : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "applies_any_route",
                table: "pricing_rules",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "carrier_name",
                table: "pricing_rules",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "carrier_party_id",
                table: "pricing_rules",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "matrix_group_code",
                table: "pricing_rules",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "route_id",
                table: "pricing_rules",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "weight_basis",
                table: "pricing_rules",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "weight_max",
                table: "pricing_rules",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "weight_min",
                table: "pricing_rules",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "rate_scope_migration_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rate_card_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pricing_rule_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    detail = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    row_version = table.Column<byte[]>(type: "bytea", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rate_scope_migration_logs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pricing_rules_route_id",
                table: "pricing_rules",
                column: "route_id");

            migrationBuilder.CreateIndex(
                name: "ix_rate_scope_migration_logs_tenant_id_pricing_rule_id_action",
                table: "rate_scope_migration_logs",
                columns: new[] { "tenant_id", "pricing_rule_id", "action" });

            migrationBuilder.Sql(
                """
                INSERT INTO rate_scope_migration_logs
                    (id, tenant_id, rate_card_id, pricing_rule_id, action, detail, row_version, created_at)
                SELECT gen_random_uuid(), c.tenant_id, c.id, r.id, 'copy_header_route',
                       'Copied header route onto the rule. Rating snapshots were not changed.',
                       gen_random_bytes(16), now()
                FROM pricing_rules r
                JOIN rate_versions v ON v.id = r.rate_version_id
                JOIN rate_cards c ON c.id = v.rate_card_id
                WHERE r.deleted_at IS NULL
                  AND c.deleted_at IS NULL
                  AND r.route_id IS NULL
                  AND (r.route_code IS NULL OR btrim(r.route_code) = '')
                  AND c.route_id IS NOT NULL;

                UPDATE pricing_rules AS r
                SET route_id = c.route_id,
                    route_code = COALESCE(NULLIF(btrim(r.route_code), ''), c.route_code),
                    applies_any_route = FALSE
                FROM rate_versions v
                JOIN rate_cards c ON c.id = v.rate_card_id
                WHERE r.rate_version_id = v.id
                  AND r.deleted_at IS NULL
                  AND c.deleted_at IS NULL
                  AND r.route_id IS NULL
                  AND (r.route_code IS NULL OR btrim(r.route_code) = '')
                  AND c.route_id IS NOT NULL;

                INSERT INTO rate_scope_migration_logs
                    (id, tenant_id, rate_card_id, pricing_rule_id, action, detail, row_version, created_at)
                SELECT gen_random_uuid(), c.tenant_id, c.id, r.id, 'copy_header_carrier',
                       'Copied header carrier name onto the rule. Vendor was not changed.',
                       gen_random_bytes(16), now()
                FROM pricing_rules r
                JOIN rate_versions v ON v.id = r.rate_version_id
                JOIN rate_cards c ON c.id = v.rate_card_id
                WHERE r.deleted_at IS NULL
                  AND c.deleted_at IS NULL
                  AND (r.carrier_name IS NULL OR btrim(r.carrier_name) = '')
                  AND c.carrier_name IS NOT NULL
                  AND btrim(c.carrier_name) <> '';

                UPDATE pricing_rules AS r
                SET carrier_name = c.carrier_name
                FROM rate_versions v
                JOIN rate_cards c ON c.id = v.rate_card_id
                WHERE r.rate_version_id = v.id
                  AND r.deleted_at IS NULL
                  AND (r.carrier_name IS NULL OR btrim(r.carrier_name) = '')
                  AND c.carrier_name IS NOT NULL
                  AND btrim(c.carrier_name) <> '';

                INSERT INTO rate_scope_migration_logs
                    (id, tenant_id, rate_card_id, pricing_rule_id, action, detail, row_version, created_at)
                SELECT gen_random_uuid(), c.tenant_id, c.id, r.id, 'mark_any_route',
                       'Legacy empty route kept as explicit every-route. Meaning was not deleted.',
                       gen_random_bytes(16), now()
                FROM pricing_rules r
                JOIN rate_versions v ON v.id = r.rate_version_id
                JOIN rate_cards c ON c.id = v.rate_card_id
                WHERE r.deleted_at IS NULL
                  AND r.route_id IS NULL
                  AND (r.route_code IS NULL OR btrim(r.route_code) = '')
                  AND r.applies_any_route IS NULL;

                UPDATE pricing_rules
                SET applies_any_route = TRUE
                WHERE deleted_at IS NULL
                  AND route_id IS NULL
                  AND (route_code IS NULL OR btrim(route_code) = '')
                  AND applies_any_route IS NULL;

                UPDATE pricing_rules
                SET applies_any_route = FALSE
                WHERE deleted_at IS NULL
                  AND applies_any_route IS NULL
                  AND (route_id IS NOT NULL OR (route_code IS NOT NULL AND btrim(route_code) <> ''));

                INSERT INTO rate_scope_migration_logs
                    (id, tenant_id, rate_card_id, pricing_rule_id, action, detail, row_version, created_at)
                SELECT gen_random_uuid(), c.tenant_id, c.id, r.id, 'bind_route_code',
                       'Bound an existing rule route code to the canonical route id.',
                       gen_random_bytes(16), now()
                FROM pricing_rules r
                JOIN rate_versions v ON v.id = r.rate_version_id
                JOIN rate_cards c ON c.id = v.rate_card_id
                JOIN routes rt ON rt.tenant_id = c.tenant_id
                    AND rt.deleted_at IS NULL
                    AND upper(rt.code) = upper(r.route_code)
                WHERE r.deleted_at IS NULL
                  AND r.route_id IS NULL
                  AND r.route_code IS NOT NULL
                  AND btrim(r.route_code) <> '';

                UPDATE pricing_rules AS r
                SET route_id = rt.id
                FROM rate_versions v
                JOIN rate_cards c ON c.id = v.rate_card_id
                JOIN routes rt ON rt.tenant_id = c.tenant_id
                    AND rt.deleted_at IS NULL
                    AND upper(rt.code) = upper(r.route_code)
                WHERE r.rate_version_id = v.id
                  AND r.deleted_at IS NULL
                  AND r.route_id IS NULL
                  AND r.route_code IS NOT NULL
                  AND btrim(r.route_code) <> '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rate_scope_migration_logs");

            migrationBuilder.DropIndex(
                name: "ix_pricing_rules_route_id",
                table: "pricing_rules");

            migrationBuilder.DropColumn(
                name: "applies_any_route",
                table: "pricing_rules");

            migrationBuilder.DropColumn(
                name: "carrier_name",
                table: "pricing_rules");

            migrationBuilder.DropColumn(
                name: "carrier_party_id",
                table: "pricing_rules");

            migrationBuilder.DropColumn(
                name: "matrix_group_code",
                table: "pricing_rules");

            migrationBuilder.DropColumn(
                name: "route_id",
                table: "pricing_rules");

            migrationBuilder.DropColumn(
                name: "weight_basis",
                table: "pricing_rules");

            migrationBuilder.DropColumn(
                name: "weight_max",
                table: "pricing_rules");

            migrationBuilder.DropColumn(
                name: "weight_min",
                table: "pricing_rules");
        }
    }
}
