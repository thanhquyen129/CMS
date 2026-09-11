using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint5_Revenue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_revenues_tenant_id_bill_id_financial_maturity",
                table: "revenues");

            migrationBuilder.AddColumn<decimal>(
                name: "actual_amount",
                table: "revenues",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "actualized_at",
                table: "revenues",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "actualized_by",
                table: "revenues",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "approval_status",
                table: "revenues",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "not_required");

            migrationBuilder.AddColumn<decimal>(
                name: "base_amount",
                table: "revenues",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "confirmed_amount",
                table: "revenues",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "confirmed_at",
                table: "revenues",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "confirmed_by",
                table: "revenues",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "customer_party_id",
                table: "revenues",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "effective_date",
                table: "revenues",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<decimal>(
                name: "expected_amount",
                table: "revenues",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "fx_rate_id",
                table: "revenues",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "recognition_policy_version",
                table: "revenues",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "revenue_type_code",
                table: "revenues",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "revenue_type_id",
                table: "revenues",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "source_id",
                table: "revenues",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_type",
                table: "revenues",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            // Backfill Expected layer from legacy stub amount (C-009 layers).
            migrationBuilder.Sql(
                """
                UPDATE revenues
                SET expected_amount = amount,
                    approval_status = CASE WHEN approval_status = '' OR approval_status IS NULL THEN 'not_required' ELSE approval_status END,
                    effective_date = CASE WHEN effective_date = DATE '0001-01-01' THEN (created_at AT TIME ZONE 'UTC')::date ELSE effective_date END
                WHERE expected_amount = 0 AND amount <> 0
                   OR approval_status = ''
                   OR effective_date = DATE '0001-01-01';
                """);

            migrationBuilder.CreateTable(
                name: "revenue_adjustments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    revenue_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("pk_revenue_adjustments", x => x.id);
                    table.ForeignKey(
                        name: "fk_revenue_adjustments_revenues_revenue_id",
                        column: x => x.revenue_id,
                        principalTable: "revenues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_revenues_bill_id",
                table: "revenues",
                column: "bill_id");

            migrationBuilder.CreateIndex(
                name: "ix_revenues_tenant_id_bill_id_financial_maturity_effective_date",
                table: "revenues",
                columns: new[] { "tenant_id", "bill_id", "financial_maturity", "effective_date" });

            migrationBuilder.CreateIndex(
                name: "ix_revenues_tenant_id_source_type_source_id",
                table: "revenues",
                columns: new[] { "tenant_id", "source_type", "source_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_revenue_adjustments_revenue_id",
                table: "revenue_adjustments",
                column: "revenue_id");

            migrationBuilder.CreateIndex(
                name: "ix_revenue_adjustments_tenant_id_revenue_id_created_at",
                table: "revenue_adjustments",
                columns: new[] { "tenant_id", "revenue_id", "created_at" });

            migrationBuilder.AddForeignKey(
                name: "fk_revenues_bills_bill_id",
                table: "revenues",
                column: "bill_id",
                principalTable: "bills",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_revenues_bills_bill_id",
                table: "revenues");

            migrationBuilder.DropTable(
                name: "revenue_adjustments");

            migrationBuilder.DropIndex(
                name: "ix_revenues_bill_id",
                table: "revenues");

            migrationBuilder.DropIndex(
                name: "ix_revenues_tenant_id_bill_id_financial_maturity_effective_date",
                table: "revenues");

            migrationBuilder.DropIndex(
                name: "ix_revenues_tenant_id_source_type_source_id",
                table: "revenues");

            migrationBuilder.DropColumn(
                name: "actual_amount",
                table: "revenues");

            migrationBuilder.DropColumn(
                name: "actualized_at",
                table: "revenues");

            migrationBuilder.DropColumn(
                name: "actualized_by",
                table: "revenues");

            migrationBuilder.DropColumn(
                name: "approval_status",
                table: "revenues");

            migrationBuilder.DropColumn(
                name: "base_amount",
                table: "revenues");

            migrationBuilder.DropColumn(
                name: "confirmed_amount",
                table: "revenues");

            migrationBuilder.DropColumn(
                name: "confirmed_at",
                table: "revenues");

            migrationBuilder.DropColumn(
                name: "confirmed_by",
                table: "revenues");

            migrationBuilder.DropColumn(
                name: "customer_party_id",
                table: "revenues");

            migrationBuilder.DropColumn(
                name: "effective_date",
                table: "revenues");

            migrationBuilder.DropColumn(
                name: "expected_amount",
                table: "revenues");

            migrationBuilder.DropColumn(
                name: "fx_rate_id",
                table: "revenues");

            migrationBuilder.DropColumn(
                name: "recognition_policy_version",
                table: "revenues");

            migrationBuilder.DropColumn(
                name: "revenue_type_code",
                table: "revenues");

            migrationBuilder.DropColumn(
                name: "revenue_type_id",
                table: "revenues");

            migrationBuilder.DropColumn(
                name: "source_id",
                table: "revenues");

            migrationBuilder.DropColumn(
                name: "source_type",
                table: "revenues");

            migrationBuilder.CreateIndex(
                name: "ix_revenues_tenant_id_bill_id_financial_maturity",
                table: "revenues",
                columns: new[] { "tenant_id", "bill_id", "financial_maturity" });
        }
    }
}
