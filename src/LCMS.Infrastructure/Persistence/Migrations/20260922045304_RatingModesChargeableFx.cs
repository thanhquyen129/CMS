using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RatingModesChargeableFx : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "chargeable_basis",
                table: "ratings",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "chargeable_weight_kg",
                table: "ratings",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "context_json",
                table: "ratings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "fx_as_of",
                table: "ratings",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "fx_rate",
                table: "ratings",
                type: "numeric(18,8)",
                precision: 18,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fx_source",
                table: "ratings",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "original_amount",
                table: "ratings",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "original_currency",
                table: "ratings",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "rate_date",
                table: "ratings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "rounded_amount",
                table: "ratings",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "formula_text",
                table: "rating_details",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "carrier_name",
                table: "rate_cards",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "route_code",
                table: "rate_cards",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "transport_mode",
                table: "rate_cards",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "charge_code",
                table: "pricing_rules",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "commodity_code",
                table: "pricing_rules",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "destination_code",
                table: "pricing_rules",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "origin_code",
                table: "pricing_rules",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "rounding_step",
                table: "pricing_rules",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "transport_mode",
                table: "pricing_rules",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "volumetric_factor",
                table: "pricing_rules",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "calc_method",
                table: "pricing_rule_components",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "depends_on_code",
                table: "pricing_rule_components",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "container_rates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pricing_rule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    container_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
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
                    table.PrimaryKey("pk_container_rates", x => x.id);
                    table.ForeignKey(
                        name: "fk_container_rates_pricing_rules_pricing_rule_id",
                        column: x => x.pricing_rule_id,
                        principalTable: "pricing_rules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rate_breaks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pricing_rule_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("pk_rate_breaks", x => x.id);
                    table.ForeignKey(
                        name: "fk_rate_breaks_pricing_rules_pricing_rule_id",
                        column: x => x.pricing_rule_id,
                        principalTable: "pricing_rules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_container_rates_pricing_rule_id",
                table: "container_rates",
                column: "pricing_rule_id");

            migrationBuilder.CreateIndex(
                name: "ix_container_rates_tenant_id_pricing_rule_id_container_type",
                table: "container_rates",
                columns: new[] { "tenant_id", "pricing_rule_id", "container_type" },
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_rate_breaks_pricing_rule_id",
                table: "rate_breaks",
                column: "pricing_rule_id");

            migrationBuilder.CreateIndex(
                name: "ix_rate_breaks_tenant_id_pricing_rule_id_sequence_no",
                table: "rate_breaks",
                columns: new[] { "tenant_id", "pricing_rule_id", "sequence_no" },
                unique: true,
                filter: "deleted_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "container_rates");

            migrationBuilder.DropTable(
                name: "rate_breaks");

            migrationBuilder.DropColumn(
                name: "chargeable_basis",
                table: "ratings");

            migrationBuilder.DropColumn(
                name: "chargeable_weight_kg",
                table: "ratings");

            migrationBuilder.DropColumn(
                name: "context_json",
                table: "ratings");

            migrationBuilder.DropColumn(
                name: "fx_as_of",
                table: "ratings");

            migrationBuilder.DropColumn(
                name: "fx_rate",
                table: "ratings");

            migrationBuilder.DropColumn(
                name: "fx_source",
                table: "ratings");

            migrationBuilder.DropColumn(
                name: "original_amount",
                table: "ratings");

            migrationBuilder.DropColumn(
                name: "original_currency",
                table: "ratings");

            migrationBuilder.DropColumn(
                name: "rate_date",
                table: "ratings");

            migrationBuilder.DropColumn(
                name: "rounded_amount",
                table: "ratings");

            migrationBuilder.DropColumn(
                name: "formula_text",
                table: "rating_details");

            migrationBuilder.DropColumn(
                name: "carrier_name",
                table: "rate_cards");

            migrationBuilder.DropColumn(
                name: "route_code",
                table: "rate_cards");

            migrationBuilder.DropColumn(
                name: "transport_mode",
                table: "rate_cards");

            migrationBuilder.DropColumn(
                name: "charge_code",
                table: "pricing_rules");

            migrationBuilder.DropColumn(
                name: "commodity_code",
                table: "pricing_rules");

            migrationBuilder.DropColumn(
                name: "destination_code",
                table: "pricing_rules");

            migrationBuilder.DropColumn(
                name: "origin_code",
                table: "pricing_rules");

            migrationBuilder.DropColumn(
                name: "rounding_step",
                table: "pricing_rules");

            migrationBuilder.DropColumn(
                name: "transport_mode",
                table: "pricing_rules");

            migrationBuilder.DropColumn(
                name: "volumetric_factor",
                table: "pricing_rules");

            migrationBuilder.DropColumn(
                name: "calc_method",
                table: "pricing_rule_components");

            migrationBuilder.DropColumn(
                name: "depends_on_code",
                table: "pricing_rule_components");
        }
    }
}
