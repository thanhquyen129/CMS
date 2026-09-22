using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllocationBasesScopeRounding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "condition_code",
                table: "cost_allocations",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "scope_id",
                table: "cost_allocations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "override_before_amount",
                table: "cost_allocation_details",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "condition_code",
                table: "cost_allocations");

            migrationBuilder.DropColumn(
                name: "scope_id",
                table: "cost_allocations");

            migrationBuilder.DropColumn(
                name: "override_before_amount",
                table: "cost_allocation_details");
        }
    }
}
