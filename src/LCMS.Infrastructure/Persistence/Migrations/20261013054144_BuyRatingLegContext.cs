using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BuyRatingLegContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "context_type",
                table: "ratings",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "bill");

            migrationBuilder.AddColumn<Guid>(
                name: "pricing_rule_id",
                table: "ratings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "rate_card_id",
                table: "ratings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "shipment_id",
                table: "ratings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "transport_leg_id",
                table: "ratings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "vendor_party_id",
                table: "ratings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "origin_level",
                table: "costs",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "pricing_rule_id",
                table: "costs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "rate_card_id",
                table: "costs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "rate_version_id",
                table: "costs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "shipment_id",
                table: "costs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "transport_leg_id",
                table: "costs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_ratings_tenant_id_transport_leg_id_rate_version_id_vendor_p",
                table: "ratings",
                columns: new[] { "tenant_id", "transport_leg_id", "rate_version_id", "vendor_party_id" });

            migrationBuilder.CreateIndex(
                name: "ix_costs_tenant_id_transport_leg_id",
                table: "costs",
                columns: new[] { "tenant_id", "transport_leg_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_ratings_tenant_id_transport_leg_id_rate_version_id_vendor_p",
                table: "ratings");

            migrationBuilder.DropIndex(
                name: "ix_costs_tenant_id_transport_leg_id",
                table: "costs");

            migrationBuilder.DropColumn(
                name: "context_type",
                table: "ratings");

            migrationBuilder.DropColumn(
                name: "pricing_rule_id",
                table: "ratings");

            migrationBuilder.DropColumn(
                name: "rate_card_id",
                table: "ratings");

            migrationBuilder.DropColumn(
                name: "shipment_id",
                table: "ratings");

            migrationBuilder.DropColumn(
                name: "transport_leg_id",
                table: "ratings");

            migrationBuilder.DropColumn(
                name: "vendor_party_id",
                table: "ratings");

            migrationBuilder.DropColumn(
                name: "origin_level",
                table: "costs");

            migrationBuilder.DropColumn(
                name: "pricing_rule_id",
                table: "costs");

            migrationBuilder.DropColumn(
                name: "rate_card_id",
                table: "costs");

            migrationBuilder.DropColumn(
                name: "rate_version_id",
                table: "costs");

            migrationBuilder.DropColumn(
                name: "shipment_id",
                table: "costs");

            migrationBuilder.DropColumn(
                name: "transport_leg_id",
                table: "costs");
        }
    }
}
