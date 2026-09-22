using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RevenueMappingProfitability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "revenue_mappings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    revenue_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    allocation_basis = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    applicability_mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    scope_id = table.Column<Guid>(type: "uuid", nullable: true),
                    condition_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    allocatable_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    allocated_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    mapped_maturity = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    mapping_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    finalized_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    finalized_by = table.Column<Guid>(type: "uuid", nullable: true),
                    supersedes_mapping_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("pk_revenue_mappings", x => x.id);
                    table.ForeignKey(
                        name: "fk_revenue_mappings_revenues_revenue_id",
                        column: x => x.revenue_id,
                        principalTable: "revenues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "revenue_mapping_details",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mapping_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    basis_value = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    basis_ratio = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    allocated_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    rounding_adjustment = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    manual_override_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    override_reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    override_before_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
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
                    table.PrimaryKey("pk_revenue_mapping_details", x => x.id);
                    table.ForeignKey(
                        name: "fk_revenue_mapping_details_bills_bill_id",
                        column: x => x.bill_id,
                        principalTable: "bills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_revenue_mapping_details_revenue_mappings_mapping_id",
                        column: x => x.mapping_id,
                        principalTable: "revenue_mappings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_revenue_mapping_details_bill_id",
                table: "revenue_mapping_details",
                column: "bill_id");

            migrationBuilder.CreateIndex(
                name: "ix_revenue_mapping_details_mapping_id",
                table: "revenue_mapping_details",
                column: "mapping_id");

            migrationBuilder.CreateIndex(
                name: "ix_revenue_mapping_details_tenant_id_mapping_id_bill_id",
                table: "revenue_mapping_details",
                columns: new[] { "tenant_id", "mapping_id", "bill_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_revenue_mappings_revenue_id",
                table: "revenue_mappings",
                column: "revenue_id");

            migrationBuilder.CreateIndex(
                name: "ix_revenue_mappings_tenant_id_revenue_id_version_no",
                table: "revenue_mappings",
                columns: new[] { "tenant_id", "revenue_id", "version_no" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "revenue_mapping_details");

            migrationBuilder.DropTable(
                name: "revenue_mappings");
        }
    }
}
