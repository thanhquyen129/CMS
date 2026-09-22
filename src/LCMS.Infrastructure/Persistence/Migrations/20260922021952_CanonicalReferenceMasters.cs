using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CanonicalReferenceMasters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "bill_party_policy_json",
                table: "tenant_settings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "destination_location_id",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "origin_location_id",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "route_id",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "bill_to_party_id",
                table: "bills",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "consignee_party_id",
                table: "bills",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "destination_location_id",
                table: "bills",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "origin_location_id",
                table: "bills",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "payer_party_id",
                table: "bills",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "route_id",
                table: "bills",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "shipper_party_id",
                table: "bills",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "commodity_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    category = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_dangerous_goods = table.Column<bool>(type: "boolean", nullable: false),
                    is_temperature_controlled = table.Column<bool>(type: "boolean", nullable: false),
                    is_oversize = table.Column<bool>(type: "boolean", nullable: false),
                    is_overweight = table.Column<bool>(type: "boolean", nullable: false),
                    is_high_value = table.Column<bool>(type: "boolean", nullable: false),
                    special_handling = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
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
                    table.PrimaryKey("pk_commodity_types", x => x.id);
                    table.ForeignKey(
                        name: "fk_commodity_types_commodity_types_parent_id",
                        column: x => x.parent_id,
                        principalTable: "commodity_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "locations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    location_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    subdivision = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    city = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    iata_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    unlocode = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    terminal_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
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
                    table.PrimaryKey("pk_locations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "operational_party_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    object_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    object_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    party_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_walk_in = table.Column<bool>(type: "boolean", nullable: false),
                    display_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    legal_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    tax_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    phone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    address_line1 = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    city = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    contact_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    contact_phone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    contact_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    captured_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    source_channel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    superseded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("pk_operational_party_snapshots", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "location_aliases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    alias_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    source_system = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
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
                    table.PrimaryKey("pk_location_aliases", x => x.id);
                    table.ForeignKey(
                        name: "fk_location_aliases_locations_location_id",
                        column: x => x.location_id,
                        principalTable: "locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "routes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    origin_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    destination_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transport_mode_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    service_type_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
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
                    table.PrimaryKey("pk_routes", x => x.id);
                    table.ForeignKey(
                        name: "fk_routes_locations_destination_location_id",
                        column: x => x.destination_location_id,
                        principalTable: "locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_routes_locations_origin_location_id",
                        column: x => x.origin_location_id,
                        principalTable: "locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "route_stops",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    route_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence_no = table.Column<int>(type: "integer", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("pk_route_stops", x => x.id);
                    table.ForeignKey(
                        name: "fk_route_stops_locations_location_id",
                        column: x => x.location_id,
                        principalTable: "locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_route_stops_routes_route_id",
                        column: x => x.route_id,
                        principalTable: "routes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bills_tenant_id_destination_location_id",
                table: "bills",
                columns: new[] { "tenant_id", "destination_location_id" });

            migrationBuilder.CreateIndex(
                name: "ix_bills_tenant_id_origin_location_id",
                table: "bills",
                columns: new[] { "tenant_id", "origin_location_id" });

            migrationBuilder.CreateIndex(
                name: "ix_bills_tenant_id_route_id",
                table: "bills",
                columns: new[] { "tenant_id", "route_id" });

            migrationBuilder.CreateIndex(
                name: "ix_commodity_types_parent_id",
                table: "commodity_types",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_commodity_types_tenant_id_code",
                table: "commodity_types",
                columns: new[] { "tenant_id", "code" },
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_commodity_types_tenant_id_parent_id",
                table: "commodity_types",
                columns: new[] { "tenant_id", "parent_id" });

            migrationBuilder.CreateIndex(
                name: "ix_location_aliases_location_id",
                table: "location_aliases",
                column: "location_id");

            migrationBuilder.CreateIndex(
                name: "ix_location_aliases_tenant_id_alias_code",
                table: "location_aliases",
                columns: new[] { "tenant_id", "alias_code" },
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_location_aliases_tenant_id_location_id",
                table: "location_aliases",
                columns: new[] { "tenant_id", "location_id" });

            migrationBuilder.CreateIndex(
                name: "ix_locations_tenant_id_code",
                table: "locations",
                columns: new[] { "tenant_id", "code" },
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_locations_tenant_id_iata_code",
                table: "locations",
                columns: new[] { "tenant_id", "iata_code" });

            migrationBuilder.CreateIndex(
                name: "ix_locations_tenant_id_location_type_is_active",
                table: "locations",
                columns: new[] { "tenant_id", "location_type", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_locations_tenant_id_unlocode",
                table: "locations",
                columns: new[] { "tenant_id", "unlocode" });

            migrationBuilder.CreateIndex(
                name: "ix_operational_party_snapshots_tenant_id_object_type_object_id",
                table: "operational_party_snapshots",
                columns: new[] { "tenant_id", "object_type", "object_id", "role_code", "superseded_at" });

            migrationBuilder.CreateIndex(
                name: "ix_route_stops_location_id",
                table: "route_stops",
                column: "location_id");

            migrationBuilder.CreateIndex(
                name: "ix_route_stops_route_id",
                table: "route_stops",
                column: "route_id");

            migrationBuilder.CreateIndex(
                name: "ix_route_stops_tenant_id_route_id_sequence_no",
                table: "route_stops",
                columns: new[] { "tenant_id", "route_id", "sequence_no" });

            migrationBuilder.CreateIndex(
                name: "ix_routes_destination_location_id",
                table: "routes",
                column: "destination_location_id");

            migrationBuilder.CreateIndex(
                name: "ix_routes_origin_location_id",
                table: "routes",
                column: "origin_location_id");

            migrationBuilder.CreateIndex(
                name: "ix_routes_tenant_id_code",
                table: "routes",
                columns: new[] { "tenant_id", "code" },
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_routes_tenant_id_origin_location_id_destination_location_id",
                table: "routes",
                columns: new[] { "tenant_id", "origin_location_id", "destination_location_id" });

            migrationBuilder.Sql(
                """
                INSERT INTO locations (id, code, name, location_type, is_active, row_version, created_at, tenant_id)
                SELECT gen_random_uuid(),
                       upper(left(m.code, 30)),
                       m.name,
                       CASE lower(coalesce(
                           CASE WHEN m.attributes_json IS NOT NULL AND left(btrim(m.attributes_json), 1) = '{'
                                THEN m.attributes_json::jsonb ->> 'class' END, 'other'))
                           WHEN 'airport' THEN 'airport'
                           WHEN 'port' THEN 'port'
                           WHEN 'city' THEN 'city'
                           WHEN 'depot' THEN 'depot'
                           WHEN 'border' THEN 'border'
                           ELSE 'other'
                       END,
                       m.is_active,
                       decode('00', 'hex'),
                       m.created_at,
                       m.tenant_id
                FROM master_catalog_items m
                WHERE m.kind = 'location'
                  AND m.deleted_at IS NULL
                  AND NOT EXISTS (
                      SELECT 1 FROM locations l
                      WHERE l.tenant_id = m.tenant_id
                        AND l.code = upper(left(m.code, 30))
                        AND l.deleted_at IS NULL);

                INSERT INTO routes (
                    id, code, name, origin_location_id, destination_location_id,
                    is_active, row_version, created_at, tenant_id)
                SELECT gen_random_uuid(),
                       upper(left(m.code, 64)),
                       m.name,
                       o.id,
                       d.id,
                       m.is_active,
                       decode('00', 'hex'),
                       m.created_at,
                       m.tenant_id
                FROM master_catalog_items m
                JOIN locations o
                  ON o.tenant_id = m.tenant_id
                 AND o.deleted_at IS NULL
                 AND o.code = upper(left(
                     CASE WHEN m.attributes_json IS NOT NULL AND left(btrim(m.attributes_json), 1) = '{'
                          THEN m.attributes_json::jsonb ->> 'origin' END, 30))
                JOIN locations d
                  ON d.tenant_id = m.tenant_id
                 AND d.deleted_at IS NULL
                 AND d.code = upper(left(
                     CASE WHEN m.attributes_json IS NOT NULL AND left(btrim(m.attributes_json), 1) = '{'
                          THEN m.attributes_json::jsonb ->> 'destination' END, 30))
                WHERE m.kind = 'transport_route'
                  AND m.deleted_at IS NULL
                  AND o.id <> d.id
                  AND NOT EXISTS (
                      SELECT 1 FROM routes r
                      WHERE r.tenant_id = m.tenant_id
                        AND r.code = upper(left(m.code, 64))
                        AND r.deleted_at IS NULL);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "commodity_types");

            migrationBuilder.DropTable(
                name: "location_aliases");

            migrationBuilder.DropTable(
                name: "operational_party_snapshots");

            migrationBuilder.DropTable(
                name: "route_stops");

            migrationBuilder.DropTable(
                name: "routes");

            migrationBuilder.DropTable(
                name: "locations");

            migrationBuilder.DropIndex(
                name: "ix_bills_tenant_id_destination_location_id",
                table: "bills");

            migrationBuilder.DropIndex(
                name: "ix_bills_tenant_id_origin_location_id",
                table: "bills");

            migrationBuilder.DropIndex(
                name: "ix_bills_tenant_id_route_id",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "bill_party_policy_json",
                table: "tenant_settings");

            migrationBuilder.DropColumn(
                name: "destination_location_id",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "origin_location_id",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "route_id",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "bill_to_party_id",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "consignee_party_id",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "destination_location_id",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "origin_location_id",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "payer_party_id",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "route_id",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "shipper_party_id",
                table: "bills");
        }
    }
}
