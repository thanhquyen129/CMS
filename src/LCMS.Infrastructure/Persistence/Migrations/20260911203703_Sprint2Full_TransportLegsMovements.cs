using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint2Full_TransportLegsMovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "transport_legs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    leg_no = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    shipment_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("pk_transport_legs", x => x.id);
                    table.ForeignKey(
                        name: "fk_transport_legs_shipments_shipment_id",
                        column: x => x.shipment_id,
                        principalTable: "shipments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_transport_legs_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "transport_movements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    movement_no = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("pk_transport_movements", x => x.id);
                    table.ForeignKey(
                        name: "fk_transport_movements_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bill_leg_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transport_leg_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("pk_bill_leg_links", x => x.id);
                    table.ForeignKey(
                        name: "fk_bill_leg_links_bills_bill_id",
                        column: x => x.bill_id,
                        principalTable: "bills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bill_leg_links_transport_legs_transport_leg_id",
                        column: x => x.transport_leg_id,
                        principalTable: "transport_legs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bill_movement_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transport_movement_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("pk_bill_movement_links", x => x.id);
                    table.ForeignKey(
                        name: "fk_bill_movement_links_bills_bill_id",
                        column: x => x.bill_id,
                        principalTable: "bills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bill_movement_links_transport_movements_transport_movement_",
                        column: x => x.transport_movement_id,
                        principalTable: "transport_movements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "leg_movement_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    transport_leg_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transport_movement_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("pk_leg_movement_links", x => x.id);
                    table.ForeignKey(
                        name: "fk_leg_movement_links_transport_legs_transport_leg_id",
                        column: x => x.transport_leg_id,
                        principalTable: "transport_legs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_leg_movement_links_transport_movements_transport_movement_id",
                        column: x => x.transport_movement_id,
                        principalTable: "transport_movements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bill_leg_links_bill_id",
                table: "bill_leg_links",
                column: "bill_id");

            migrationBuilder.CreateIndex(
                name: "ix_bill_leg_links_tenant_id_bill_id",
                table: "bill_leg_links",
                columns: new[] { "tenant_id", "bill_id" });

            migrationBuilder.CreateIndex(
                name: "ix_bill_leg_links_tenant_id_bill_id_transport_leg_id",
                table: "bill_leg_links",
                columns: new[] { "tenant_id", "bill_id", "transport_leg_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bill_leg_links_tenant_id_transport_leg_id",
                table: "bill_leg_links",
                columns: new[] { "tenant_id", "transport_leg_id" });

            migrationBuilder.CreateIndex(
                name: "ix_bill_leg_links_transport_leg_id",
                table: "bill_leg_links",
                column: "transport_leg_id");

            migrationBuilder.CreateIndex(
                name: "ix_bill_movement_links_bill_id",
                table: "bill_movement_links",
                column: "bill_id");

            migrationBuilder.CreateIndex(
                name: "ix_bill_movement_links_tenant_id_bill_id",
                table: "bill_movement_links",
                columns: new[] { "tenant_id", "bill_id" });

            migrationBuilder.CreateIndex(
                name: "ix_bill_movement_links_tenant_id_bill_id_transport_movement_id",
                table: "bill_movement_links",
                columns: new[] { "tenant_id", "bill_id", "transport_movement_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bill_movement_links_tenant_id_transport_movement_id",
                table: "bill_movement_links",
                columns: new[] { "tenant_id", "transport_movement_id" });

            migrationBuilder.CreateIndex(
                name: "ix_bill_movement_links_transport_movement_id",
                table: "bill_movement_links",
                column: "transport_movement_id");

            migrationBuilder.CreateIndex(
                name: "ix_leg_movement_links_tenant_id_transport_leg_id",
                table: "leg_movement_links",
                columns: new[] { "tenant_id", "transport_leg_id" });

            migrationBuilder.CreateIndex(
                name: "ix_leg_movement_links_tenant_id_transport_leg_id_transport_mov",
                table: "leg_movement_links",
                columns: new[] { "tenant_id", "transport_leg_id", "transport_movement_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_leg_movement_links_tenant_id_transport_movement_id",
                table: "leg_movement_links",
                columns: new[] { "tenant_id", "transport_movement_id" });

            migrationBuilder.CreateIndex(
                name: "ix_leg_movement_links_transport_leg_id",
                table: "leg_movement_links",
                column: "transport_leg_id");

            migrationBuilder.CreateIndex(
                name: "ix_leg_movement_links_transport_movement_id",
                table: "leg_movement_links",
                column: "transport_movement_id");

            migrationBuilder.CreateIndex(
                name: "ix_transport_legs_shipment_id",
                table: "transport_legs",
                column: "shipment_id");

            migrationBuilder.CreateIndex(
                name: "ix_transport_legs_tenant_id_leg_no",
                table: "transport_legs",
                columns: new[] { "tenant_id", "leg_no" });

            migrationBuilder.CreateIndex(
                name: "ix_transport_legs_tenant_id_shipment_id",
                table: "transport_legs",
                columns: new[] { "tenant_id", "shipment_id" });

            migrationBuilder.CreateIndex(
                name: "ix_transport_legs_tenant_id_source_system_external_id",
                table: "transport_legs",
                columns: new[] { "tenant_id", "source_system", "external_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_transport_movements_tenant_id_movement_no",
                table: "transport_movements",
                columns: new[] { "tenant_id", "movement_no" });

            migrationBuilder.CreateIndex(
                name: "ix_transport_movements_tenant_id_source_system_external_id",
                table: "transport_movements",
                columns: new[] { "tenant_id", "source_system", "external_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bill_leg_links");

            migrationBuilder.DropTable(
                name: "bill_movement_links");

            migrationBuilder.DropTable(
                name: "leg_movement_links");

            migrationBuilder.DropTable(
                name: "transport_legs");

            migrationBuilder.DropTable(
                name: "transport_movements");
        }
    }
}
