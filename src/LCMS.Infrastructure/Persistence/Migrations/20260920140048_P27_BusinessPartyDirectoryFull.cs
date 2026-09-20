using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P27_BusinessPartyDirectoryFull : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "function_code",
                table: "party_contacts",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "general");

            migrationBuilder.AddColumn<string>(
                name: "bank_code",
                table: "party_bank_accounts",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "swift_bic",
                table: "party_bank_accounts",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "assigned_user_id",
                table: "business_parties",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "blocked_at",
                table: "business_parties",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "blocked_reason",
                table: "business_parties",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "credit_control_mode",
                table: "business_parties",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "advisory");

            migrationBuilder.AddColumn<string>(
                name: "external_code",
                table: "business_parties",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "group_code",
                table: "business_parties",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "industry_code",
                table: "business_parties",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "invoice_email",
                table: "business_parties",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_blocked",
                table: "business_parties",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "legal_type",
                table: "business_parties",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "parent_party_id",
                table: "business_parties",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "party_kind",
                table: "business_parties",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "organization");

            migrationBuilder.AddColumn<string>(
                name: "short_name",
                table: "business_parties",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "vat_registered",
                table: "business_parties",
                type: "boolean",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_business_parties_tenant_id_external_code",
                table: "business_parties",
                columns: new[] { "tenant_id", "external_code" },
                unique: true,
                filter: "external_code IS NOT NULL AND deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_business_parties_tenant_id_group_code",
                table: "business_parties",
                columns: new[] { "tenant_id", "group_code" });

            migrationBuilder.CreateIndex(
                name: "ix_business_parties_tenant_id_is_blocked",
                table: "business_parties",
                columns: new[] { "tenant_id", "is_blocked" });

            migrationBuilder.CreateIndex(
                name: "ix_business_parties_tenant_id_parent_party_id",
                table: "business_parties",
                columns: new[] { "tenant_id", "parent_party_id" });

            migrationBuilder.CreateIndex(
                name: "ix_business_parties_tenant_id_phone",
                table: "business_parties",
                columns: new[] { "tenant_id", "phone" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_business_parties_tenant_id_external_code",
                table: "business_parties");

            migrationBuilder.DropIndex(
                name: "ix_business_parties_tenant_id_group_code",
                table: "business_parties");

            migrationBuilder.DropIndex(
                name: "ix_business_parties_tenant_id_is_blocked",
                table: "business_parties");

            migrationBuilder.DropIndex(
                name: "ix_business_parties_tenant_id_parent_party_id",
                table: "business_parties");

            migrationBuilder.DropIndex(
                name: "ix_business_parties_tenant_id_phone",
                table: "business_parties");

            migrationBuilder.DropColumn(
                name: "function_code",
                table: "party_contacts");

            migrationBuilder.DropColumn(
                name: "bank_code",
                table: "party_bank_accounts");

            migrationBuilder.DropColumn(
                name: "swift_bic",
                table: "party_bank_accounts");

            migrationBuilder.DropColumn(
                name: "assigned_user_id",
                table: "business_parties");

            migrationBuilder.DropColumn(
                name: "blocked_at",
                table: "business_parties");

            migrationBuilder.DropColumn(
                name: "blocked_reason",
                table: "business_parties");

            migrationBuilder.DropColumn(
                name: "credit_control_mode",
                table: "business_parties");

            migrationBuilder.DropColumn(
                name: "external_code",
                table: "business_parties");

            migrationBuilder.DropColumn(
                name: "group_code",
                table: "business_parties");

            migrationBuilder.DropColumn(
                name: "industry_code",
                table: "business_parties");

            migrationBuilder.DropColumn(
                name: "invoice_email",
                table: "business_parties");

            migrationBuilder.DropColumn(
                name: "is_blocked",
                table: "business_parties");

            migrationBuilder.DropColumn(
                name: "legal_type",
                table: "business_parties");

            migrationBuilder.DropColumn(
                name: "parent_party_id",
                table: "business_parties");

            migrationBuilder.DropColumn(
                name: "party_kind",
                table: "business_parties");

            migrationBuilder.DropColumn(
                name: "short_name",
                table: "business_parties");

            migrationBuilder.DropColumn(
                name: "vat_registered",
                table: "business_parties");
        }
    }
}
