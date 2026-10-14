using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RateCardDeclarationMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "declaration_mode",
                table: "rate_cards",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "route_id",
                table: "rate_cards",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_rate_cards_route_id",
                table: "rate_cards",
                column: "route_id");

            migrationBuilder.AddForeignKey(
                name: "fk_rate_cards_routes_route_id",
                table: "rate_cards",
                column: "route_id",
                principalTable: "routes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(
                """
                UPDATE rate_cards AS c
                SET declaration_mode = inferred.mode
                FROM (
                    SELECT v.rate_card_id,
                        CASE
                            WHEN bool_or(lower(r.calc_method) = 'weight_break_pivot')
                             AND bool_or(lower(r.calc_method) <> 'weight_break_pivot') THEN NULL
                            WHEN bool_or(lower(r.calc_method) = 'weight_break_pivot') THEN 'advanced'
                            WHEN bool_or(lower(r.calc_method) <> 'weight_break_pivot') THEN 'basic'
                            ELSE NULL
                        END AS mode
                    FROM rate_versions v
                    JOIN pricing_rules r ON r.rate_version_id = v.id AND r.deleted_at IS NULL
                    WHERE v.deleted_at IS NULL
                    GROUP BY v.rate_card_id
                ) AS inferred
                WHERE c.id = inferred.rate_card_id
                  AND c.deleted_at IS NULL
                  AND c.declaration_mode IS NULL
                  AND inferred.mode IS NOT NULL;

                UPDATE rate_cards AS c
                SET route_id = r.id
                FROM routes AS r
                WHERE c.route_id IS NULL
                  AND c.deleted_at IS NULL
                  AND c.route_code IS NOT NULL
                  AND btrim(c.route_code) <> ''
                  AND r.deleted_at IS NULL
                  AND r.tenant_id = c.tenant_id
                  AND upper(r.code) = upper(c.route_code);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_rate_cards_routes_route_id",
                table: "rate_cards");

            migrationBuilder.DropIndex(
                name: "ix_rate_cards_route_id",
                table: "rate_cards");

            migrationBuilder.DropColumn(
                name: "declaration_mode",
                table: "rate_cards");

            migrationBuilder.DropColumn(
                name: "route_id",
                table: "rate_cards");
        }
    }
}
