using System.Net.Sockets;
using LCMS.Application.Abstractions;
using LCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace LCMS.Api.Tests;

/// <summary>Runs only when something accepts TCP on 127.0.0.1:5433. Otherwise the suite stays green.</summary>
public sealed class RequiresPostgres5433Attribute : FactAttribute
{
    public RequiresPostgres5433Attribute()
    {
        try
        {
            using var client = new TcpClient();
            var connect = client.ConnectAsync("127.0.0.1", 5433);
            if (!connect.Wait(TimeSpan.FromSeconds(2)) || !client.Connected)
            {
                Skip = "Requires local Postgres on port 5433";
            }
        }
        catch (Exception)
        {
            Skip = "Requires local Postgres on port 5433";
        }
    }
}

public sealed class RateScopeMigrationPostgresTests
{
    [RequiresPostgres5433]
    public async Task Ac30_LegacyHeaderScopeIsCopiedWithoutDroppingCardsOrRatings()
    {
        const string user = "lcms";
        const string password = "lcms";
        const int port = 5433;
        var admin = $"Host=127.0.0.1;Port={port};Database=postgres;Username={user};Password={password}";
        const string name = "lcms_rate_ac30";
        await using (var adminConn = new NpgsqlConnection(admin))
        {
            await adminConn.OpenAsync();
            await using (var kill = new NpgsqlCommand(
                "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = @name AND pid <> pg_backend_pid()",
                adminConn))
            {
                kill.Parameters.AddWithValue("name", name);
                await kill.ExecuteNonQueryAsync();
            }

            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {name}", adminConn);
            await drop.ExecuteNonQueryAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE {name}", adminConn);
            await create.ExecuteNonQueryAsync();
        }

        var cs = $"Host=127.0.0.1;Port={port};Database={name};Username={user};Password={password}";
        await using (var db = NewContext(cs))
        {
            await db.GetService<IMigrator>()!.MigrateAsync("20261014061119_RateCardDeclarationMode");
        }

        var tenantId = Guid.NewGuid();
        var directedCardId = Guid.NewGuid();
        var anyCardId = Guid.NewGuid();
        var codedCardId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        var originId = Guid.NewGuid();
        var destinationId = Guid.NewGuid();
        var directedVersionId = Guid.NewGuid();
        var anyVersionId = Guid.NewGuid();
        var codedVersionId = Guid.NewGuid();
        var directedRuleId = Guid.NewGuid();
        var anyRuleId = Guid.NewGuid();
        var codedRuleId = Guid.NewGuid();
        var billId = Guid.NewGuid();
        var ratingId = Guid.NewGuid();
        await using (var conn = new NpgsqlConnection(cs))
        {
            await conn.OpenAsync();
            await InsertAsync(conn, "tenants", new Dictionary<string, object?>
            {
                ["id"] = tenantId,
                ["code"] = "AC30",
                ["name"] = "AC30",
                ["is_active"] = true,
                ["time_zone_id"] = "Asia/Ho_Chi_Minh",
                ["date_format"] = "dd/MM/yyyy",
                ["default_currency_code"] = "VND"
            });
            await InsertAsync(conn, "locations", Row(tenantId, originId, ("code", "SGN"), ("name", "SGN"), ("location_type", "airport"), ("is_active", true)));
            await InsertAsync(conn, "locations", Row(tenantId, destinationId, ("code", "HAN"), ("name", "HAN"), ("location_type", "airport"), ("is_active", true)));
            await InsertAsync(conn, "routes", Row(
                tenantId,
                routeId,
                ("code", "SGN-HAN"),
                ("name", "SGN-HAN"),
                ("origin_location_id", originId),
                ("destination_location_id", destinationId),
                ("is_active", true)));
            await InsertCardAsync(conn, tenantId, directedCardId, "RC-DIR", routeId, "SGN-HAN", "VN-AIR");
            await InsertCardAsync(conn, tenantId, anyCardId, "RC-ANY", null, null, null);
            await InsertCardAsync(conn, tenantId, codedCardId, "RC-CODE", null, null, null);
            await InsertVersionAsync(conn, tenantId, directedVersionId, directedCardId);
            await InsertVersionAsync(conn, tenantId, anyVersionId, anyCardId);
            await InsertVersionAsync(conn, tenantId, codedVersionId, codedCardId);
            await InsertRuleAsync(conn, tenantId, directedRuleId, directedVersionId, null);
            await InsertRuleAsync(conn, tenantId, anyRuleId, anyVersionId, null);
            await InsertRuleAsync(conn, tenantId, codedRuleId, codedVersionId, "SGN-HAN");
            await InsertAsync(conn, "bills", Row(tenantId, billId, ("bill_no", "BL-AC30"), ("bill_type", "house"), ("operational_status", "active"), ("is_active", true)));
            await InsertAsync(conn, "ratings", Row(
                tenantId,
                ratingId,
                ("bill_id", billId),
                ("rate_version_id", directedVersionId),
                ("context_type", "bill"),
                ("rated_at", DateTimeOffset.UtcNow),
                ("currency_code", "USD"),
                ("total_amount", 123.45m),
                ("quantity", 1m),
                ("status", "completed")));
        }

        await using (var db = NewContext(cs))
        {
            await db.GetService<IMigrator>()!.MigrateAsync("20261014100816_RateRuleScopeWeightBand");
        }

        await using var check = new NpgsqlConnection(cs);
        await check.OpenAsync();
        Assert.Equal(3, await ScalarAsync<long>(check, "SELECT COUNT(*) FROM rate_cards WHERE deleted_at IS NULL"));
        Assert.Equal(3, await ScalarAsync<long>(check, "SELECT COUNT(*) FROM pricing_rules WHERE deleted_at IS NULL"));
        Assert.Equal(3, await ScalarAsync<long>(check, "SELECT COUNT(*) FROM rate_scope_migration_logs WHERE action = 'card_preserved'"));
        Assert.Equal(1, await ScalarAsync<long>(check, "SELECT COUNT(*) FROM rate_scope_migration_logs WHERE action = 'copy_header_route' AND rate_card_id = @id", directedCardId));
        Assert.Equal(routeId, await ScalarAsync<Guid>(check, "SELECT route_id FROM pricing_rules WHERE id = @id", directedRuleId));
        Assert.False(await ScalarAsync<bool>(check, "SELECT applies_any_route FROM pricing_rules WHERE id = @id", directedRuleId));
        Assert.Equal("VN-AIR", await ScalarAsync<string>(check, "SELECT carrier_name FROM pricing_rules WHERE id = @id", directedRuleId));
        Assert.Equal(1, await ScalarAsync<long>(check, "SELECT COUNT(*) FROM rate_scope_migration_logs WHERE action = 'copy_header_carrier' AND pricing_rule_id = @id", directedRuleId));
        Assert.True(await ScalarAsync<bool>(check, "SELECT applies_any_route FROM pricing_rules WHERE id = @id", anyRuleId));
        Assert.Equal(1, await ScalarAsync<long>(check, "SELECT COUNT(*) FROM rate_scope_migration_logs WHERE action = 'mark_any_route' AND pricing_rule_id = @id", anyRuleId));
        Assert.Equal(routeId, await ScalarAsync<Guid>(check, "SELECT route_id FROM pricing_rules WHERE id = @id", codedRuleId));
        Assert.Equal(1, await ScalarAsync<long>(check, "SELECT COUNT(*) FROM rate_scope_migration_logs WHERE action = 'bind_route_code' AND pricing_rule_id = @id", codedRuleId));
        Assert.Equal(123.45m, await ScalarAsync<decimal>(check, "SELECT total_amount FROM ratings WHERE id = @id", ratingId));
        Assert.Equal(1, await ScalarAsync<long>(check, "SELECT COUNT(*) FROM ratings"));
    }

