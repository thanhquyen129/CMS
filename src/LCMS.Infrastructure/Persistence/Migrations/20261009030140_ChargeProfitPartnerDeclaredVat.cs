using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ChargeProfitPartnerDeclaredVat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "vat_rate",
                table: "surcharge_versions",
                type: "numeric(9,4)",
                precision: 9,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "customer_group_code",
                table: "surcharge_scopes",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "economic_charge_type_id",
                table: "revenues",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "gross_amount",
                table: "revenues",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "net_amount",
                table: "revenues",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "partner_override_requires_rerate",
                table: "revenues",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "partner_suggested_id",
                table: "revenues",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "vat_amount",
                table: "revenues",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "vat_rate",
                table: "revenues",
                type: "numeric(9,4)",
                precision: 9,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "economic_charge_type_id",
                table: "rating_details",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "gross_amount",
                table: "rating_details",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "net_amount",
                table: "rating_details",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "partner_suggested_id",
                table: "rating_details",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "vat_amount",
                table: "rating_details",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "vat_rate",
                table: "rating_details",
                type: "numeric(9,4)",
                precision: 9,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "vat_rate",
                table: "rate_versions",
                type: "numeric(9,4)",
                precision: 9,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "customer_group_code",
                table: "rate_cards",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "customer_party_id",
                table: "rate_cards",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "supplier_party_id",
                table: "rate_cards",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "economic_charge_type_id",
                table: "financial_document_lines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "gross_amount",
                table: "financial_document_lines",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "net_amount",
                table: "financial_document_lines",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "rating_detail_id",
                table: "financial_document_lines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "vat_amount",
                table: "financial_document_lines",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "vat_rate",
                table: "financial_document_lines",
                type: "numeric(9,4)",
                precision: 9,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "vat_variance_amount",
                table: "financial_document_lines",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "economic_charge_type_id",
                table: "costs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "gross_amount",
                table: "costs",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "net_amount",
                table: "costs",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "partner_override_requires_rerate",
                table: "costs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "partner_suggested_id",
                table: "costs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "vat_amount",
                table: "costs",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "vat_rate",
                table: "costs",
                type: "numeric(9,4)",
                precision: 9,
                scale: 4,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "economic_charge_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("pk_economic_charge_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "charge_type_mappings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    source_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    economic_charge_type_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("pk_charge_type_mappings", x => x.id);
                    table.ForeignKey(
                        name: "fk_charge_type_mappings_economic_charge_types_economic_charge_",
                        column: x => x.economic_charge_type_id,
                        principalTable: "economic_charge_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_costs_tenant_id_economic_charge_type_id",
                table: "costs",
                columns: new[] { "tenant_id", "economic_charge_type_id" });

            migrationBuilder.CreateIndex(
                name: "ix_charge_type_mappings_economic_charge_type_id",
                table: "charge_type_mappings",
                column: "economic_charge_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_charge_type_mappings_tenant_id_source_kind_source_code",
                table: "charge_type_mappings",
                columns: new[] { "tenant_id", "source_kind", "source_code" },
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_economic_charge_types_tenant_id_code",
                table: "economic_charge_types",
                columns: new[] { "tenant_id", "code" },
                unique: true,
                filter: "deleted_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "charge_type_mappings");

            migrationBuilder.DropTable(
                name: "economic_charge_types");

            migrationBuilder.DropIndex(
                name: "ix_costs_tenant_id_economic_charge_type_id",
                table: "costs");

            migrationBuilder.DropColumn(
                name: "vat_rate",
                table: "surcharge_versions");

            migrationBuilder.DropColumn(
                name: "customer_group_code",
                table: "surcharge_scopes");

            migrationBuilder.DropColumn(
                name: "economic_charge_type_id",
                table: "revenues");

            migrationBuilder.DropColumn(
                name: "gross_amount",
                table: "revenues");

            migrationBuilder.DropColumn(
                name: "net_amount",
                table: "revenues");

            migrationBuilder.DropColumn(
                name: "partner_override_requires_rerate",
                table: "revenues");

            migrationBuilder.DropColumn(
                name: "partner_suggested_id",
                table: "revenues");

            migrationBuilder.DropColumn(
                name: "vat_amount",
                table: "revenues");

            migrationBuilder.DropColumn(
                name: "vat_rate",
                table: "revenues");

            migrationBuilder.DropColumn(
                name: "economic_charge_type_id",
                table: "rating_details");

            migrationBuilder.DropColumn(
                name: "gross_amount",
                table: "rating_details");

            migrationBuilder.DropColumn(
                name: "net_amount",
                table: "rating_details");

            migrationBuilder.DropColumn(
                name: "partner_suggested_id",
                table: "rating_details");

            migrationBuilder.DropColumn(
                name: "vat_amount",
                table: "rating_details");

            migrationBuilder.DropColumn(
                name: "vat_rate",
                table: "rating_details");

            migrationBuilder.DropColumn(
                name: "vat_rate",
                table: "rate_versions");

            migrationBuilder.DropColumn(
                name: "customer_group_code",
                table: "rate_cards");

            migrationBuilder.DropColumn(
                name: "customer_party_id",
                table: "rate_cards");

            migrationBuilder.DropColumn(
                name: "supplier_party_id",
                table: "rate_cards");

            migrationBuilder.DropColumn(
                name: "economic_charge_type_id",
                table: "financial_document_lines");

            migrationBuilder.DropColumn(
                name: "gross_amount",
                table: "financial_document_lines");

            migrationBuilder.DropColumn(
                name: "net_amount",
                table: "financial_document_lines");

            migrationBuilder.DropColumn(
                name: "rating_detail_id",
                table: "financial_document_lines");

            migrationBuilder.DropColumn(
                name: "vat_amount",
                table: "financial_document_lines");

            migrationBuilder.DropColumn(
                name: "vat_rate",
                table: "financial_document_lines");

            migrationBuilder.DropColumn(
                name: "vat_variance_amount",
                table: "financial_document_lines");

            migrationBuilder.DropColumn(
                name: "economic_charge_type_id",
                table: "costs");

            migrationBuilder.DropColumn(
                name: "gross_amount",
                table: "costs");

            migrationBuilder.DropColumn(
                name: "net_amount",
                table: "costs");

            migrationBuilder.DropColumn(
                name: "partner_override_requires_rerate",
                table: "costs");

            migrationBuilder.DropColumn(
                name: "partner_suggested_id",
                table: "costs");

            migrationBuilder.DropColumn(
                name: "vat_amount",
                table: "costs");

            migrationBuilder.DropColumn(
                name: "vat_rate",
                table: "costs");
        }
    }
}
