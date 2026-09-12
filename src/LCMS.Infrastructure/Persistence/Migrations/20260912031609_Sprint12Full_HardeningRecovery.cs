using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint12Full_HardeningRecovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "recovered_at",
                table: "integration_errors",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "recovered_by",
                table: "integration_errors",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "recovery_note",
                table: "integration_errors",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "recovery_status",
                table: "integration_errors",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "pending");

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    topic = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    payload_json = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    attempt_no = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    enqueued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("pk_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_integration_errors_tenant_id_recovery_status_occurred_at",
                table: "integration_errors",
                columns: new[] { "tenant_id", "recovery_status", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_tenant_id_status_enqueued_at",
                table: "outbox_messages",
                columns: new[] { "tenant_id", "status", "enqueued_at" });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_tenant_id_topic_enqueued_at",
                table: "outbox_messages",
                columns: new[] { "tenant_id", "topic", "enqueued_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropIndex(
                name: "ix_integration_errors_tenant_id_recovery_status_occurred_at",
                table: "integration_errors");

            migrationBuilder.DropColumn(
                name: "recovered_at",
                table: "integration_errors");

            migrationBuilder.DropColumn(
                name: "recovered_by",
                table: "integration_errors");

            migrationBuilder.DropColumn(
                name: "recovery_note",
                table: "integration_errors");

            migrationBuilder.DropColumn(
                name: "recovery_status",
                table: "integration_errors");
        }
    }
}
