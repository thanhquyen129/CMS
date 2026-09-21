using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UI02_OperationalCreateContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "assigned_user_id",
                table: "shipments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "context_json",
                table: "shipments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "customer_reference",
                table: "shipments",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "shipments",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "destination_code",
                table: "shipments",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "eta_at",
                table: "shipments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "etd_at",
                table: "shipments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "origin_code",
                table: "shipments",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "route_code",
                table: "shipments",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "transport_mode",
                table: "shipments",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "assigned_user_id",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "context_json",
                table: "orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "customer_party_id",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "customer_reference",
                table: "orders",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "orders",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "destination_code",
                table: "orders",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "eta_at",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "etd_at",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "origin_code",
                table: "orders",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "route_code",
                table: "orders",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "transport_mode",
                table: "orders",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "context_json",
                table: "bills",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "customer_reference",
                table: "bills",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "destination_code",
                table: "bills",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "origin_code",
                table: "bills",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "transport_mode",
                table: "bills",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_shipments_tenant_id_transport_mode",
                table: "shipments",
                columns: new[] { "tenant_id", "transport_mode" });

            migrationBuilder.CreateIndex(
                name: "ix_orders_tenant_id_customer_party_id",
                table: "orders",
                columns: new[] { "tenant_id", "customer_party_id" });

            migrationBuilder.CreateIndex(
                name: "ix_bills_tenant_id_transport_mode",
                table: "bills",
                columns: new[] { "tenant_id", "transport_mode" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_shipments_tenant_id_transport_mode",
                table: "shipments");

            migrationBuilder.DropIndex(
                name: "ix_orders_tenant_id_customer_party_id",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "ix_bills_tenant_id_transport_mode",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "assigned_user_id",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "context_json",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "customer_reference",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "description",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "destination_code",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "eta_at",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "etd_at",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "origin_code",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "route_code",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "transport_mode",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "assigned_user_id",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "context_json",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "customer_party_id",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "customer_reference",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "description",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "destination_code",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "eta_at",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "etd_at",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "origin_code",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "route_code",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "transport_mode",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "context_json",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "customer_reference",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "destination_code",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "origin_code",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "transport_mode",
                table: "bills");
        }
    }
}
