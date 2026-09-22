using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OperationalCargoAndFieldOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "carrier_party_id",
                table: "transport_movements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "destination_location_id",
                table: "transport_movements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "movement_on",
                table: "transport_movements",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "origin_location_id",
                table: "transport_movements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "transport_mode",
                table: "transport_movements",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "destination_code",
                table: "transport_legs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "destination_location_id",
                table: "transport_legs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "origin_code",
                table: "transport_legs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "origin_location_id",
                table: "transport_legs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "sequence_no",
                table: "transport_legs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "carrier_name",
                table: "shipments",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "carrier_party_id",
                table: "shipments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "commodity_type_id",
                table: "shipments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "destination_location_id",
                table: "shipments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "origin_location_id",
                table: "shipments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "route_id",
                table: "shipments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "service_type_code",
                table: "shipments",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "commodity_type_id",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contact_channel",
                table: "orders",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contact_name",
                table: "orders",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "delivery_place",
                table: "orders",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "incoterm_code",
                table: "orders",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "order_date",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pickup_place",
                table: "orders",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "requested_at",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "service_type_code",
                table: "orders",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "bill_date",
                table: "bills",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "commodity_type_id",
                table: "bills",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "incoterm_code",
                table: "bills",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "master_bill_no",
                table: "bills",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "preferred_currency",
                table: "bills",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rate_date_policy",
                table: "bills",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "service_type_code",
                table: "bills",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "vendor_party_id",
                table: "bills",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "cargo_containers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    object_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    object_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence_no = table.Column<int>(type: "integer", nullable: false),
                    container_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    container_no = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    teu = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
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
                    table.PrimaryKey("pk_cargo_containers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cargo_packages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    object_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    object_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence_no = table.Column<int>(type: "integer", nullable: false),
                    package_count = table.Column<int>(type: "integer", nullable: false),
                    length_cm = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    width_cm = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    height_cm = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    weight_kg = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
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
                    table.PrimaryKey("pk_cargo_packages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "field_ownerships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    object_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    object_id = table.Column<Guid>(type: "uuid", nullable: false),
                    field_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    owner_system = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("pk_field_ownerships", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "operational_measurements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    object_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    object_id = table.Column<Guid>(type: "uuid", nullable: false),
                    measure_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    uom = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    source_channel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    rule_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    is_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("pk_operational_measurements", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cargo_containers_tenant_id_object_type_object_id_sequence_no",
                table: "cargo_containers",
                columns: new[] { "tenant_id", "object_type", "object_id", "sequence_no" });

            migrationBuilder.CreateIndex(
                name: "ix_cargo_packages_tenant_id_object_type_object_id_sequence_no",
                table: "cargo_packages",
                columns: new[] { "tenant_id", "object_type", "object_id", "sequence_no" });

            migrationBuilder.CreateIndex(
                name: "ix_field_ownerships_tenant_id_object_type_object_id_field_name",
                table: "field_ownerships",
                columns: new[] { "tenant_id", "object_type", "object_id", "field_name" },
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_operational_measurements_tenant_id_object_type_object_id_me",
                table: "operational_measurements",
                columns: new[] { "tenant_id", "object_type", "object_id", "measure_code" },
                unique: true,
                filter: "deleted_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cargo_containers");

            migrationBuilder.DropTable(
                name: "cargo_packages");

            migrationBuilder.DropTable(
                name: "field_ownerships");

            migrationBuilder.DropTable(
                name: "operational_measurements");

            migrationBuilder.DropColumn(
                name: "carrier_party_id",
                table: "transport_movements");

            migrationBuilder.DropColumn(
                name: "destination_location_id",
                table: "transport_movements");

            migrationBuilder.DropColumn(
                name: "movement_on",
                table: "transport_movements");

            migrationBuilder.DropColumn(
                name: "origin_location_id",
                table: "transport_movements");

            migrationBuilder.DropColumn(
                name: "transport_mode",
                table: "transport_movements");

            migrationBuilder.DropColumn(
                name: "destination_code",
                table: "transport_legs");

            migrationBuilder.DropColumn(
                name: "destination_location_id",
                table: "transport_legs");

            migrationBuilder.DropColumn(
                name: "origin_code",
                table: "transport_legs");

            migrationBuilder.DropColumn(
                name: "origin_location_id",
                table: "transport_legs");

            migrationBuilder.DropColumn(
                name: "sequence_no",
                table: "transport_legs");

            migrationBuilder.DropColumn(
                name: "carrier_name",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "carrier_party_id",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "commodity_type_id",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "destination_location_id",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "origin_location_id",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "route_id",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "service_type_code",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "commodity_type_id",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "contact_channel",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "contact_name",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "delivery_place",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "incoterm_code",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "order_date",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "pickup_place",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "requested_at",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "service_type_code",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "bill_date",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "commodity_type_id",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "incoterm_code",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "master_bill_no",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "preferred_currency",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "rate_date_policy",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "service_type_code",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "vendor_party_id",
                table: "bills");
        }
    }
}
