using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TransportLegBuyInputs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "commodity_type_id",
                table: "transport_legs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "route_code",
                table: "transport_legs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "service_type_code",
                table: "transport_legs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "transport_mode",
                table: "transport_legs",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "commodity_type_id",
                table: "transport_legs");

            migrationBuilder.DropColumn(
                name: "route_code",
                table: "transport_legs");

            migrationBuilder.DropColumn(
                name: "service_type_code",
                table: "transport_legs");

            migrationBuilder.DropColumn(
                name: "transport_mode",
                table: "transport_legs");
        }
    }
}
