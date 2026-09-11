using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint10_FinancialClose : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "financial_closes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    scope_id = table.Column<Guid>(type: "uuid", nullable: true),
                    period_from = table.Column<DateOnly>(type: "date", nullable: true),
                    period_to = table.Column<DateOnly>(type: "date", nullable: true),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    policy_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    base_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    notes = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    started_by = table.Column<Guid>(type: "uuid", nullable: true),
                    locked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    locked_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reopened_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reopened_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reopen_reason = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    supersedes_close_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("pk_financial_closes", x => x.id);
                    table.ForeignKey(
                        name: "fk_financial_closes_bills_scope_id",
                        column: x => x.scope_id,
                        principalTable: "bills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "financial_close_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    financial_close_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    scope_id = table.Column<Guid>(type: "uuid", nullable: true),
                    snapshot_version = table.Column<int>(type: "integer", nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    closed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    policy_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    base_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    immutable_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
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
                    table.PrimaryKey("pk_financial_close_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "fk_financial_close_snapshots_financial_closes_financial_close_",
                        column: x => x.financial_close_id,
                        principalTable: "financial_closes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "financial_close_snapshot_details",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_no = table.Column<int>(type: "integer", nullable: false),
                    metric_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    metric_value = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    source_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    source_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("pk_financial_close_snapshot_details", x => x.id);
                    table.ForeignKey(
                        name: "fk_financial_close_snapshot_details_financial_close_snapshots_",
                        column: x => x.snapshot_id,
                        principalTable: "financial_close_snapshots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_financial_close_snapshot_details_snapshot_id",
                table: "financial_close_snapshot_details",
                column: "snapshot_id");

            migrationBuilder.CreateIndex(
                name: "ix_financial_close_snapshot_details_tenant_id_snapshot_id_line",
                table: "financial_close_snapshot_details",
                columns: new[] { "tenant_id", "snapshot_id", "line_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_financial_close_snapshot_details_tenant_id_snapshot_id_metr",
                table: "financial_close_snapshot_details",
                columns: new[] { "tenant_id", "snapshot_id", "metric_key" });

            migrationBuilder.CreateIndex(
                name: "ix_financial_close_snapshots_financial_close_id",
                table: "financial_close_snapshots",
                column: "financial_close_id");

            migrationBuilder.CreateIndex(
                name: "ix_financial_close_snapshots_tenant_id_financial_close_id_snap",
                table: "financial_close_snapshots",
                columns: new[] { "tenant_id", "financial_close_id", "snapshot_version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_financial_close_snapshots_tenant_id_immutable_hash",
                table: "financial_close_snapshots",
                columns: new[] { "tenant_id", "immutable_hash" });

            migrationBuilder.CreateIndex(
                name: "ix_financial_close_snapshots_tenant_id_scope_type_scope_id_clo",
                table: "financial_close_snapshots",
                columns: new[] { "tenant_id", "scope_type", "scope_id", "closed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_financial_closes_scope_id",
                table: "financial_closes",
                column: "scope_id");

            migrationBuilder.CreateIndex(
                name: "ix_financial_closes_tenant_id_scope_type_scope_id_version_no",
                table: "financial_closes",
                columns: new[] { "tenant_id", "scope_type", "scope_id", "version_no" });

            migrationBuilder.CreateIndex(
                name: "ix_financial_closes_tenant_id_status_period_from_period_to",
                table: "financial_closes",
                columns: new[] { "tenant_id", "status", "period_from", "period_to" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "financial_close_snapshot_details");

            migrationBuilder.DropTable(
                name: "financial_close_snapshots");

            migrationBuilder.DropTable(
                name: "financial_closes");
        }
    }
}
