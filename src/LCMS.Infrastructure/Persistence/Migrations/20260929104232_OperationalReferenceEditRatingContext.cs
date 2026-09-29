using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OperationalReferenceEditRatingContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "stale_at",
                table: "ratings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "stale_reason",
                table: "ratings",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "operational_field_overrides",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    object_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    object_id = table.Column<Guid>(type: "uuid", nullable: false),
                    field_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    source_value = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    override_value = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    source_channel = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    overridden_by = table.Column<Guid>(type: "uuid", nullable: true),
                    overridden_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("pk_operational_field_overrides", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_operational_field_overrides_tenant_id_object_type_object_id",
                table: "operational_field_overrides",
                columns: new[] { "tenant_id", "object_type", "object_id", "field_code" },
                unique: true,
                filter: "deleted_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "operational_field_overrides");

            migrationBuilder.DropColumn(
                name: "stale_at",
                table: "ratings");

            migrationBuilder.DropColumn(
                name: "stale_reason",
                table: "ratings");
        }
    }
}
