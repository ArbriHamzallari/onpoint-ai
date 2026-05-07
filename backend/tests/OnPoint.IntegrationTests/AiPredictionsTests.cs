using FluentAssertions;
using Npgsql;
using OnPoint.IntegrationTests.Infrastructure;
using Xunit;

namespace OnPoint.IntegrationTests;

[Collection("postgres")]
public class AiPredictionsTests
{
    private readonly PostgresFixture _fx;

    public AiPredictionsTests(PostgresFixture fx) => _fx = fx;

    [Fact]
    public async Task Schema_HasAiPredictionsAndModelVersionsTables()
    {
        await using var conn = _fx.OpenConnection();

        var tables = await ScalarListAsync<string>(conn,
            """
            SELECT table_name
              FROM information_schema.tables
             WHERE table_schema = 'public'
               AND table_name IN ('ai_predictions', 'model_versions')
            """);

        tables.Should().BeEquivalentTo(new[] { "ai_predictions", "model_versions" });
    }

    [Fact]
    public async Task Insert_Roundtrip_PreservesEnumsJsonbAndObservabilityFields()
    {
        await using var conn = _fx.OpenConnection();
        var businessId = await SeedBusinessAsync(conn, $"rt-{Guid.NewGuid():N}");

        // Set tenant context BEFORE seeding the session — feedback_sessions is RLS-protected.
        await SetTenantAsync(conn, businessId);

        // CHECK constraint on ai_predictions requires ≥1 of issue_id / feedback_id / session_id.
        // Use session_id (simpler than seeding the full feedback chain).
        var sessionId = await SeedSessionAsync(conn, businessId);

        await using (var insert = new NpgsqlCommand(
            """
            INSERT INTO ai_predictions
                (business_id, session_id, stage, input_hash, output_json,
                 model_version, provider, confidence, latency_ms,
                 prompt_text, response_text, contains_pii, ai_fallback)
            VALUES
                (@biz, @sid, 'sentiment'::ai_stage, 'hash-rt',
                 '{"sentiment":"negative","urgency":0.82}'::jsonb,
                 'sentiment-classifier@1.0.0', 'openai'::ai_provider,
                 0.91, 47, 'rendered prompt', '{"raw":"model said this"}', TRUE, FALSE)
            """, conn))
        {
            insert.Parameters.AddWithValue("biz", businessId);
            insert.Parameters.AddWithValue("sid", sessionId);
            await insert.ExecuteNonQueryAsync();
        }

        await using var read = new NpgsqlCommand(
            """
            SELECT stage::text, provider::text,
                   output_json->>'sentiment' AS sentiment,
                   output_json->>'urgency'   AS urgency,
                   confidence, latency_ms, contains_pii, ai_fallback,
                   prompt_text, response_text
              FROM ai_predictions
             WHERE business_id = @biz AND input_hash = 'hash-rt'
            """, conn);
        read.Parameters.AddWithValue("biz", businessId);

        await using var reader = await read.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();
        reader.GetString(0).Should().Be("sentiment");
        reader.GetString(1).Should().Be("openai");
        reader.GetString(2).Should().Be("negative");
        reader.GetString(3).Should().Be("0.82");
        reader.GetDecimal(4).Should().Be(0.91m);
        reader.GetInt32(5).Should().Be(47);
        reader.GetBoolean(6).Should().BeTrue();
        reader.GetBoolean(7).Should().BeFalse();
        reader.GetString(8).Should().Be("rendered prompt");
        reader.GetString(9).Should().Be("{\"raw\":\"model said this\"}");
    }

