using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint3_RatePricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rate_cards",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    party_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("pk_rate_cards", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rate_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rate_card_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    effective_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    effective_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    note = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
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
                    table.PrimaryKey("pk_rate_versions", x => x.id);
                    table.ForeignKey(
                        name: "fk_rate_versions_rate_cards_rate_card_id",
                        column: x => x.rate_card_id,
                        principalTable: "rate_cards",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pricing_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rate_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    calc_method = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    unit_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    applicability = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("pk_pricing_rules", x => x.id);
                    table.ForeignKey(
                        name: "fk_pricing_rules_rate_versions_rate_version_id",
                        column: x => x.rate_version_id,
                        principalTable: "rate_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ratings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rate_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
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
                    table.PrimaryKey("pk_ratings", x => x.id);
                    table.ForeignKey(
                        name: "fk_ratings_bills_bill_id",
                        column: x => x.bill_id,
                        principalTable: "bills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ratings_rate_versions_rate_version_id",
                        column: x => x.rate_version_id,
                        principalTable: "rate_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pricing_rule_components",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pricing_rule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    financial_nature = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    cost_type_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    revenue_type_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_pricing_rule_components", x => x.id);
                    table.ForeignKey(
                        name: "fk_pricing_rule_components_pricing_rules_pricing_rule_id",
                        column: x => x.pricing_rule_id,
                        principalTable: "pricing_rules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rating_details",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rating_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pricing_rule_id = table.Column<Guid>(type: "uuid", nullable: true),
                    pricing_rule_component_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rule_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    component_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    component_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    financial_nature = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    financial_maturity = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
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
                    table.PrimaryKey("pk_rating_details", x => x.id);
                    table.ForeignKey(
                        name: "fk_rating_details_ratings_rating_id",
                        column: x => x.rating_id,
                        principalTable: "ratings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pricing_rule_components_pricing_rule_id",
                table: "pricing_rule_components",
                column: "pricing_rule_id");

            migrationBuilder.CreateIndex(
                name: "ix_pricing_rule_components_tenant_id_pricing_rule_id_code",
                table: "pricing_rule_components",
                columns: new[] { "tenant_id", "pricing_rule_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pricing_rules_rate_version_id",
                table: "pricing_rules",
                column: "rate_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_pricing_rules_tenant_id_rate_version_id_code",
                table: "pricing_rules",
                columns: new[] { "tenant_id", "rate_version_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rate_cards_tenant_id_code",
                table: "rate_cards",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rate_versions_rate_card_id",
                table: "rate_versions",
                column: "rate_card_id");

            migrationBuilder.CreateIndex(
                name: "ix_rate_versions_tenant_id_rate_card_id_status",
                table: "rate_versions",
                columns: new[] { "tenant_id", "rate_card_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_rate_versions_tenant_id_rate_card_id_version_no",
                table: "rate_versions",
                columns: new[] { "tenant_id", "rate_card_id", "version_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rating_details_rating_id",
                table: "rating_details",
                column: "rating_id");

            migrationBuilder.CreateIndex(
                name: "ix_rating_details_tenant_id_rating_id",
                table: "rating_details",
                columns: new[] { "tenant_id", "rating_id" });

            migrationBuilder.CreateIndex(
                name: "ix_ratings_bill_id",
                table: "ratings",
                column: "bill_id");

            migrationBuilder.CreateIndex(
                name: "ix_ratings_rate_version_id",
                table: "ratings",
                column: "rate_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_ratings_tenant_id_bill_id_rated_at",
                table: "ratings",
                columns: new[] { "tenant_id", "bill_id", "rated_at" });

            migrationBuilder.CreateIndex(
                name: "ix_ratings_tenant_id_rate_version_id",
                table: "ratings",
                columns: new[] { "tenant_id", "rate_version_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pricing_rule_components");

            migrationBuilder.DropTable(
                name: "rating_details");

            migrationBuilder.DropTable(
                name: "pricing_rules");

            migrationBuilder.DropTable(
                name: "ratings");

            migrationBuilder.DropTable(
                name: "rate_versions");

            migrationBuilder.DropTable(
                name: "rate_cards");
        }
    }
}
