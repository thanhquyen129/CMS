using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ArLedgerWriteOffReversal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "reverses_adjustment_id",
                table: "accounts_receivable_adjustments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "reverses_adjustment_id",
                table: "accounts_payable_adjustments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_accounts_receivable_adjustments_tenant_id_reverses_adjustme",
                table: "accounts_receivable_adjustments",
                columns: new[] { "tenant_id", "reverses_adjustment_id" },
                unique: true,
                filter: "reverses_adjustment_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_accounts_payable_adjustments_tenant_id_reverses_adjustment_",
                table: "accounts_payable_adjustments",
                columns: new[] { "tenant_id", "reverses_adjustment_id" },
                unique: true,
                filter: "reverses_adjustment_id IS NOT NULL");

            // Allocation.CurrencyCode was never stamped (defaulted to VND); Amount is in cash currency.
            migrationBuilder.Sql(
                """
                UPDATE collection_allocations a
                SET currency_code = c.currency_code
                FROM collections c
                WHERE a.collection_id = c.id AND a.tenant_id = c.tenant_id AND a.currency_code <> c.currency_code;

                UPDATE payment_allocations a
                SET currency_code = p.currency_code
                FROM payments p
                WHERE a.payment_id = p.id AND a.tenant_id = p.tenant_id AND a.currency_code <> p.currency_code;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_accounts_receivable_adjustments_tenant_id_reverses_adjustme",
                table: "accounts_receivable_adjustments");

            migrationBuilder.DropIndex(
                name: "ix_accounts_payable_adjustments_tenant_id_reverses_adjustment_",
                table: "accounts_payable_adjustments");

            migrationBuilder.DropColumn(
                name: "reverses_adjustment_id",
                table: "accounts_receivable_adjustments");

            migrationBuilder.DropColumn(
                name: "reverses_adjustment_id",
                table: "accounts_payable_adjustments");
        }
    }
}