    [RequiresPostgres5433]
    public async Task Baseline_UpDownUp_KeepsNullChargeCodes_AndHistoricalAmounts()
    {
        const string user = "lcms";
        const string password = "lcms";
        const int port = 5433;
        var admin = $"Host=127.0.0.1;Port={port};Database=postgres;Username={user};Password={password}";
        const string name = "lcms_rate_baseline_audit";
        const string previous = "20261014100816_RateRuleScopeWeightBand";
        await using (var adminConn = new NpgsqlConnection(admin))
        {
            await adminConn.OpenAsync();
            await using (var kill = new NpgsqlCommand(
                "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = @name AND pid <> pg_backend_pid()",
                adminConn))
            {
                kill.Parameters.AddWithValue("name", name);
                await kill.ExecuteNonQueryAsync();
            }

            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {name}", adminConn);
            await drop.ExecuteNonQueryAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE {name}", adminConn);
            await create.ExecuteNonQueryAsync();
        }

        var cs = $"Host=127.0.0.1;Port={port};Database={name};Username={user};Password={password}";
        await using (var db = NewContext(cs))
        {
            await db.GetService<IMigrator>()!.MigrateAsync(previous);
        }

        var tenantId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var billId = Guid.NewGuid();
        var buyId = Guid.NewGuid();
        var sellId = Guid.NewGuid();
        await using (var conn = new NpgsqlConnection(cs))
        {
            await conn.OpenAsync();
            await InsertAsync(conn, "tenants", new Dictionary<string, object?>
            {
                ["id"] = tenantId,
                ["code"] = "BASE",
                ["name"] = "BASE",
                ["is_active"] = true,
                ["time_zone_id"] = "Asia/Ho_Chi_Minh",
                ["date_format"] = "dd/MM/yyyy",
                ["default_currency_code"] = "USD"
            });
            await InsertCardAsync(conn, tenantId, cardId, "NSE", null, null, null);
            await InsertAsync(conn, "rate_versions", Row(
                tenantId,
                versionId,
                ("rate_card_id", cardId),
                ("version_no", 1),
                ("status", "published")));
            for (var i = 1; i <= 5; i++)
            {
                await InsertAsync(conn, "pricing_rules", Row(
                    tenantId,
                    Guid.NewGuid(),
                    ("rate_version_id", versionId),
                    ("code", i <= 2 ? $"NSE-REMOTE-{i}" : $"NSE-DELIVERY-{i}"),
                    ("name", i <= 2 ? "REMOTE" : "DELIVERY"),
                    ("calc_method", "fixed"),
                    ("unit_amount", 10m * i),
                    ("currency_code", "USD"),
                    ("route_code", "SGN-FRA"),
                    ("sort_order", i),
                    ("is_active", true)));
            }

            await InsertAsync(conn, "bills", Row(
                tenantId,
                billId,
                ("bill_no", "BILL26107473"),
                ("bill_type", "house"),
                ("operational_status", "active"),
                ("is_active", true)));
            await InsertRatingAsync(conn, tenantId, buyId, billId, versionId, 350m);
            await InsertRatingAsync(conn, tenantId, sellId, billId, versionId, 500m);
        }

        await using (var db = NewContext(cs))
        {
            await db.GetService<IMigrator>()!.MigrateAsync();
        }

        await using var afterUp = new NpgsqlConnection(cs);
        await afterUp.OpenAsync();
        Assert.Equal(350m, await ScalarAsync<decimal>(afterUp, "SELECT total_amount FROM ratings WHERE id = @id", buyId));
        Assert.Equal(500m, await ScalarAsync<decimal>(afterUp, "SELECT total_amount FROM ratings WHERE id = @id", sellId));
        Assert.Equal(5, await ScalarAsync<long>(afterUp, "SELECT COUNT(*) FROM pricing_rules WHERE charge_code IS NULL AND deleted_at IS NULL"));
        Assert.Equal(0, await ScalarAsync<long>(afterUp, "SELECT COUNT(*) FROM rate_versions WHERE explicit_charge_groups = true"));

        await using (var db = NewContext(cs))
        {
            await db.GetService<IMigrator>()!.MigrateAsync(previous);
        }

        await using var afterDown = new NpgsqlConnection(cs);
        await afterDown.OpenAsync();
        Assert.Equal(0, await ScalarAsync<long>(afterDown, "SELECT COUNT(*) FROM information_schema.columns WHERE table_name = 'rate_versions' AND column_name = 'explicit_charge_groups'"));
        Assert.Equal(350m, await ScalarAsync<decimal>(afterDown, "SELECT total_amount FROM ratings WHERE id = @id", buyId));
        Assert.Equal(500m, await ScalarAsync<decimal>(afterDown, "SELECT total_amount FROM ratings WHERE id = @id", sellId));

        await using (var db = NewContext(cs))
        {
            await db.GetService<IMigrator>()!.MigrateAsync();
        }

        await using var afterSecondUp = new NpgsqlConnection(cs);
        await afterSecondUp.OpenAsync();
        Assert.Equal(350m, await ScalarAsync<decimal>(afterSecondUp, "SELECT total_amount FROM ratings WHERE id = @id", buyId));
        Assert.Equal(500m, await ScalarAsync<decimal>(afterSecondUp, "SELECT total_amount FROM ratings WHERE id = @id", sellId));
        Assert.Equal(5, await ScalarAsync<long>(afterSecondUp, "SELECT COUNT(*) FROM pricing_rules WHERE charge_code IS NULL"));
    }

