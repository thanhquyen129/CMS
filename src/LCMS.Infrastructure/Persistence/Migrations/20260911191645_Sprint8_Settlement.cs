using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint8_Settlement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "collections",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    value_date = table.Column<DateOnly>(type: "date", nullable: false),
                    counterparty_id = table.Column<Guid>(type: "uuid", nullable: true),
                    bill_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reference_no = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    notes = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
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
                    table.PrimaryKey("pk_collections", x => x.id);
                    table.ForeignKey(
                        name: "fk_collections_bills_bill_id",
                        column: x => x.bill_id,
                        principalTable: "bills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    value_date = table.Column<DateOnly>(type: "date", nullable: false),
                    counterparty_id = table.Column<Guid>(type: "uuid", nullable: true),
                    bill_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reference_no = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    notes = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
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
                    table.PrimaryKey("pk_payments", x => x.id);
                    table.ForeignKey(
                        name: "fk_payments_bills_bill_id",
                        column: x => x.bill_id,
                        principalTable: "bills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "collection_allocations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    collection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    accounts_receivable_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    allocation_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    finalized_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    finalized_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reversed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reversed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reverse_reason = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
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
                    table.PrimaryKey("pk_collection_allocations", x => x.id);
                    table.ForeignKey(
                        name: "fk_collection_allocations_accounts_receivable_accounts_receiva",
                        column: x => x.accounts_receivable_id,
                        principalTable: "accounts_receivable",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_collection_allocations_collections_collection_id",
                        column: x => x.collection_id,
                        principalTable: "collections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payment_allocations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    accounts_payable_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    allocation_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    finalized_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    finalized_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reversed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reversed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reverse_reason = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
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
                    table.PrimaryKey("pk_payment_allocations", x => x.id);
                    table.ForeignKey(
                        name: "fk_payment_allocations_accounts_payable_accounts_payable_id",
                        column: x => x.accounts_payable_id,
                        principalTable: "accounts_payable",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_payment_allocations_payments_payment_id",
                        column: x => x.payment_id,
                        principalTable: "payments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_collection_allocations_accounts_receivable_id",
                table: "collection_allocations",
                column: "accounts_receivable_id");

            migrationBuilder.CreateIndex(
                name: "ix_collection_allocations_collection_id",
                table: "collection_allocations",
                column: "collection_id");

            migrationBuilder.CreateIndex(
                name: "ix_collection_allocations_tenant_id_accounts_receivable_id_all",
                table: "collection_allocations",
                columns: new[] { "tenant_id", "accounts_receivable_id", "allocation_status" });

            migrationBuilder.CreateIndex(
                name: "ix_collection_allocations_tenant_id_collection_id_allocation_s",
                table: "collection_allocations",
                columns: new[] { "tenant_id", "collection_id", "allocation_status" });

            migrationBuilder.CreateIndex(
                name: "ix_collections_bill_id",
                table: "collections",
                column: "bill_id");

            migrationBuilder.CreateIndex(
                name: "ix_collections_tenant_id_bill_id",
                table: "collections",
                columns: new[] { "tenant_id", "bill_id" });

            migrationBuilder.CreateIndex(
                name: "ix_collections_tenant_id_value_date_counterparty_id",
                table: "collections",
                columns: new[] { "tenant_id", "value_date", "counterparty_id" });

            migrationBuilder.CreateIndex(
                name: "ix_payment_allocations_accounts_payable_id",
                table: "payment_allocations",
                column: "accounts_payable_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_allocations_payment_id",
                table: "payment_allocations",
                column: "payment_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_allocations_tenant_id_accounts_payable_id_allocatio",
                table: "payment_allocations",
                columns: new[] { "tenant_id", "accounts_payable_id", "allocation_status" });

            migrationBuilder.CreateIndex(
                name: "ix_payment_allocations_tenant_id_payment_id_allocation_status",
                table: "payment_allocations",
                columns: new[] { "tenant_id", "payment_id", "allocation_status" });

            migrationBuilder.CreateIndex(
                name: "ix_payments_bill_id",
                table: "payments",
                column: "bill_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_tenant_id_bill_id",
                table: "payments",
                columns: new[] { "tenant_id", "bill_id" });

            migrationBuilder.CreateIndex(
                name: "ix_payments_tenant_id_value_date_counterparty_id",
                table: "payments",
                columns: new[] { "tenant_id", "value_date", "counterparty_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "collection_allocations");

            migrationBuilder.DropTable(
                name: "payment_allocations");

            migrationBuilder.DropTable(
                name: "collections");

            migrationBuilder.DropTable(
                name: "payments");
        }
    }
}
