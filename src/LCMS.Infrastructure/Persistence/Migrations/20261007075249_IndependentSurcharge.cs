using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IndependentSurcharge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "formula_text",
                table: "rating_details",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "amount_original",
                table: "rating_details",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "line_fx_as_of",
                table: "rating_details",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "line_fx_rate",
                table: "rating_details",
                type: "numeric(18,8)",
                precision: 18,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "line_fx_source",
                table: "rating_details",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "original_currency",
                table: "rating_details",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "reporting_amount",
                table: "rating_details",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "source_id",
                table: "rating_details",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_type",
                table: "rating_details",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "base_rate");

            migrationBuilder.AddColumn<Guid>(
                name: "source_version_id",
                table: "rating_details",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "surcharge_migration_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pricing_rule_component_id = table.Column<Guid>(type: "uuid", nullable: false),
                    classification = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    surcharge_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("pk_surcharge_migration_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "surcharges",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    direction = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    source_legacy_component_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("pk_surcharges", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "surcharge_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    surcharge_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    publish_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    valid_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    valid_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("pk_surcharge_versions", x => x.id);
                    table.ForeignKey(
                        name: "fk_surcharge_versions_surcharges_surcharge_id",
                        column: x => x.surcharge_id,
                        principalTable: "surcharges",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "surcharge_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    surcharge_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    calculation_mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    basis = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    rate_amount_percent = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    min_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    max_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    container_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
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
                    table.PrimaryKey("pk_surcharge_rules", x => x.id);
                    table.ForeignKey(
                        name: "fk_surcharge_rules_surcharge_versions_surcharge_version_id",
                        column: x => x.surcharge_version_id,
                        principalTable: "surcharge_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "surcharge_breaks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    surcharge_rule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence_no = table.Column<int>(type: "integer", nullable: false),
                    min_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    max_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    unit_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
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
                    table.PrimaryKey("pk_surcharge_breaks", x => x.id);
                    table.ForeignKey(
                        name: "fk_surcharge_breaks_surcharge_rules_surcharge_rule_id",
                        column: x => x.surcharge_rule_id,
                        principalTable: "surcharge_rules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "surcharge_conditions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    surcharge_rule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dimension = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    @operator = table.Column<string>(name: "operator", type: "character varying(16)", maxLength: 16, nullable: false),
                    value_text = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    value_from = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    value_to = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
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
                    table.PrimaryKey("pk_surcharge_conditions", x => x.id);
                    table.ForeignKey(
                        name: "fk_surcharge_conditions_surcharge_rules_surcharge_rule_id",
                        column: x => x.surcharge_rule_id,
                        principalTable: "surcharge_rules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "surcharge_scopes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    surcharge_rule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rate_card_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rate_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    vendor_party_id = table.Column<Guid>(type: "uuid", nullable: true),
                    customer_party_id = table.Column<Guid>(type: "uuid", nullable: true),
                    service_type_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    route_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    transport_mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
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
                    table.PrimaryKey("pk_surcharge_scopes", x => x.id);
                    table.ForeignKey(
                        name: "fk_surcharge_scopes_surcharge_rules_surcharge_rule_id",
                        column: x => x.surcharge_rule_id,
                        principalTable: "surcharge_rules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_rating_details_tenant_id_source_type_source_version_id",
                table: "rating_details",
                columns: new[] { "tenant_id", "source_type", "source_version_id" });

            migrationBuilder.CreateIndex(
                name: "ix_surcharge_breaks_surcharge_rule_id",
                table: "surcharge_breaks",
                column: "surcharge_rule_id");

            migrationBuilder.CreateIndex(
                name: "ix_surcharge_breaks_tenant_id_surcharge_rule_id_sequence_no",
                table: "surcharge_breaks",
                columns: new[] { "tenant_id", "surcharge_rule_id", "sequence_no" },
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_surcharge_conditions_surcharge_rule_id",
                table: "surcharge_conditions",
                column: "surcharge_rule_id");

            migrationBuilder.CreateIndex(
                name: "ix_surcharge_conditions_tenant_id_surcharge_rule_id",
                table: "surcharge_conditions",
                columns: new[] { "tenant_id", "surcharge_rule_id" });

            migrationBuilder.CreateIndex(
                name: "ix_surcharge_migration_logs_tenant_id_pricing_rule_component_id",
                table: "surcharge_migration_logs",
                columns: new[] { "tenant_id", "pricing_rule_component_id" },
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_surcharge_rules_surcharge_version_id",
                table: "surcharge_rules",
                column: "surcharge_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_surcharge_rules_tenant_id_surcharge_version_id",
                table: "surcharge_rules",
                columns: new[] { "tenant_id", "surcharge_version_id" });

            migrationBuilder.CreateIndex(
                name: "ix_surcharge_scopes_surcharge_rule_id",
                table: "surcharge_scopes",
                column: "surcharge_rule_id");

            migrationBuilder.CreateIndex(
                name: "ix_surcharge_scopes_tenant_id_rate_card_id",
                table: "surcharge_scopes",
                columns: new[] { "tenant_id", "rate_card_id" });

            migrationBuilder.CreateIndex(
                name: "ix_surcharge_scopes_tenant_id_surcharge_rule_id",
                table: "surcharge_scopes",
                columns: new[] { "tenant_id", "surcharge_rule_id" });

            migrationBuilder.CreateIndex(
                name: "ix_surcharge_versions_surcharge_id",
                table: "surcharge_versions",
                column: "surcharge_id");

            migrationBuilder.CreateIndex(
                name: "ix_surcharge_versions_tenant_id_publish_status_valid_from",
                table: "surcharge_versions",
                columns: new[] { "tenant_id", "publish_status", "valid_from" });

            migrationBuilder.CreateIndex(
                name: "ix_surcharge_versions_tenant_id_surcharge_id_version_no",
                table: "surcharge_versions",
                columns: new[] { "tenant_id", "surcharge_id", "version_no" },
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_surcharges_tenant_id_code",
                table: "surcharges",
                columns: new[] { "tenant_id", "code" },
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_surcharges_tenant_id_source_legacy_component_id",
                table: "surcharges",
                columns: new[] { "tenant_id", "source_legacy_component_id" },
                unique: true,
                filter: "deleted_at IS NULL AND source_legacy_component_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "surcharge_breaks");

            migrationBuilder.DropTable(
                name: "surcharge_conditions");

            migrationBuilder.DropTable(
                name: "surcharge_migration_logs");

            migrationBuilder.DropTable(
                name: "surcharge_scopes");

            migrationBuilder.DropTable(
                name: "surcharge_rules");

            migrationBuilder.DropTable(
                name: "surcharge_versions");

            migrationBuilder.DropTable(
                name: "surcharges");

            migrationBuilder.DropIndex(
                name: "ix_rating_details_tenant_id_source_type_source_version_id",
                table: "rating_details");

            migrationBuilder.DropColumn(
                name: "amount_original",
                table: "rating_details");

            migrationBuilder.DropColumn(
                name: "line_fx_as_of",
                table: "rating_details");

            migrationBuilder.DropColumn(
                name: "line_fx_rate",
                table: "rating_details");

            migrationBuilder.DropColumn(
                name: "line_fx_source",
                table: "rating_details");

            migrationBuilder.DropColumn(
                name: "original_currency",
                table: "rating_details");

            migrationBuilder.DropColumn(
                name: "reporting_amount",
                table: "rating_details");

            migrationBuilder.DropColumn(
                name: "source_id",
                table: "rating_details");

            migrationBuilder.DropColumn(
                name: "source_type",
                table: "rating_details");

            migrationBuilder.DropColumn(
                name: "source_version_id",
                table: "rating_details");

            migrationBuilder.AlterColumn<string>(
                name: "formula_text",
                table: "rating_details",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(512)",
                oldMaxLength: 512,
                oldNullable: true);
        }
    }
}
