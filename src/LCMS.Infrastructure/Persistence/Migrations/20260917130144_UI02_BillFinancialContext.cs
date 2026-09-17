using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UI02_BillFinancialContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "assigned_user_id",
                table: "bills",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "customer_party_id",
                table: "bills",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "bills",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "eta_at",
                table: "bills",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "etd_at",
                table: "bills",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "internal_note",
                table: "bills",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "route_code",
                table: "bills",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_bills_tenant_id_customer_party_id",
                table: "bills",
                columns: new[] { "tenant_id", "customer_party_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_bills_tenant_id_customer_party_id",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "assigned_user_id",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "customer_party_id",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "description",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "eta_at",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "etd_at",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "internal_note",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "route_code",
                table: "bills");
        }
    }
}
