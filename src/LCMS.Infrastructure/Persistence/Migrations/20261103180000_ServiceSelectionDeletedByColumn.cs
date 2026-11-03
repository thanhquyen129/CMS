using LCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LCMS.Infrastructure.Persistence.Migrations;

/// <summary>
/// Hotfix: 20261102170000 created service_selections / audits without deleted_by,
/// but EntityBase + soft-delete filter always SELECT deleted_by → Postgres 42703 → HTTP 500 on CreateRating.
/// </summary>
[DbContext(typeof(LcmsDbContext))]
[Migration("20261103180000_ServiceSelectionDeletedByColumn")]
public class ServiceSelectionDeletedByColumn : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Idempotent: prod may already have received emergency ALTER before this migration ships.
        if (string.Equals(
                migrationBuilder.ActiveProvider,
                "Microsoft.EntityFrameworkCore.Sqlite",
                StringComparison.Ordinal))
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE service_selections ADD COLUMN deleted_by TEXT NULL;
                """);
            migrationBuilder.Sql(
                """
                ALTER TABLE service_selection_audits ADD COLUMN deleted_by TEXT NULL;
                """);
            return;
        }

        migrationBuilder.Sql(
            """
            ALTER TABLE service_selections ADD COLUMN IF NOT EXISTS deleted_by uuid NULL;
            ALTER TABLE service_selection_audits ADD COLUMN IF NOT EXISTS deleted_by uuid NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        if (string.Equals(
                migrationBuilder.ActiveProvider,
                "Microsoft.EntityFrameworkCore.Sqlite",
                StringComparison.Ordinal))
        {
            return; // SQLite cannot DROP COLUMN reliably across versions used in tests
        }

        migrationBuilder.Sql(
            """
            ALTER TABLE service_selections DROP COLUMN IF EXISTS deleted_by;
            ALTER TABLE service_selection_audits DROP COLUMN IF EXISTS deleted_by;
            """);
    }
}
