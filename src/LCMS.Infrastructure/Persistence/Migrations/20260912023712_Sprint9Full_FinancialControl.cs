using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint9Full_FinancialControl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "severity",
                table: "variances",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "low");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "escalated_at",
                table: "exceptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "escalated_by",
                table: "exceptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "escalation_reason",
                table: "exceptions",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "object_id",
                table: "exceptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "object_type",
                table: "exceptions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "current_level",
                table: "approvals",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "required_level",
                table: "approvals",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "ix_variances_tenant_id_severity_status",
                table: "variances",
                columns: new[] { "tenant_id", "severity", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_exceptions_tenant_id_object_type_object_id_status",
                table: "exceptions",
                columns: new[] { "tenant_id", "object_type", "object_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_approvals_tenant_id_required_level_current_level_status",
                table: "approvals",
                columns: new[] { "tenant_id", "required_level", "current_level", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_variances_tenant_id_severity_status",
                table: "variances");

            migrationBuilder.DropIndex(
                name: "ix_exceptions_tenant_id_object_type_object_id_status",
                table: "exceptions");

            migrationBuilder.DropIndex(
                name: "ix_approvals_tenant_id_required_level_current_level_status",
                table: "approvals");

            migrationBuilder.DropColumn(
                name: "severity",
                table: "variances");

            migrationBuilder.DropColumn(
                name: "escalated_at",
                table: "exceptions");

            migrationBuilder.DropColumn(
                name: "escalated_by",
                table: "exceptions");

            migrationBuilder.DropColumn(
                name: "escalation_reason",
                table: "exceptions");

            migrationBuilder.DropColumn(
                name: "object_id",
                table: "exceptions");

            migrationBuilder.DropColumn(
                name: "object_type",
                table: "exceptions");

            migrationBuilder.DropColumn(
                name: "current_level",
                table: "approvals");

            migrationBuilder.DropColumn(
                name: "required_level",
                table: "approvals");
        }
    }
}
