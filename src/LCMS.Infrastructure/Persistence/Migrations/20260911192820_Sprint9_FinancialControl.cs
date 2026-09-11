using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint9_FinancialControl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "approvals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    object_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    object_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    requested_by = table.Column<Guid>(type: "uuid", nullable: true),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    request_reason = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    decided_by = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decision_reason = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    notes = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
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
                    table.PrimaryKey("pk_approvals", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "reconciliations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reconciliation_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    rule_code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    bill_id = table.Column<Guid>(type: "uuid", nullable: true),
                    notes = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    started_by = table.Column<Guid>(type: "uuid", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_by = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("pk_reconciliations", x => x.id);
                    table.ForeignKey(
                        name: "fk_reconciliations_bills_bill_id",
                        column: x => x.bill_id,
                        principalTable: "bills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "exceptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rule_code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    severity = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    description = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    bill_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reconciliation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    variance_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    resolution_notes = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_by = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("pk_exceptions", x => x.id);
                    table.ForeignKey(
                        name: "fk_exceptions_bills_bill_id",
                        column: x => x.bill_id,
                        principalTable: "bills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_exceptions_reconciliations_reconciliation_id",
                        column: x => x.reconciliation_id,
                        principalTable: "reconciliations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reconciliation_details",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reconciliation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    target_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    target_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    matched_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    variance_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    line_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    variance_id = table.Column<Guid>(type: "uuid", nullable: true),
                    notes = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
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
                    table.PrimaryKey("pk_reconciliation_details", x => x.id);
                    table.ForeignKey(
                        name: "fk_reconciliation_details_reconciliations_reconciliation_id",
                        column: x => x.reconciliation_id,
                        principalTable: "reconciliations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "variances",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reconciliation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reconciliation_detail_id = table.Column<Guid>(type: "uuid", nullable: true),
                    variance_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    source_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    target_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    explanation = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    exception_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("pk_variances", x => x.id);
                    table.ForeignKey(
                        name: "fk_variances_reconciliation_details_reconciliation_detail_id",
                        column: x => x.reconciliation_detail_id,
                        principalTable: "reconciliation_details",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_variances_reconciliations_reconciliation_id",
                        column: x => x.reconciliation_id,
                        principalTable: "reconciliations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_approvals_tenant_id_object_type_object_id_status",
                table: "approvals",
                columns: new[] { "tenant_id", "object_type", "object_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_approvals_tenant_id_status_requested_at",
                table: "approvals",
                columns: new[] { "tenant_id", "status", "requested_at" });

            migrationBuilder.CreateIndex(
                name: "ix_exceptions_bill_id",
                table: "exceptions",
                column: "bill_id");

            migrationBuilder.CreateIndex(
                name: "ix_exceptions_reconciliation_id",
                table: "exceptions",
                column: "reconciliation_id");

            migrationBuilder.CreateIndex(
                name: "ix_exceptions_tenant_id_reconciliation_id",
                table: "exceptions",
                columns: new[] { "tenant_id", "reconciliation_id" });

            migrationBuilder.CreateIndex(
                name: "ix_exceptions_tenant_id_status_severity_owner_id_due_at",
                table: "exceptions",
                columns: new[] { "tenant_id", "status", "severity", "owner_id", "due_at" });

            migrationBuilder.CreateIndex(
                name: "ix_exceptions_tenant_id_variance_id",
                table: "exceptions",
                columns: new[] { "tenant_id", "variance_id" });

            migrationBuilder.CreateIndex(
                name: "ix_reconciliation_details_reconciliation_id",
                table: "reconciliation_details",
                column: "reconciliation_id");

            migrationBuilder.CreateIndex(
                name: "ix_reconciliation_details_tenant_id_reconciliation_id",
                table: "reconciliation_details",
                columns: new[] { "tenant_id", "reconciliation_id" });

            migrationBuilder.CreateIndex(
                name: "ix_reconciliation_details_tenant_id_source_type_source_id",
                table: "reconciliation_details",
                columns: new[] { "tenant_id", "source_type", "source_id" });

            migrationBuilder.CreateIndex(
                name: "ix_reconciliation_details_tenant_id_variance_id",
                table: "reconciliation_details",
                columns: new[] { "tenant_id", "variance_id" });

            migrationBuilder.CreateIndex(
                name: "ix_reconciliations_bill_id",
                table: "reconciliations",
                column: "bill_id");

            migrationBuilder.CreateIndex(
                name: "ix_reconciliations_tenant_id_bill_id_version_no",
                table: "reconciliations",
                columns: new[] { "tenant_id", "bill_id", "version_no" });

            migrationBuilder.CreateIndex(
                name: "ix_reconciliations_tenant_id_status_reconciliation_type",
                table: "reconciliations",
                columns: new[] { "tenant_id", "status", "reconciliation_type" });

            migrationBuilder.CreateIndex(
                name: "ix_variances_reconciliation_detail_id",
                table: "variances",
                column: "reconciliation_detail_id");

            migrationBuilder.CreateIndex(
                name: "ix_variances_reconciliation_id",
                table: "variances",
                column: "reconciliation_id");

            migrationBuilder.CreateIndex(
                name: "ix_variances_tenant_id_exception_id",
                table: "variances",
                columns: new[] { "tenant_id", "exception_id" });

            migrationBuilder.CreateIndex(
                name: "ix_variances_tenant_id_reconciliation_id",
                table: "variances",
                columns: new[] { "tenant_id", "reconciliation_id" });

            migrationBuilder.CreateIndex(
                name: "ix_variances_tenant_id_status_variance_type",
                table: "variances",
                columns: new[] { "tenant_id", "status", "variance_type" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "approvals");

            migrationBuilder.DropTable(
                name: "exceptions");

            migrationBuilder.DropTable(
                name: "variances");

            migrationBuilder.DropTable(
                name: "reconciliation_details");

            migrationBuilder.DropTable(
                name: "reconciliations");
        }
    }
}
