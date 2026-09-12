using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint8Full_Settlement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "base_amount",
                table: "payments",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "fx_rate_id",
                table: "payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "base_amount",
                table: "payment_allocations",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "currency_code",
                table: "payment_allocations",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "VND");

            migrationBuilder.AddColumn<Guid>(
                name: "fx_rate_id",
                table: "payment_allocations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "base_amount",
                table: "collections",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "fx_rate_id",
                table: "collections",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "base_amount",
                table: "collection_allocations",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "currency_code",
                table: "collection_allocations",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "VND");

            migrationBuilder.AddColumn<Guid>(
                name: "fx_rate_id",
                table: "collection_allocations",
                type: "uuid",
                nullable: true);

            // Backfill allocation currency + payment/collection base_amount from txn currency (same-currency = amount).
            migrationBuilder.Sql("""
                UPDATE payment_allocations pa
                SET currency_code = p.currency_code
                FROM payments p
                WHERE pa.payment_id = p.id;

                UPDATE collection_allocations ca
                SET currency_code = c.currency_code
                FROM collections c
                WHERE ca.collection_id = c.id;

                UPDATE payments
                SET base_amount = amount
                WHERE base_amount IS NULL AND currency_code = 'VND';

                UPDATE collections
                SET base_amount = amount
                WHERE base_amount IS NULL AND currency_code = 'VND';

                UPDATE payment_allocations
                SET base_amount = amount
                WHERE base_amount IS NULL AND currency_code = 'VND';

                UPDATE collection_allocations
                SET base_amount = amount
                WHERE base_amount IS NULL AND currency_code = 'VND';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "base_amount",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "fx_rate_id",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "base_amount",
                table: "payment_allocations");

            migrationBuilder.DropColumn(
                name: "currency_code",
                table: "payment_allocations");

            migrationBuilder.DropColumn(
                name: "fx_rate_id",
                table: "payment_allocations");

            migrationBuilder.DropColumn(
                name: "base_amount",
                table: "collections");

            migrationBuilder.DropColumn(
                name: "fx_rate_id",
                table: "collections");

            migrationBuilder.DropColumn(
                name: "base_amount",
                table: "collection_allocations");

            migrationBuilder.DropColumn(
                name: "currency_code",
                table: "collection_allocations");

            migrationBuilder.DropColumn(
                name: "fx_rate_id",
                table: "collection_allocations");
        }
    }
}
