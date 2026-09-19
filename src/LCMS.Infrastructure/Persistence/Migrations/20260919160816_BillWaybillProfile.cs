using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BillWaybillProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bill_waybills",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    carrier_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    item_form_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    sender_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    sender_phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    sender_email = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    sender_address = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    sender_customer_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    sender_postal_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    consignee_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    consignee_phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    consignee_email = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    consignee_address = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    consignee_delivery_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    consignee_postal_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    package_kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    contents_description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    contents_quantity = table.Column<int>(type: "integer", nullable: true),
                    declared_value = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    accompanying_docs = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    vat_services_note = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    non_delivery_action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    sender_commit_accepted = table.Column<bool>(type: "boolean", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    parcel_count = table.Column<int>(type: "integer", nullable: false),
                    actual_weight_kg = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    chargeable_weight_kg = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    base_postage = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    vat_postage = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    surcharge = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    cod_fee = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    other_fee = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total_postage_incl_vat = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total_collect = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    grand_total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    postage_payer = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    charge_economic_role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    cod_collect_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    operations_note = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    accepting_office = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    accepted_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    received_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
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
                    table.PrimaryKey("pk_bill_waybills", x => x.id);
                    table.ForeignKey(
                        name: "fk_bill_waybills_bills_bill_id",
                        column: x => x.bill_id,
                        principalTable: "bills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bill_waybills_bill_id",
                table: "bill_waybills",
                column: "bill_id");

            migrationBuilder.CreateIndex(
                name: "ix_bill_waybills_tenant_id_bill_id",
                table: "bill_waybills",
                columns: new[] { "tenant_id", "bill_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bill_waybills_tenant_id_consignee_name",
                table: "bill_waybills",
                columns: new[] { "tenant_id", "consignee_name" });

            migrationBuilder.CreateIndex(
                name: "ix_bill_waybills_tenant_id_sender_name",
                table: "bill_waybills",
                columns: new[] { "tenant_id", "sender_name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bill_waybills");
        }
    }
}
