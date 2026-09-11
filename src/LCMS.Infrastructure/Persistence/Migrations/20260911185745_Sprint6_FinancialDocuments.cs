using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint6_FinancialDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "financial_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    document_no = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    counterparty_id = table.Column<Guid>(type: "uuid", nullable: true),
                    bill_id = table.Column<Guid>(type: "uuid", nullable: true),
                    direction = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    document_date = table.Column<DateOnly>(type: "date", nullable: false),
                    receipt_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    acceptance_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    matching_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    received_by = table.Column<Guid>(type: "uuid", nullable: true),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    accepted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    record_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    notes = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    source_system = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    external_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
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
                    table.PrimaryKey("pk_financial_documents", x => x.id);
                    table.ForeignKey(
                        name: "fk_financial_documents_bills_bill_id",
                        column: x => x.bill_id,
                        principalTable: "bills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "document_matches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    match_method = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    match_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    primary_document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tolerance_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    notes = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    confirmed_by = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("pk_document_matches", x => x.id);
                    table.ForeignKey(
                        name: "fk_document_matches_financial_documents_primary_document_id",
                        column: x => x.primary_document_id,
                        principalTable: "financial_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "financial_document_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_no = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    matched_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    bill_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cost_type_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    revenue_type_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
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
                    table.PrimaryKey("pk_financial_document_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_financial_document_lines_financial_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "financial_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "document_match_details",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    match_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_line_id = table.Column<Guid>(type: "uuid", nullable: true),
                    target_cost_id = table.Column<Guid>(type: "uuid", nullable: true),
                    target_revenue_id = table.Column<Guid>(type: "uuid", nullable: true),
                    matched_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
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
                    table.PrimaryKey("pk_document_match_details", x => x.id);
                    table.ForeignKey(
                        name: "fk_document_match_details_costs_target_cost_id",
                        column: x => x.target_cost_id,
                        principalTable: "costs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_document_match_details_document_matches_match_id",
                        column: x => x.match_id,
                        principalTable: "document_matches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_document_match_details_financial_document_lines_source_line",
                        column: x => x.source_line_id,
                        principalTable: "financial_document_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_document_match_details_financial_document_lines_target_line",
                        column: x => x.target_line_id,
                        principalTable: "financial_document_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_document_match_details_revenues_target_revenue_id",
                        column: x => x.target_revenue_id,
                        principalTable: "revenues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_document_match_details_match_id",
                table: "document_match_details",
                column: "match_id");

            migrationBuilder.CreateIndex(
                name: "ix_document_match_details_source_line_id",
                table: "document_match_details",
                column: "source_line_id");

            migrationBuilder.CreateIndex(
                name: "ix_document_match_details_target_cost_id",
                table: "document_match_details",
                column: "target_cost_id");

            migrationBuilder.CreateIndex(
                name: "ix_document_match_details_target_line_id",
                table: "document_match_details",
                column: "target_line_id");

            migrationBuilder.CreateIndex(
                name: "ix_document_match_details_target_revenue_id",
                table: "document_match_details",
                column: "target_revenue_id");

            migrationBuilder.CreateIndex(
                name: "ix_document_match_details_tenant_id_match_id",
                table: "document_match_details",
                columns: new[] { "tenant_id", "match_id" });

            migrationBuilder.CreateIndex(
                name: "ix_document_match_details_tenant_id_source_line_id",
                table: "document_match_details",
                columns: new[] { "tenant_id", "source_line_id" });

            migrationBuilder.CreateIndex(
                name: "ix_document_match_details_tenant_id_target_line_id",
                table: "document_match_details",
                columns: new[] { "tenant_id", "target_line_id" });

            migrationBuilder.CreateIndex(
                name: "ix_document_matches_primary_document_id",
                table: "document_matches",
                column: "primary_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_document_matches_tenant_id_primary_document_id_version_no",
                table: "document_matches",
                columns: new[] { "tenant_id", "primary_document_id", "version_no" });

            migrationBuilder.CreateIndex(
                name: "ix_financial_document_lines_document_id",
                table: "financial_document_lines",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "ix_financial_document_lines_tenant_id_document_id_line_no",
                table: "financial_document_lines",
                columns: new[] { "tenant_id", "document_id", "line_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_financial_documents_bill_id",
                table: "financial_documents",
                column: "bill_id");

            migrationBuilder.CreateIndex(
                name: "ix_financial_documents_tenant_id_document_type_document_no_cou",
                table: "financial_documents",
                columns: new[] { "tenant_id", "document_type", "document_no", "counterparty_id" });

            migrationBuilder.CreateIndex(
                name: "ix_financial_documents_tenant_id_source_system_external_id",
                table: "financial_documents",
                columns: new[] { "tenant_id", "source_system", "external_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_match_details");

            migrationBuilder.DropTable(
                name: "document_matches");

            migrationBuilder.DropTable(
                name: "financial_document_lines");

            migrationBuilder.DropTable(
                name: "financial_documents");
        }
    }
}
