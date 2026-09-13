using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P10_P12_ApArAdjustmentsAndMatrix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "accounts_payable_adjustments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    accounts_payable_id = table.Column<Guid>(type: "uuid", nullable: false),
                    adjustment_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    delta_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    reason = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    effective_date = table.Column<DateOnly>(type: "date", nullable: false),
                    adjustment_amount_before = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    adjustment_amount_after = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    outstanding_before = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    outstanding_after = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
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
                    table.PrimaryKey("pk_accounts_payable_adjustments", x => x.id);
                    table.ForeignKey(
                        name: "fk_accounts_payable_adjustments_accounts_payable_accounts_paya",
                        column: x => x.accounts_payable_id,
                        principalTable: "accounts_payable",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "accounts_receivable_adjustments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    accounts_receivable_id = table.Column<Guid>(type: "uuid", nullable: false),
                    adjustment_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    delta_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    reason = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    effective_date = table.Column<DateOnly>(type: "date", nullable: false),
                    adjustment_amount_before = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    adjustment_amount_after = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    outstanding_before = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    outstanding_after = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
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
                    table.PrimaryKey("pk_accounts_receivable_adjustments", x => x.id);
                    table.ForeignKey(
                        name: "fk_accounts_receivable_adjustments_accounts_receivable_account",
                        column: x => x.accounts_receivable_id,
                        principalTable: "accounts_receivable",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_accounts_payable_adjustments_accounts_payable_id",
                table: "accounts_payable_adjustments",
                column: "accounts_payable_id");

            migrationBuilder.CreateIndex(
                name: "ix_accounts_payable_adjustments_tenant_id_accounts_payable_id_",
                table: "accounts_payable_adjustments",
                columns: new[] { "tenant_id", "accounts_payable_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_accounts_receivable_adjustments_accounts_receivable_id",
                table: "accounts_receivable_adjustments",
                column: "accounts_receivable_id");

            migrationBuilder.CreateIndex(
                name: "ix_accounts_receivable_adjustments_tenant_id_accounts_receivab",
                table: "accounts_receivable_adjustments",
                columns: new[] { "tenant_id", "accounts_receivable_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "accounts_payable_adjustments");

            migrationBuilder.DropTable(
                name: "accounts_receivable_adjustments");
        }
    }
}
