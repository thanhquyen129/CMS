using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint2_OperationalReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_no = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    source_system = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    external_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    external_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    operational_status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("pk_orders", x => x.id);
                    table.ForeignKey(
                        name: "fk_orders_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shipments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shipment_no = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    source_system = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    external_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    external_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    operational_status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("pk_shipments", x => x.id);
                    table.ForeignKey(
                        name: "fk_shipments_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "order_bill_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bill_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("pk_order_bill_links", x => x.id);
                    table.ForeignKey(
                        name: "fk_order_bill_links_bills_bill_id",
                        column: x => x.bill_id,
                        principalTable: "bills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_order_bill_links_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bill_shipment_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shipment_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("pk_bill_shipment_links", x => x.id);
                    table.ForeignKey(
                        name: "fk_bill_shipment_links_bills_bill_id",
                        column: x => x.bill_id,
                        principalTable: "bills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bill_shipment_links_shipments_shipment_id",
                        column: x => x.shipment_id,
                        principalTable: "shipments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bill_shipment_links_bill_id",
                table: "bill_shipment_links",
                column: "bill_id");

            migrationBuilder.CreateIndex(
                name: "ix_bill_shipment_links_shipment_id",
                table: "bill_shipment_links",
                column: "shipment_id");

            migrationBuilder.CreateIndex(
                name: "ix_bill_shipment_links_tenant_id_bill_id",
                table: "bill_shipment_links",
                columns: new[] { "tenant_id", "bill_id" });

            migrationBuilder.CreateIndex(
                name: "ix_bill_shipment_links_tenant_id_bill_id_shipment_id",
                table: "bill_shipment_links",
                columns: new[] { "tenant_id", "bill_id", "shipment_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bill_shipment_links_tenant_id_shipment_id",
                table: "bill_shipment_links",
                columns: new[] { "tenant_id", "shipment_id" });

            migrationBuilder.CreateIndex(
                name: "ix_order_bill_links_bill_id",
                table: "order_bill_links",
                column: "bill_id");

            migrationBuilder.CreateIndex(
                name: "ix_order_bill_links_order_id",
                table: "order_bill_links",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_order_bill_links_tenant_id_bill_id",
                table: "order_bill_links",
                columns: new[] { "tenant_id", "bill_id" });

            migrationBuilder.CreateIndex(
                name: "ix_order_bill_links_tenant_id_order_id",
                table: "order_bill_links",
                columns: new[] { "tenant_id", "order_id" });

            migrationBuilder.CreateIndex(
                name: "ix_order_bill_links_tenant_id_order_id_bill_id",
                table: "order_bill_links",
                columns: new[] { "tenant_id", "order_id", "bill_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_orders_tenant_id_order_no",
                table: "orders",
                columns: new[] { "tenant_id", "order_no" });

            migrationBuilder.CreateIndex(
                name: "ix_orders_tenant_id_source_system_external_id",
                table: "orders",
                columns: new[] { "tenant_id", "source_system", "external_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_shipments_tenant_id_shipment_no",
                table: "shipments",
                columns: new[] { "tenant_id", "shipment_no" });

            migrationBuilder.CreateIndex(
                name: "ix_shipments_tenant_id_source_system_external_id",
                table: "shipments",
                columns: new[] { "tenant_id", "source_system", "external_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bill_shipment_links");

            migrationBuilder.DropTable(
                name: "order_bill_links");

            migrationBuilder.DropTable(
                name: "shipments");

            migrationBuilder.DropTable(
                name: "orders");
        }
    }
}
