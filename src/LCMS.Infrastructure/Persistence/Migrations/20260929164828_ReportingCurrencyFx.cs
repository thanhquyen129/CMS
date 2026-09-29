using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReportingCurrencyFx : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "fx_applied_at",
                table: "revenues",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "fx_applied_by",
                table: "revenues",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fx_override_reason",
                table: "revenues",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "fx_rate",
                table: "revenues",
                type: "numeric(18,8)",
                precision: 18,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "fx_rate_date",
                table: "revenues",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fx_source_name",
                table: "revenues",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fx_source_type",
                table: "revenues",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fx_status",
                table: "revenues",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "missing");

            migrationBuilder.AddColumn<string>(
                name: "reporting_currency_code",
                table: "revenues",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "fx_applied_at",
                table: "payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "fx_applied_by",
                table: "payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fx_override_reason",
                table: "payments",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "fx_rate",
                table: "payments",
                type: "numeric(18,8)",
                precision: 18,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "fx_rate_date",
                table: "payments",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fx_source_name",
                table: "payments",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fx_source_type",
                table: "payments",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fx_status",
                table: "payments",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "missing");

            migrationBuilder.AddColumn<string>(
                name: "reporting_currency_code",
                table: "payments",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "fx_difference_amount",
                table: "payment_allocations",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reporting_currency_code",
                table: "payment_allocations",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "settled_reporting_amount",
                table: "payment_allocations",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "fx_applied_at",
                table: "costs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "fx_applied_by",
                table: "costs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fx_override_reason",
                table: "costs",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "fx_rate",
                table: "costs",
                type: "numeric(18,8)",
                precision: 18,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "fx_rate_date",
                table: "costs",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fx_source_name",
                table: "costs",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fx_source_type",
                table: "costs",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fx_status",
                table: "costs",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "missing");

            migrationBuilder.AddColumn<string>(
                name: "reporting_currency_code",
                table: "costs",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "fx_applied_at",
                table: "collections",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "fx_applied_by",
                table: "collections",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fx_override_reason",
                table: "collections",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "fx_rate",
                table: "collections",
                type: "numeric(18,8)",
                precision: 18,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "fx_rate_date",
                table: "collections",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fx_source_name",
                table: "collections",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fx_source_type",
                table: "collections",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fx_status",
                table: "collections",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "missing");

            migrationBuilder.AddColumn<string>(
                name: "reporting_currency_code",
                table: "collections",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "fx_difference_amount",
                table: "collection_allocations",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reporting_currency_code",
                table: "collection_allocations",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "settled_reporting_amount",
                table: "collection_allocations",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "fx_applied_at",
                table: "accounts_receivable",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "fx_applied_by",
                table: "accounts_receivable",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fx_override_reason",
                table: "accounts_receivable",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "fx_rate",
                table: "accounts_receivable",
                type: "numeric(18,8)",
                precision: 18,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "fx_rate_date",
                table: "accounts_receivable",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "fx_rate_id",
                table: "accounts_receivable",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fx_source_name",
                table: "accounts_receivable",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fx_source_type",
                table: "accounts_receivable",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fx_status",
                table: "accounts_receivable",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "missing");

            migrationBuilder.AddColumn<decimal>(
                name: "reporting_amount",
                table: "accounts_receivable",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reporting_currency_code",
                table: "accounts_receivable",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "fx_applied_at",
                table: "accounts_payable",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "fx_applied_by",
                table: "accounts_payable",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fx_override_reason",
                table: "accounts_payable",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "fx_rate",
                table: "accounts_payable",
                type: "numeric(18,8)",
                precision: 18,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "fx_rate_date",
                table: "accounts_payable",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "fx_rate_id",
                table: "accounts_payable",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fx_source_name",
                table: "accounts_payable",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fx_source_type",
                table: "accounts_payable",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fx_status",
                table: "accounts_payable",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "missing");

            migrationBuilder.AddColumn<decimal>(
                name: "reporting_amount",
                table: "accounts_payable",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reporting_currency_code",
                table: "accounts_payable",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.Sql(
                """
                -- MIG-01/02/03: idempotent. Same currency → FX 1. Linked book rate → copy. Else review, never today's rate.
                UPDATE costs c
                SET reporting_currency_code = t.default_currency_code,
                    fx_rate = 1,
                    fx_source_type = 'identity',
                    fx_status = 'converted',
                    base_amount = COALESCE(c.base_amount, c.amount)
                FROM tenants t
                WHERE c.tenant_id = t.id
                  AND c.fx_status = 'missing'
                  AND upper(c.currency_code) = upper(t.default_currency_code);

                UPDATE costs c
                SET reporting_currency_code = t.default_currency_code,
                    fx_rate = f.rate,
                    fx_source_type = CASE WHEN f.source = 'vcb' THEN 'auto_provider' ELSE 'policy' END,
                    fx_source_name = 'Sổ tỷ giá · ' || f.source,
                    fx_rate_date = f.rate_date,
                    fx_status = 'converted',
                    base_amount = ROUND(c.amount * f.rate, 4)
                FROM tenants t, fx_rates f
                WHERE c.tenant_id = t.id
                  AND c.fx_rate_id = f.id
                  AND c.fx_status = 'missing'
                  AND upper(c.currency_code) <> upper(t.default_currency_code);

                UPDATE costs c
                SET reporting_currency_code = t.default_currency_code,
                    fx_status = 'requires_review'
                FROM tenants t
                WHERE c.tenant_id = t.id
                  AND c.fx_status = 'missing'
                  AND upper(c.currency_code) <> upper(t.default_currency_code);

                UPDATE revenues c
                SET reporting_currency_code = t.default_currency_code,
                    fx_rate = 1,
                    fx_source_type = 'identity',
                    fx_status = 'converted',
                    base_amount = COALESCE(c.base_amount, c.amount)
                FROM tenants t
                WHERE c.tenant_id = t.id
                  AND c.fx_status = 'missing'
                  AND upper(c.currency_code) = upper(t.default_currency_code);

                UPDATE revenues c
                SET reporting_currency_code = t.default_currency_code,
                    fx_rate = f.rate,
                    fx_source_type = CASE WHEN f.source = 'vcb' THEN 'auto_provider' ELSE 'policy' END,
                    fx_source_name = 'Sổ tỷ giá · ' || f.source,
                    fx_rate_date = f.rate_date,
                    fx_status = 'converted',
                    base_amount = ROUND(c.amount * f.rate, 4)
                FROM tenants t, fx_rates f
                WHERE c.tenant_id = t.id
                  AND c.fx_rate_id = f.id
                  AND c.fx_status = 'missing'
                  AND upper(c.currency_code) <> upper(t.default_currency_code);

                UPDATE revenues c
                SET reporting_currency_code = t.default_currency_code,
                    fx_status = 'requires_review'
                FROM tenants t
                WHERE c.tenant_id = t.id
                  AND c.fx_status = 'missing'
                  AND upper(c.currency_code) <> upper(t.default_currency_code);

                UPDATE payments c
                SET reporting_currency_code = t.default_currency_code,
                    fx_rate = 1,
                    fx_source_type = 'identity',
                    fx_status = 'converted',
                    base_amount = COALESCE(c.base_amount, c.amount)
                FROM tenants t
                WHERE c.tenant_id = t.id
                  AND c.fx_status = 'missing'
                  AND upper(c.currency_code) = upper(t.default_currency_code);

                UPDATE payments c
                SET reporting_currency_code = t.default_currency_code,
                    fx_rate = f.rate,
                    fx_source_type = CASE WHEN f.source = 'vcb' THEN 'auto_provider' ELSE 'policy' END,
                    fx_source_name = 'Sổ tỷ giá · ' || f.source,
                    fx_rate_date = f.rate_date,
                    fx_status = 'converted',
                    base_amount = ROUND(c.amount * f.rate, 4)
                FROM tenants t, fx_rates f
                WHERE c.tenant_id = t.id
                  AND c.fx_rate_id = f.id
                  AND c.fx_status = 'missing'
                  AND upper(c.currency_code) <> upper(t.default_currency_code);

                UPDATE payments c
                SET reporting_currency_code = t.default_currency_code,
                    fx_status = 'requires_review'
                FROM tenants t
                WHERE c.tenant_id = t.id
                  AND c.fx_status = 'missing'
                  AND upper(c.currency_code) <> upper(t.default_currency_code);

                UPDATE collections c
                SET reporting_currency_code = t.default_currency_code,
                    fx_rate = 1,
                    fx_source_type = 'identity',
                    fx_status = 'converted',
                    base_amount = COALESCE(c.base_amount, c.amount)
                FROM tenants t
                WHERE c.tenant_id = t.id
                  AND c.fx_status = 'missing'
                  AND upper(c.currency_code) = upper(t.default_currency_code);

                UPDATE collections c
                SET reporting_currency_code = t.default_currency_code,
                    fx_rate = f.rate,
                    fx_source_type = CASE WHEN f.source = 'vcb' THEN 'auto_provider' ELSE 'policy' END,
                    fx_source_name = 'Sổ tỷ giá · ' || f.source,
                    fx_rate_date = f.rate_date,
                    fx_status = 'converted',
                    base_amount = ROUND(c.amount * f.rate, 4)
                FROM tenants t, fx_rates f
                WHERE c.tenant_id = t.id
                  AND c.fx_rate_id = f.id
                  AND c.fx_status = 'missing'
                  AND upper(c.currency_code) <> upper(t.default_currency_code);

                UPDATE collections c
                SET reporting_currency_code = t.default_currency_code,
                    fx_status = 'requires_review'
                FROM tenants t
                WHERE c.tenant_id = t.id
                  AND c.fx_status = 'missing'
                  AND upper(c.currency_code) <> upper(t.default_currency_code);

                UPDATE accounts_payable c
                SET reporting_currency_code = t.default_currency_code,
                    fx_rate = 1,
                    fx_source_type = 'identity',
                    fx_status = 'converted',
                    reporting_amount = c.recognized_amount
                FROM tenants t
                WHERE c.tenant_id = t.id
                  AND c.fx_status = 'missing'
                  AND upper(c.currency_code) = upper(t.default_currency_code);

                UPDATE accounts_payable c
                SET reporting_currency_code = t.default_currency_code,
                    fx_status = 'requires_review'
                FROM tenants t
                WHERE c.tenant_id = t.id
                  AND c.fx_status = 'missing'
                  AND upper(c.currency_code) <> upper(t.default_currency_code);

                UPDATE accounts_receivable c
                SET reporting_currency_code = t.default_currency_code,
                    fx_rate = 1,
                    fx_source_type = 'identity',
                    fx_status = 'converted',
                    reporting_amount = c.recognized_amount
                FROM tenants t
                WHERE c.tenant_id = t.id
                  AND c.fx_status = 'missing'
                  AND upper(c.currency_code) = upper(t.default_currency_code);

                UPDATE accounts_receivable c
                SET reporting_currency_code = t.default_currency_code,
                    fx_status = 'requires_review'
                FROM tenants t
                WHERE c.tenant_id = t.id
                  AND c.fx_status = 'missing'
                  AND upper(c.currency_code) <> upper(t.default_currency_code);

                UPDATE payment_allocations a
                SET reporting_currency_code = t.default_currency_code
                FROM tenants t
                WHERE a.tenant_id = t.id
                  AND a.reporting_currency_code IS NULL;

                UPDATE collection_allocations a
                SET reporting_currency_code = t.default_currency_code
                FROM tenants t
                WHERE a.tenant_id = t.id
                  AND a.reporting_currency_code IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "fx_applied_at",
                table: "revenues");

            migrationBuilder.DropColumn(
                name: "fx_applied_by",
                table: "revenues");

            migrationBuilder.DropColumn(
                name: "fx_override_reason",
                table: "revenues");

            migrationBuilder.DropColumn(
                name: "fx_rate",
                table: "revenues");

            migrationBuilder.DropColumn(
                name: "fx_rate_date",
                table: "revenues");

            migrationBuilder.DropColumn(
                name: "fx_source_name",
                table: "revenues");

            migrationBuilder.DropColumn(
                name: "fx_source_type",
                table: "revenues");

            migrationBuilder.DropColumn(
                name: "fx_status",
                table: "revenues");

            migrationBuilder.DropColumn(
                name: "reporting_currency_code",
                table: "revenues");

            migrationBuilder.DropColumn(
                name: "fx_applied_at",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "fx_applied_by",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "fx_override_reason",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "fx_rate",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "fx_rate_date",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "fx_source_name",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "fx_source_type",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "fx_status",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "reporting_currency_code",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "fx_difference_amount",
                table: "payment_allocations");

            migrationBuilder.DropColumn(
                name: "reporting_currency_code",
                table: "payment_allocations");

            migrationBuilder.DropColumn(
                name: "settled_reporting_amount",
                table: "payment_allocations");

            migrationBuilder.DropColumn(
                name: "fx_applied_at",
                table: "costs");

            migrationBuilder.DropColumn(
                name: "fx_applied_by",
                table: "costs");

            migrationBuilder.DropColumn(
                name: "fx_override_reason",
                table: "costs");

            migrationBuilder.DropColumn(
                name: "fx_rate",
                table: "costs");

            migrationBuilder.DropColumn(
                name: "fx_rate_date",
                table: "costs");

            migrationBuilder.DropColumn(
                name: "fx_source_name",
                table: "costs");

            migrationBuilder.DropColumn(
                name: "fx_source_type",
                table: "costs");

            migrationBuilder.DropColumn(
                name: "fx_status",
                table: "costs");

            migrationBuilder.DropColumn(
                name: "reporting_currency_code",
                table: "costs");

            migrationBuilder.DropColumn(
                name: "fx_applied_at",
                table: "collections");

            migrationBuilder.DropColumn(
                name: "fx_applied_by",
                table: "collections");

            migrationBuilder.DropColumn(
                name: "fx_override_reason",
                table: "collections");

            migrationBuilder.DropColumn(
                name: "fx_rate",
                table: "collections");

            migrationBuilder.DropColumn(
                name: "fx_rate_date",
                table: "collections");

            migrationBuilder.DropColumn(
                name: "fx_source_name",
                table: "collections");

            migrationBuilder.DropColumn(
                name: "fx_source_type",
                table: "collections");

            migrationBuilder.DropColumn(
                name: "fx_status",
                table: "collections");

            migrationBuilder.DropColumn(
                name: "reporting_currency_code",
                table: "collections");

            migrationBuilder.DropColumn(
                name: "fx_difference_amount",
                table: "collection_allocations");

            migrationBuilder.DropColumn(
                name: "reporting_currency_code",
                table: "collection_allocations");

            migrationBuilder.DropColumn(
                name: "settled_reporting_amount",
                table: "collection_allocations");

            migrationBuilder.DropColumn(
                name: "fx_applied_at",
                table: "accounts_receivable");

            migrationBuilder.DropColumn(
                name: "fx_applied_by",
                table: "accounts_receivable");

            migrationBuilder.DropColumn(
                name: "fx_override_reason",
                table: "accounts_receivable");

            migrationBuilder.DropColumn(
                name: "fx_rate",
                table: "accounts_receivable");

            migrationBuilder.DropColumn(
                name: "fx_rate_date",
                table: "accounts_receivable");

            migrationBuilder.DropColumn(
                name: "fx_rate_id",
                table: "accounts_receivable");

            migrationBuilder.DropColumn(
                name: "fx_source_name",
                table: "accounts_receivable");

            migrationBuilder.DropColumn(
                name: "fx_source_type",
                table: "accounts_receivable");

            migrationBuilder.DropColumn(
                name: "fx_status",
                table: "accounts_receivable");

            migrationBuilder.DropColumn(
                name: "reporting_amount",
                table: "accounts_receivable");

            migrationBuilder.DropColumn(
                name: "reporting_currency_code",
                table: "accounts_receivable");

            migrationBuilder.DropColumn(
                name: "fx_applied_at",
                table: "accounts_payable");

            migrationBuilder.DropColumn(
                name: "fx_applied_by",
                table: "accounts_payable");

            migrationBuilder.DropColumn(
                name: "fx_override_reason",
                table: "accounts_payable");

            migrationBuilder.DropColumn(
                name: "fx_rate",
                table: "accounts_payable");

            migrationBuilder.DropColumn(
                name: "fx_rate_date",
                table: "accounts_payable");

            migrationBuilder.DropColumn(
                name: "fx_rate_id",
                table: "accounts_payable");

            migrationBuilder.DropColumn(
                name: "fx_source_name",
                table: "accounts_payable");

            migrationBuilder.DropColumn(
                name: "fx_source_type",
                table: "accounts_payable");

            migrationBuilder.DropColumn(
                name: "fx_status",
                table: "accounts_payable");

            migrationBuilder.DropColumn(
                name: "reporting_amount",
                table: "accounts_payable");

            migrationBuilder.DropColumn(
                name: "reporting_currency_code",
                table: "accounts_payable");
        }
    }
}
