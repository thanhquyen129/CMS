using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint1Full_IdentityMasterData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "organization_id",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "organization_id",
                table: "costs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "organization_id",
                table: "bills",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "party_roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    party_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("pk_party_roles", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_users_tenant_id_organization_id",
                table: "users",
                columns: new[] { "tenant_id", "organization_id" });

            migrationBuilder.CreateIndex(
                name: "ix_costs_tenant_id_organization_id",
                table: "costs",
                columns: new[] { "tenant_id", "organization_id" });

            migrationBuilder.CreateIndex(
                name: "ix_bills_tenant_id_organization_id",
                table: "bills",
                columns: new[] { "tenant_id", "organization_id" });

            migrationBuilder.CreateIndex(
                name: "ix_party_roles_tenant_id_party_id_role_code",
                table: "party_roles",
                columns: new[] { "tenant_id", "party_id", "role_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_party_roles_tenant_id_role_code",
                table: "party_roles",
                columns: new[] { "tenant_id", "role_code" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "party_roles");

            migrationBuilder.DropIndex(
                name: "ix_users_tenant_id_organization_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_costs_tenant_id_organization_id",
                table: "costs");

            migrationBuilder.DropIndex(
                name: "ix_bills_tenant_id_organization_id",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "organization_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "organization_id",
                table: "costs");

            migrationBuilder.DropColumn(
                name: "organization_id",
                table: "bills");
        }
    }
}
