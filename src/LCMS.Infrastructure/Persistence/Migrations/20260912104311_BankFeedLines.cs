using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BankFeedLines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bank_feed_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    value_date = table.Column<DateOnly>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    direction = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    bank_reference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    counterparty_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    description = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    matched_reconciliation_detail_id = table.Column<Guid>(type: "uuid", nullable: true),
                    matched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ignored_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ignore_reason = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
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
                    table.PrimaryKey("pk_bank_feed_lines", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bank_feed_lines_tenant_id_bank_reference",
                table: "bank_feed_lines",
                columns: new[] { "tenant_id", "bank_reference" });

            migrationBuilder.CreateIndex(
                name: "ix_bank_feed_lines_tenant_id_matched_reconciliation_detail_id",
                table: "bank_feed_lines",
                columns: new[] { "tenant_id", "matched_reconciliation_detail_id" });

            migrationBuilder.CreateIndex(
                name: "ix_bank_feed_lines_tenant_id_status_value_date",
                table: "bank_feed_lines",
                columns: new[] { "tenant_id", "status", "value_date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bank_feed_lines");
        }
    }
}
