using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint7_ExposureApAr : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "payable_exposures",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bill_id = table.Column<Guid>(type: "uuid", nullable: true),
                    counterparty_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cost_id = table.Column<Guid>(type: "uuid", nullable: true),
                    financial_document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    recognized_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    effective_date = table.Column<DateOnly>(type: "date", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    notes = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    source_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    source_id = table.Column<Guid>(type: "uuid", nullable: true),
                    record_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
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
                    table.PrimaryKey("pk_payable_exposures", x => x.id);
                    table.ForeignKey(
                        name: "fk_payable_exposures_bills_bill_id",
                        column: x => x.bill_id,
                        principalTable: "bills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_payable_exposures_costs_cost_id",
                        column: x => x.cost_id,
                        principalTable: "costs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_payable_exposures_financial_documents_financial_document_id",
                        column: x => x.financial_document_id,
                        principalTable: "financial_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "receivable_exposures",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bill_id = table.Column<Guid>(type: "uuid", nullable: true),
                    counterparty_id = table.Column<Guid>(type: "uuid", nullable: true),
                    revenue_id = table.Column<Guid>(type: "uuid", nullable: true),
                    financial_document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    recognized_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    effective_date = table.Column<DateOnly>(type: "date", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    notes = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    source_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    source_id = table.Column<Guid>(type: "uuid", nullable: true),
                    record_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
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
                    table.PrimaryKey("pk_receivable_exposures", x => x.id);
                    table.ForeignKey(
                        name: "fk_receivable_exposures_bills_bill_id",
                        column: x => x.bill_id,
                        principalTable: "bills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_receivable_exposures_financial_documents_financial_document",
                        column: x => x.financial_document_id,
                        principalTable: "financial_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_receivable_exposures_revenues_revenue_id",
                        column: x => x.revenue_id,
                        principalTable: "revenues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "accounts_payable",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payable_exposure_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bill_id = table.Column<Guid>(type: "uuid", nullable: true),
                    counterparty_id = table.Column<Guid>(type: "uuid", nullable: true),
                    recognized_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    adjustment_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    finalized_settled_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    settlement_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    recognized_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    recognized_by = table.Column<Guid>(type: "uuid", nullable: true),
                    notes = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    record_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
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
                    table.PrimaryKey("pk_accounts_payable", x => x.id);
                    table.ForeignKey(
                        name: "fk_accounts_payable_bills_bill_id",
                        column: x => x.bill_id,
                        principalTable: "bills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_accounts_payable_payable_exposures_payable_exposure_id",
                        column: x => x.payable_exposure_id,
                        principalTable: "payable_exposures",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "accounts_receivable",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    receivable_exposure_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bill_id = table.Column<Guid>(type: "uuid", nullable: true),
                    counterparty_id = table.Column<Guid>(type: "uuid", nullable: true),
                    recognized_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    adjustment_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    finalized_settled_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    settlement_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    recognized_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    recognized_by = table.Column<Guid>(type: "uuid", nullable: true),
                    notes = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    record_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
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
                    table.PrimaryKey("pk_accounts_receivable", x => x.id);
                    table.ForeignKey(
                        name: "fk_accounts_receivable_bills_bill_id",
                        column: x => x.bill_id,
                        principalTable: "bills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_accounts_receivable_receivable_exposures_receivable_exposur",
                        column: x => x.receivable_exposure_id,
                        principalTable: "receivable_exposures",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_accounts_payable_bill_id",
                table: "accounts_payable",
                column: "bill_id");

            migrationBuilder.CreateIndex(
                name: "ix_accounts_payable_payable_exposure_id",
                table: "accounts_payable",
                column: "payable_exposure_id");

            migrationBuilder.CreateIndex(
                name: "ix_accounts_payable_tenant_id_counterparty_id_due_date_settlem",
                table: "accounts_payable",
                columns: new[] { "tenant_id", "counterparty_id", "due_date", "settlement_status" });

            migrationBuilder.CreateIndex(
                name: "ix_accounts_payable_tenant_id_payable_exposure_id",
                table: "accounts_payable",
                columns: new[] { "tenant_id", "payable_exposure_id" });

            migrationBuilder.CreateIndex(
                name: "ix_accounts_receivable_bill_id",
                table: "accounts_receivable",
                column: "bill_id");

            migrationBuilder.CreateIndex(
                name: "ix_accounts_receivable_receivable_exposure_id",
                table: "accounts_receivable",
                column: "receivable_exposure_id");

            migrationBuilder.CreateIndex(
                name: "ix_accounts_receivable_tenant_id_counterparty_id_due_date_sett",
                table: "accounts_receivable",
                columns: new[] { "tenant_id", "counterparty_id", "due_date", "settlement_status" });

            migrationBuilder.CreateIndex(
                name: "ix_accounts_receivable_tenant_id_receivable_exposure_id",
                table: "accounts_receivable",
                columns: new[] { "tenant_id", "receivable_exposure_id" });

            migrationBuilder.CreateIndex(
                name: "ix_payable_exposures_bill_id",
                table: "payable_exposures",
                column: "bill_id");

            migrationBuilder.CreateIndex(
                name: "ix_payable_exposures_cost_id",
                table: "payable_exposures",
                column: "cost_id");

            migrationBuilder.CreateIndex(
                name: "ix_payable_exposures_financial_document_id",
                table: "payable_exposures",
                column: "financial_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_payable_exposures_tenant_id_bill_id",
                table: "payable_exposures",
                columns: new[] { "tenant_id", "bill_id" });

            migrationBuilder.CreateIndex(
                name: "ix_payable_exposures_tenant_id_counterparty_id_due_date",
                table: "payable_exposures",
                columns: new[] { "tenant_id", "counterparty_id", "due_date" });

            migrationBuilder.CreateIndex(
                name: "ix_payable_exposures_tenant_id_status_effective_date",
                table: "payable_exposures",
                columns: new[] { "tenant_id", "status", "effective_date" });

            migrationBuilder.CreateIndex(
                name: "ix_receivable_exposures_bill_id",
                table: "receivable_exposures",
                column: "bill_id");

            migrationBuilder.CreateIndex(
                name: "ix_receivable_exposures_financial_document_id",
                table: "receivable_exposures",
                column: "financial_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_receivable_exposures_revenue_id",
                table: "receivable_exposures",
                column: "revenue_id");

            migrationBuilder.CreateIndex(
                name: "ix_receivable_exposures_tenant_id_bill_id",
                table: "receivable_exposures",
                columns: new[] { "tenant_id", "bill_id" });

            migrationBuilder.CreateIndex(
                name: "ix_receivable_exposures_tenant_id_counterparty_id_due_date",
                table: "receivable_exposures",
                columns: new[] { "tenant_id", "counterparty_id", "due_date" });

            migrationBuilder.CreateIndex(
                name: "ix_receivable_exposures_tenant_id_status_effective_date",
                table: "receivable_exposures",
                columns: new[] { "tenant_id", "status", "effective_date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "accounts_payable");

            migrationBuilder.DropTable(
                name: "accounts_receivable");

            migrationBuilder.DropTable(
                name: "payable_exposures");

            migrationBuilder.DropTable(
                name: "receivable_exposures");
        }
    }
}
