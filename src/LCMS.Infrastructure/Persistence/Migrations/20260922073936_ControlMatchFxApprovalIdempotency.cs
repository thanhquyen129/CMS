using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ControlMatchFxApprovalIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "fx_rate",
                table: "payment_allocations",
                type: "numeric(18,8)",
                precision: 18,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "fx_rate_date",
                table: "payment_allocations",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fx_source",
                table: "payment_allocations",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "original_amount",
                table: "payment_allocations",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "settled_amount",
                table: "payment_allocations",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "applied_tolerance",
                table: "document_match_details",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "outcome_code",
                table: "document_match_details",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "matched");

            migrationBuilder.AddColumn<decimal>(
                name: "fx_rate",
                table: "collection_allocations",
                type: "numeric(18,8)",
                precision: 18,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "fx_rate_date",
                table: "collection_allocations",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fx_source",
                table: "collection_allocations",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "original_amount",
                table: "collection_allocations",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "settled_amount",
                table: "collection_allocations",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "object_fingerprint",
                table: "approvals",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "idempotency_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    object_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("pk_idempotency_records", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_idempotency_records_tenant_id_scope_key",
                table: "idempotency_records",
                columns: new[] { "tenant_id", "scope", "key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "idempotency_records");

            migrationBuilder.DropColumn(
                name: "fx_rate",
                table: "payment_allocations");

            migrationBuilder.DropColumn(
                name: "fx_rate_date",
                table: "payment_allocations");

            migrationBuilder.DropColumn(
                name: "fx_source",
                table: "payment_allocations");

            migrationBuilder.DropColumn(
                name: "original_amount",
                table: "payment_allocations");

            migrationBuilder.DropColumn(
                name: "settled_amount",
                table: "payment_allocations");

            migrationBuilder.DropColumn(
                name: "applied_tolerance",
                table: "document_match_details");

            migrationBuilder.DropColumn(
                name: "outcome_code",
                table: "document_match_details");

            migrationBuilder.DropColumn(
                name: "fx_rate",
                table: "collection_allocations");

            migrationBuilder.DropColumn(
                name: "fx_rate_date",
                table: "collection_allocations");

            migrationBuilder.DropColumn(
                name: "fx_source",
                table: "collection_allocations");

            migrationBuilder.DropColumn(
                name: "original_amount",
                table: "collection_allocations");

            migrationBuilder.DropColumn(
                name: "settled_amount",
                table: "collection_allocations");

            migrationBuilder.DropColumn(
                name: "object_fingerprint",
                table: "approvals");
        }
    }
}
