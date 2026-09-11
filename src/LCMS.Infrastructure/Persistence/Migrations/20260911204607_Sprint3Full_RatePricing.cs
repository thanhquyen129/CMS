using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint3Full_RatePricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "base_amount",
                table: "ratings",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "party_type_code",
                table: "ratings",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "route_code",
                table: "ratings",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "service_type_code",
                table: "ratings",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "supersedes_rating_id",
                table: "ratings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "weight",
                table: "ratings",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "max_amount",
                table: "pricing_rules",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "min_amount",
                table: "pricing_rules",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "party_type_code",
                table: "pricing_rules",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "route_code",
                table: "pricing_rules",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "service_type_code",
                table: "pricing_rules",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_ratings_supersedes_rating_id",
                table: "ratings",
                column: "supersedes_rating_id");

            migrationBuilder.CreateIndex(
                name: "ix_ratings_tenant_id_supersedes_rating_id",
                table: "ratings",
                columns: new[] { "tenant_id", "supersedes_rating_id" });

            migrationBuilder.CreateIndex(
                name: "ix_pricing_rules_tenant_id_rate_version_id_service_type_code",
                table: "pricing_rules",
                columns: new[] { "tenant_id", "rate_version_id", "service_type_code" });

            migrationBuilder.AddForeignKey(
                name: "fk_ratings_ratings_supersedes_rating_id",
                table: "ratings",
                column: "supersedes_rating_id",
                principalTable: "ratings",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_ratings_ratings_supersedes_rating_id",
                table: "ratings");

            migrationBuilder.DropIndex(
                name: "ix_ratings_supersedes_rating_id",
                table: "ratings");

            migrationBuilder.DropIndex(
                name: "ix_ratings_tenant_id_supersedes_rating_id",
                table: "ratings");

            migrationBuilder.DropIndex(
                name: "ix_pricing_rules_tenant_id_rate_version_id_service_type_code",
                table: "pricing_rules");

            migrationBuilder.DropColumn(
                name: "base_amount",
                table: "ratings");

            migrationBuilder.DropColumn(
                name: "party_type_code",
                table: "ratings");

            migrationBuilder.DropColumn(
                name: "route_code",
                table: "ratings");

            migrationBuilder.DropColumn(
                name: "service_type_code",
                table: "ratings");

            migrationBuilder.DropColumn(
                name: "supersedes_rating_id",
                table: "ratings");

            migrationBuilder.DropColumn(
                name: "weight",
                table: "ratings");

            migrationBuilder.DropColumn(
                name: "max_amount",
                table: "pricing_rules");

            migrationBuilder.DropColumn(
                name: "min_amount",
                table: "pricing_rules");

            migrationBuilder.DropColumn(
                name: "party_type_code",
                table: "pricing_rules");

            migrationBuilder.DropColumn(
                name: "route_code",
                table: "pricing_rules");

            migrationBuilder.DropColumn(
                name: "service_type_code",
                table: "pricing_rules");
        }
    }
}
