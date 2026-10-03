using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FinancialDocumentSourceMatching : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "base_amount",
                table: "financial_documents",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "fx_applied_at",
                table: "financial_documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "fx_applied_by",
                table: "financial_documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fx_override_reason",
                table: "financial_documents",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "fx_rate",
                table: "financial_documents",
                type: "numeric(18,8)",
                precision: 18,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "fx_rate_date",
                table: "financial_documents",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "fx_rate_id",
                table: "financial_documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fx_source_name",
                table: "financial_documents",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fx_source_type",
                table: "financial_documents",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fx_status",
                table: "financial_documents",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "missing");

            migrationBuilder.AddColumn<string>(
                name: "mode",
                table: "financial_documents",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "external_received");

            migrationBuilder.AddColumn<string>(
                name: "reporting_currency_code",
                table: "financial_documents",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "bill_id",
                table: "document_match_details",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "matched_reporting_amount",
                table: "document_match_details",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "source_original_amount",
                table: "document_match_details",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_type",
                table: "document_match_details",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "variance_amount",
                table: "document_match_details",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_document_match_details_bill_id",
                table: "document_match_details",
                column: "bill_id");

            migrationBuilder.CreateIndex(
                name: "ix_document_match_details_tenant_id_bill_id",
                table: "document_match_details",
                columns: new[] { "tenant_id", "bill_id" });

            migrationBuilder.AddForeignKey(
                name: "fk_document_match_details_bills_bill_id",
                table: "document_match_details",
                column: "bill_id",
                principalTable: "bills",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_document_match_details_bills_bill_id",
                table: "document_match_details");

            migrationBuilder.DropIndex(
                name: "ix_document_match_details_bill_id",
                table: "document_match_details");

            migrationBuilder.DropIndex(
                name: "ix_document_match_details_tenant_id_bill_id",
                table: "document_match_details");

            migrationBuilder.DropColumn(
                name: "base_amount",
                table: "financial_documents");

            migrationBuilder.DropColumn(
                name: "fx_applied_at",
                table: "financial_documents");

            migrationBuilder.DropColumn(
                name: "fx_applied_by",
                table: "financial_documents");

            migrationBuilder.DropColumn(
                name: "fx_override_reason",
                table: "financial_documents");

            migrationBuilder.DropColumn(
                name: "fx_rate",
                table: "financial_documents");

            migrationBuilder.DropColumn(
                name: "fx_rate_date",
                table: "financial_documents");

            migrationBuilder.DropColumn(
                name: "fx_rate_id",
                table: "financial_documents");

            migrationBuilder.DropColumn(
                name: "fx_source_name",
                table: "financial_documents");

            migrationBuilder.DropColumn(
                name: "fx_source_type",
                table: "financial_documents");

            migrationBuilder.DropColumn(
                name: "fx_status",
                table: "financial_documents");

            migrationBuilder.DropColumn(
                name: "mode",
                table: "financial_documents");

            migrationBuilder.DropColumn(
                name: "reporting_currency_code",
                table: "financial_documents");

            migrationBuilder.DropColumn(
                name: "bill_id",
                table: "document_match_details");

            migrationBuilder.DropColumn(
                name: "matched_reporting_amount",
                table: "document_match_details");

            migrationBuilder.DropColumn(
                name: "source_original_amount",
                table: "document_match_details");

            migrationBuilder.DropColumn(
                name: "source_type",
                table: "document_match_details");

            migrationBuilder.DropColumn(
                name: "variance_amount",
                table: "document_match_details");
        }
    }
}