    [Fact]
    public async Task Rls_IsolatesTenants_AndPlatformAdminBypasses()
    {
        await using var conn = _fx.OpenConnection();
        var bizA = await SeedBusinessAsync(conn, $"rls-a-{Guid.NewGuid():N}");
        var bizB = await SeedBusinessAsync(conn, $"rls-b-{Guid.NewGuid():N}");

        // Insert as tenant A — set context before any RLS-protected insert
        await SetTenantAsync(conn, bizA);
        var sessionA = await SeedSessionAsync(conn, bizA);

        await using (var insert = new NpgsqlCommand(
            """
            INSERT INTO ai_predictions
                (business_id, session_id, stage, input_hash, output_json,
                 model_version, provider, latency_ms)
            VALUES
                (@biz, @sid, 'classifier'::ai_stage, 'rls-hash',
                 '{"category":"AC"}'::jsonb,
                 'classifier@1.0.0', 'anthropic'::ai_provider, 12)
            """, conn))
        {
            insert.Parameters.AddWithValue("biz", bizA);
            insert.Parameters.AddWithValue("sid", sessionA);
            await insert.ExecuteNonQueryAsync();
        }

        // Switch to tenant B — must not see tenant A's row
        await SetTenantAsync(conn, bizB);

        var visibleAsB = await ScalarAsync<long>(conn,
            "SELECT COUNT(*) FROM ai_predictions WHERE input_hash = 'rls-hash'");
        visibleAsB.Should().Be(0, "tenant B must not see tenant A's predictions");

        // Try to insert into tenant A's space while acting as tenant B — must fail
        await using (var crossInsert = new NpgsqlCommand(
            """
            INSERT INTO ai_predictions
                (business_id, session_id, stage, input_hash, output_json,
                 model_version, provider, latency_ms)
            VALUES
                (@biz, @sid, 'classifier'::ai_stage, 'cross-tenant',
                 '{}'::jsonb, 'classifier@1.0.0', 'openai'::ai_provider, 1)
            """, conn))
        {
            crossInsert.Parameters.AddWithValue("biz", bizA);   // wrong tenant
            crossInsert.Parameters.AddWithValue("sid", sessionA);
            var act = async () => await crossInsert.ExecuteNonQueryAsync();
            await act.Should().ThrowAsync<PostgresException>()
                .Where(e => e.SqlState == "42501",
                    "RLS WITH CHECK should reject cross-tenant inserts");
        }

        // Platform admin bypass — sees all rows
        await using (var setAdmin = new NpgsqlCommand(
            "SET app.is_platform_admin = 'true';", conn))
            await setAdmin.ExecuteNonQueryAsync();

        var visibleAsAdmin = await ScalarAsync<long>(conn,
            "SELECT COUNT(*) FROM ai_predictions WHERE input_hash = 'rls-hash'");
        visibleAsAdmin.Should().Be(1, "platform admin must see all tenants");
    }

    // ──────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────

    // SeedBusinessAsync inserts into the businesses table (NOT under RLS, so
    // safe to call before SetTenantAsync). All RLS-protected inserts must
    // come after the tenant context is set.
    private static async Task<Guid> SeedBusinessAsync(NpgsqlConnection conn, string slug)
    {
        await using var cmd = new NpgsqlCommand(
            "INSERT INTO businesses (slug, name) VALUES (@slug, @name) RETURNING id", conn);
        cmd.Parameters.AddWithValue("slug", slug);
        cmd.Parameters.AddWithValue("name", $"Test Biz {slug}");
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task<Guid> SeedSessionAsync(NpgsqlConnection conn, Guid businessId)
    {
        await using var cmd = new NpgsqlCommand(
            "INSERT INTO feedback_sessions (business_id) VALUES (@biz) RETURNING id", conn);
        cmd.Parameters.AddWithValue("biz", businessId);
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task SetTenantAsync(NpgsqlConnection conn, Guid businessId)
    {
        await using var cmd = new NpgsqlCommand(
            $"SET app.current_business_id = '{businessId}'; SET app.is_platform_admin = 'false';",
            conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<T> ScalarAsync<T>(NpgsqlConnection conn, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        var result = await cmd.ExecuteScalarAsync();
        return (T)result!;
    }

    private static async Task<List<T>> ScalarListAsync<T>(NpgsqlConnection conn, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        var list = new List<T>();
        while (await reader.ReadAsync()) list.Add((T)reader.GetValue(0));
        return list;
    }
}
