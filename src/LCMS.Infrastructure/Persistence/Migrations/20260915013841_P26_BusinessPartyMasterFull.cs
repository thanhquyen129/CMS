using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P26_BusinessPartyMasterFull : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "address_line1",
                table: "business_parties",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "address_line2",
                table: "business_parties",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "city",
                table: "business_parties",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "country_code",
                table: "business_parties",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "credit_limit",
                table: "business_parties",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "credit_limit_currency_code",
                table: "business_parties",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "default_currency_code",
                table: "business_parties",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "district",
                table: "business_parties",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "email",
                table: "business_parties",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "legal_name",
                table: "business_parties",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "notes",
                table: "business_parties",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "payment_term_days",
                table: "business_parties",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "phone",
                table: "business_parties",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "postal_code",
                table: "business_parties",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "province",
                table: "business_parties",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tax_id",
                table: "business_parties",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ward",
                table: "business_parties",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "website",
                table: "business_parties",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "party_bank_accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    party_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bank_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    bank_branch = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    account_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    account_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    note = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
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
                    table.PrimaryKey("pk_party_bank_accounts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "party_contacts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    party_id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    title = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    phone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    note = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
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
                    table.PrimaryKey("pk_party_contacts", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_business_parties_tenant_id_is_active",
                table: "business_parties",
                columns: new[] { "tenant_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_business_parties_tenant_id_tax_id",
                table: "business_parties",
                columns: new[] { "tenant_id", "tax_id" },
                unique: true,
                filter: "tax_id IS NOT NULL AND deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_party_bank_accounts_tenant_id_party_id",
                table: "party_bank_accounts",
                columns: new[] { "tenant_id", "party_id" });

            migrationBuilder.CreateIndex(
                name: "ix_party_bank_accounts_tenant_id_party_id_account_number",
                table: "party_bank_accounts",
                columns: new[] { "tenant_id", "party_id", "account_number" },
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_party_contacts_tenant_id_party_id",
                table: "party_contacts",
                columns: new[] { "tenant_id", "party_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "party_bank_accounts");

            migrationBuilder.DropTable(
                name: "party_contacts");

            migrationBuilder.DropIndex(
                name: "ix_business_parties_tenant_id_is_active",
                table: "business_parties");

            migrationBuilder.DropIndex(
                name: "ix_business_parties_tenant_id_tax_id",
                table: "business_parties");

            migrationBuilder.DropColumn(
                name: "address_line1",
                table: "business_parties");

            migrationBuilder.DropColumn(
                name: "address_line2",
                table: "business_parties");

            migrationBuilder.DropColumn(
                name: "city",
                table: "business_parties");

            migrationBuilder.DropColumn(
                name: "country_code",
                table: "business_parties");

            migrationBuilder.DropColumn(
                name: "credit_limit",
                table: "business_parties");

            migrationBuilder.DropColumn(
                name: "credit_limit_currency_code",
                table: "business_parties");

            migrationBuilder.DropColumn(
                name: "default_currency_code",
                table: "business_parties");

            migrationBuilder.DropColumn(
                name: "district",
                table: "business_parties");

            migrationBuilder.DropColumn(
                name: "email",
                table: "business_parties");

            migrationBuilder.DropColumn(
                name: "legal_name",
                table: "business_parties");

            migrationBuilder.DropColumn(
                name: "notes",
                table: "business_parties");

            migrationBuilder.DropColumn(
                name: "payment_term_days",
                table: "business_parties");

            migrationBuilder.DropColumn(
                name: "phone",
                table: "business_parties");

            migrationBuilder.DropColumn(
                name: "postal_code",
                table: "business_parties");

            migrationBuilder.DropColumn(
                name: "province",
                table: "business_parties");

            migrationBuilder.DropColumn(
                name: "tax_id",
                table: "business_parties");

            migrationBuilder.DropColumn(
                name: "ward",
                table: "business_parties");

            migrationBuilder.DropColumn(
                name: "website",
                table: "business_parties");
        }
    }
}
