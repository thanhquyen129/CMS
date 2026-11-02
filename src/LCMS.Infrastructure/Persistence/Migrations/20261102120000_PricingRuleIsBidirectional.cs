using LCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LCMS.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(LcmsDbContext))]
    [Migration("20261102120000_PricingRuleIsBidirectional")]
    public class PricingRuleIsBidirectional : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_bidirectional",
                table: "pricing_rules",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_bidirectional",
                table: "pricing_rules");
        }
    }
}
