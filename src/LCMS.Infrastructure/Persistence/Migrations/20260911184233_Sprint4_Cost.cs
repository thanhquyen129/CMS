using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint4_Cost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_costs_tenant_id_bill_id_financial_maturity",
                table: "costs");

            migrationBuilder.AddColumn<decimal>(
                name: "actual_amount",
                table: "costs",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "actualized_at",
                table: "costs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "actualized_by",
                table: "costs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "approval_status",
                table: "costs",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "not_required");

            migrationBuilder.AddColumn<string>(
                name: "attribution_type",
                table: "costs",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "direct");

            migrationBuilder.AddColumn<decimal>(
                name: "base_amount",
                table: "costs",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "confirmed_amount",
                table: "costs",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "confirmed_at",
                table: "costs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "confirmed_by",
                table: "costs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "cost_category_id",
                table: "costs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cost_type_code",
                table: "costs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "cost_type_id",
                table: "costs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "effective_date",
                table: "costs",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<decimal>(
                name: "expected_amount",
                table: "costs",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            // Backfill Expected layer from legacy amount so maturity history stays honest.
            migrationBuilder.Sql("UPDATE costs SET expected_amount = amount WHERE expected_amount = 0;");

            migrationBuilder.AddColumn<Guid>(
                name: "fx_rate_id",
                table: "costs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "source_id",
                table: "costs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_type",
                table: "costs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "vendor_party_id",
                table: "costs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "cost_adjustments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cost_id = table.Column<Guid>(type: "uuid", nullable: false),
                    adjustment_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    delta_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    reason = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    effective_date = table.Column<DateOnly>(type: "date", nullable: false),
                    applied_to_maturity = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    amount_before = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    amount_after = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
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
                    table.PrimaryKey("pk_cost_adjustments", x => x.id);
                    table.ForeignKey(
                        name: "fk_cost_adjustments_costs_cost_id",
                        column: x => x.cost_id,
                        principalTable: "costs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cost_allocations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cost_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    allocation_basis = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    applicability_mode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    allocatable_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    allocated_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    allocation_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    rule_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rule_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    finalized_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    finalized_by = table.Column<Guid>(type: "uuid", nullable: true),
                    supersedes_allocation_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("pk_cost_allocations", x => x.id);
                    table.ForeignKey(
                        name: "fk_cost_allocations_costs_cost_id",
                        column: x => x.cost_id,
                        principalTable: "costs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cost_allocation_details",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    allocation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    basis_value = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    basis_ratio = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    allocated_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    rounding_adjustment = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    manual_override_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    override_reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
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
                    table.PrimaryKey("pk_cost_allocation_details", x => x.id);
                    table.ForeignKey(
                        name: "fk_cost_allocation_details_bills_bill_id",
                        column: x => x.bill_id,
                        principalTable: "bills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cost_allocation_details_cost_allocations_allocation_id",
                        column: x => x.allocation_id,
                        principalTable: "cost_allocations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_costs_bill_id",
                table: "costs",
                column: "bill_id");

            migrationBuilder.CreateIndex(
                name: "ix_costs_tenant_id_bill_id_financial_maturity_effective_date",
                table: "costs",
                columns: new[] { "tenant_id", "bill_id", "financial_maturity", "effective_date" });

            migrationBuilder.CreateIndex(
                name: "ix_costs_tenant_id_source_type_source_id",
                table: "costs",
                columns: new[] { "tenant_id", "source_type", "source_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_costs_tenant_id_vendor_party_id_effective_date",
                table: "costs",
                columns: new[] { "tenant_id", "vendor_party_id", "effective_date" });

            migrationBuilder.CreateIndex(
                name: "ix_cost_adjustments_cost_id",
                table: "cost_adjustments",
                column: "cost_id");

            migrationBuilder.CreateIndex(
                name: "ix_cost_adjustments_tenant_id_cost_id_created_at",
                table: "cost_adjustments",
                columns: new[] { "tenant_id", "cost_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_cost_allocation_details_allocation_id",
                table: "cost_allocation_details",
                column: "allocation_id");

            migrationBuilder.CreateIndex(
                name: "ix_cost_allocation_details_bill_id",
                table: "cost_allocation_details",
                column: "bill_id");

            migrationBuilder.CreateIndex(
                name: "ix_cost_allocation_details_tenant_id_allocation_id_bill_id",
                table: "cost_allocation_details",
                columns: new[] { "tenant_id", "allocation_id", "bill_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cost_allocations_cost_id",
                table: "cost_allocations",
                column: "cost_id");

            migrationBuilder.CreateIndex(
                name: "ix_cost_allocations_tenant_id_cost_id_version_no",
                table: "cost_allocations",
                columns: new[] { "tenant_id", "cost_id", "version_no" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_costs_bills_bill_id",
                table: "costs",
                column: "bill_id",
                principalTable: "bills",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_costs_bills_bill_id",
                table: "costs");

            migrationBuilder.DropTable(
                name: "cost_adjustments");

            migrationBuilder.DropTable(
                name: "cost_allocation_details");

            migrationBuilder.DropTable(
                name: "cost_allocations");

            migrationBuilder.DropIndex(
                name: "ix_costs_bill_id",
                table: "costs");

            migrationBuilder.DropIndex(
                name: "ix_costs_tenant_id_bill_id_financial_maturity_effective_date",
                table: "costs");

            migrationBuilder.DropIndex(
                name: "ix_costs_tenant_id_source_type_source_id",
                table: "costs");

            migrationBuilder.DropIndex(
                name: "ix_costs_tenant_id_vendor_party_id_effective_date",
                table: "costs");

            migrationBuilder.DropColumn(
                name: "actual_amount",
                table: "costs");

            migrationBuilder.DropColumn(
                name: "actualized_at",
                table: "costs");

            migrationBuilder.DropColumn(
                name: "actualized_by",
                table: "costs");

            migrationBuilder.DropColumn(
                name: "approval_status",
                table: "costs");

            migrationBuilder.DropColumn(
                name: "attribution_type",
                table: "costs");

            migrationBuilder.DropColumn(
                name: "base_amount",
                table: "costs");

            migrationBuilder.DropColumn(
                name: "confirmed_amount",
                table: "costs");

            migrationBuilder.DropColumn(
                name: "confirmed_at",
                table: "costs");

            migrationBuilder.DropColumn(
                name: "confirmed_by",
                table: "costs");

            migrationBuilder.DropColumn(
                name: "cost_category_id",
                table: "costs");

            migrationBuilder.DropColumn(
                name: "cost_type_code",
                table: "costs");

            migrationBuilder.DropColumn(
                name: "cost_type_id",
                table: "costs");

            migrationBuilder.DropColumn(
                name: "effective_date",
                table: "costs");

            migrationBuilder.DropColumn(
                name: "expected_amount",
                table: "costs");

            migrationBuilder.DropColumn(
                name: "fx_rate_id",
                table: "costs");

            migrationBuilder.DropColumn(
                name: "source_id",
                table: "costs");

            migrationBuilder.DropColumn(
                name: "source_type",
                table: "costs");

            migrationBuilder.DropColumn(
                name: "vendor_party_id",
                table: "costs");

            migrationBuilder.CreateIndex(
                name: "ix_costs_tenant_id_bill_id_financial_maturity",
                table: "costs",
                columns: new[] { "tenant_id", "bill_id", "financial_maturity" });
        }
    }
}
