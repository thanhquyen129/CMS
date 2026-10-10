using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SurchargeVersionEconomicChargeType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "economic_charge_type_id",
                table: "surcharge_versions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "economic_charge_type_id",
                table: "pricing_rule_components",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_surcharge_versions_tenant_id_economic_charge_type_id",
                table: "surcharge_versions",
                columns: new[] { "tenant_id", "economic_charge_type_id" });

            migrationBuilder.CreateIndex(
                name: "ix_pricing_rule_components_tenant_id_economic_charge_type_id",
                table: "pricing_rule_components",
                columns: new[] { "tenant_id", "economic_charge_type_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_surcharge_versions_tenant_id_economic_charge_type_id",
                table: "surcharge_versions");

            migrationBuilder.DropIndex(
                name: "ix_pricing_rule_components_tenant_id_economic_charge_type_id",
                table: "pricing_rule_components");

            migrationBuilder.DropColumn(
                name: "economic_charge_type_id",
                table: "surcharge_versions");

            migrationBuilder.DropColumn(
                name: "economic_charge_type_id",
                table: "pricing_rule_components");
        }
    }
}
