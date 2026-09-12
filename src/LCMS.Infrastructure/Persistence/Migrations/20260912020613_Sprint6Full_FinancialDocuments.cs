using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint6Full_FinancialDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cancel_reason",
                table: "financial_documents",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "cancelled_at",
                table: "financial_documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "cancelled_by",
                table: "financial_documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cancel_reason",
                table: "document_matches",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "cancelled_at",
                table: "document_matches",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "cancelled_by",
                table: "document_matches",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "tolerance_percent",
                table: "document_matches",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "detail_status",
                table: "document_match_details",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "active");

            migrationBuilder.AddColumn<string>(
                name: "reverse_reason",
                table: "document_match_details",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "reversed_at",
                table: "document_match_details",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "reversed_by",
                table: "document_match_details",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_document_match_details_tenant_id_detail_status",
                table: "document_match_details",
                columns: new[] { "tenant_id", "detail_status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_document_match_details_tenant_id_detail_status",
                table: "document_match_details");

            migrationBuilder.DropColumn(
                name: "cancel_reason",
                table: "financial_documents");

            migrationBuilder.DropColumn(
                name: "cancelled_at",
                table: "financial_documents");

            migrationBuilder.DropColumn(
                name: "cancelled_by",
                table: "financial_documents");

            migrationBuilder.DropColumn(
                name: "cancel_reason",
                table: "document_matches");

            migrationBuilder.DropColumn(
                name: "cancelled_at",
                table: "document_matches");

            migrationBuilder.DropColumn(
                name: "cancelled_by",
                table: "document_matches");

            migrationBuilder.DropColumn(
                name: "tolerance_percent",
                table: "document_matches");

            migrationBuilder.DropColumn(
                name: "detail_status",
                table: "document_match_details");

            migrationBuilder.DropColumn(
                name: "reverse_reason",
                table: "document_match_details");

            migrationBuilder.DropColumn(
                name: "reversed_at",
                table: "document_match_details");

            migrationBuilder.DropColumn(
                name: "reversed_by",
                table: "document_match_details");
        }
    }
}