    private static async Task InsertRatingAsync(NpgsqlConnection conn, Guid tenantId, Guid id, Guid billId, Guid versionId, decimal amount)
    {
        await InsertAsync(conn, "ratings", Row(
            tenantId,
            id,
            ("bill_id", billId),
            ("rate_version_id", versionId),
            ("context_type", "bill"),
            ("rated_at", DateTimeOffset.UtcNow),
            ("currency_code", "USD"),
            ("total_amount", amount),
            ("quantity", 1m),
            ("status", "completed")));
    }

    private static LcmsDbContext NewContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<LcmsDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new LcmsDbContext(options, new NullTenant(), new NullUser());
    }

    private static Dictionary<string, object?> Row(Guid tenantId, Guid id, params (string Name, object? Value)[] values)
    {
        var row = new Dictionary<string, object?>
        {
            ["id"] = id,
            ["tenant_id"] = tenantId
        };
        foreach (var (name, value) in values)
        {
            row[name] = value;
        }

        return row;
    }

    private static async Task InsertCardAsync(
        NpgsqlConnection conn,
        Guid tenantId,
        Guid id,
        string code,
        Guid? routeId,
        string? routeCode,
        string? carrierName)
    {
        await InsertAsync(conn, "rate_cards", Row(
            tenantId,
            id,
            ("code", code),
            ("name", code),
            ("party_type", "vendor"),
            ("currency_code", "USD"),
            ("route_id", routeId),
            ("route_code", routeCode),
            ("carrier_name", carrierName),
            ("is_active", true)));
    }

    private static async Task InsertVersionAsync(NpgsqlConnection conn, Guid tenantId, Guid id, Guid cardId)
    {
        await InsertAsync(conn, "rate_versions", Row(
            tenantId,
            id,
            ("rate_card_id", cardId),
            ("version_no", 1),
            ("status", "draft")));
    }

    private static async Task InsertRuleAsync(NpgsqlConnection conn, Guid tenantId, Guid id, Guid versionId, string? routeCode)
    {
        await InsertAsync(conn, "pricing_rules", Row(
            tenantId,
            id,
            ("rate_version_id", versionId),
            ("code", "FRT"),
            ("name", "Cước"),
            ("calc_method", "fixed"),
            ("unit_amount", 10m),
            ("currency_code", "USD"),
            ("route_code", routeCode),
            ("sort_order", 10),
            ("is_active", true)));
    }

    private static async Task InsertAsync(NpgsqlConnection conn, string table, Dictionary<string, object?> values)
    {
        var columns = await ColumnsAsync(conn, table);
        foreach (var column in columns)
        {
            if (column.Nullable || column.HasDefault || values.ContainsKey(column.Name))
            {
                continue;
            }

            values[column.Name] = column.Type switch
            {
                "uuid" => Guid.NewGuid(),
                "boolean" => false,
                "integer" or "bigint" or "smallint" => 0,
                "numeric" or "double precision" or "real" => 0m,
                "bytea" => Guid.NewGuid().ToByteArray(),
                "date" => DateTime.UtcNow.Date,
                "timestamp with time zone" or "timestamp without time zone" => DateTimeOffset.UtcNow,
                _ => "x"
            };
        }

        var names = values.Keys.ToList();
        var sql = $"INSERT INTO {table} ({string.Join(", ", names)}) VALUES ({string.Join(", ", names.Select((_, i) => $"@p{i}"))})";
        await using var cmd = new NpgsqlCommand(sql, conn);
        for (var i = 0; i < names.Count; i++)
        {
            cmd.Parameters.AddWithValue($"p{i}", values[names[i]] ?? DBNull.Value);
        }

        try
        {
            await cmd.ExecuteNonQueryAsync();
        }
        catch (PostgresException ex)
        {
            throw new InvalidOperationException($"Insert {table} failed: {ex.MessageText}", ex);
        }
    }

    private static async Task<List<ColumnInfo>> ColumnsAsync(NpgsqlConnection conn, string table)
    {
        const string sql = """
            SELECT column_name, is_nullable, column_default, data_type
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = @table
            """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("table", table);
        var rows = new List<ColumnInfo>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(new ColumnInfo(
                reader.GetString(0),
                reader.GetString(1) == "YES",
                !reader.IsDBNull(2),
                reader.GetString(3)));
        }

        return rows;
    }

    private static async Task<T> ScalarAsync<T>(NpgsqlConnection conn, string sql, Guid? id = null)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        if (id is Guid value)
        {
            cmd.Parameters.AddWithValue("id", value);
        }

        var result = await cmd.ExecuteScalarAsync();
        return (T)result!;
    }

    private sealed record ColumnInfo(string Name, bool Nullable, bool HasDefault, string Type);

    private sealed class NullTenant : ITenantContext
    {
        public Guid? TenantId => null;
    }

    private sealed class NullUser : ICurrentUserContext
    {
        public Guid? UserId => null;
    }
}
