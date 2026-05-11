using FluentAssertions;
using Npgsql;
using OnPoint.IntegrationTests.Infrastructure;
using Xunit;

namespace OnPoint.IntegrationTests;

/// <summary>
/// Verifies migration 0003 correctly adds the four AI enrichment columns to
/// the issues table with the right defaults and constraints.
/// </summary>
[Collection("postgres")]
public class IssueAiFieldsTests
{
    private readonly PostgresFixture _fx;

    public IssueAiFieldsTests(PostgresFixture fx) => _fx = fx;

    // ── Schema tests ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Migration0003_AddsAllFourAiColumnsToIssues()
    {
        await using var conn = _fx.OpenConnection();

        var cols = await ScalarListAsync<string>(conn, """
            SELECT column_name
              FROM information_schema.columns
             WHERE table_schema = 'public'
               AND table_name   = 'issues'
               AND column_name  IN (
                       'ai_category',
                       'ai_category_confidence',
                       'ai_priority_score',
                       'ai_fallback')
             ORDER BY column_name
            """);

        cols.Should().BeEquivalentTo(
            new[] { "ai_category", "ai_category_confidence", "ai_fallback", "ai_priority_score" },
            "all four AI columns must be present after migration 0003");
    }

    [Fact]
    public async Task AiFallback_DefaultsToFalseOnNewIssue()
    {
        await using var conn = _fx.OpenConnection();
        var bizId     = await SeedBusinessAsync(conn, $"ai-df-{Guid.NewGuid():N}");
        await SetTenantAsync(conn, bizId);
        var sessionId  = await SeedSessionAsync(conn, bizId);
        var feedbackId = await SeedFeedbackAsync(conn, bizId, sessionId);

        await using var insert = new NpgsqlCommand("""
            INSERT INTO issues (business_id, feedback_id, session_id,
                                title, status, priority)
            VALUES (@biz, @fb, @sid,
                    'Default ai_fallback test',
                    'open'::issue_status, 'medium'::issue_priority)
            RETURNING id
            """, conn);
        insert.Parameters.AddWithValue("biz", bizId);
        insert.Parameters.AddWithValue("fb",  feedbackId);
        insert.Parameters.AddWithValue("sid", sessionId);
        var issueId = (Guid)(await insert.ExecuteScalarAsync())!;

        await using var chk = new NpgsqlCommand(
            "SELECT ai_fallback FROM issues WHERE id = @id", conn);
        chk.Parameters.AddWithValue("id", issueId);
        var fallback = (bool)(await chk.ExecuteScalarAsync())!;

        fallback.Should().BeFalse("ai_fallback defaults to false per migration 0003");
    }

    // ── Round-trip tests ───────────────────────────────────────────────────────

    [Fact]
    public async Task AiFields_CanBeSetAndReadBack()
    {
        await using var conn = _fx.OpenConnection();
        var bizId     = await SeedBusinessAsync(conn, $"ai-rt-{Guid.NewGuid():N}");
        await SetTenantAsync(conn, bizId);
        var sessionId  = await SeedSessionAsync(conn, bizId);
        var feedbackId = await SeedFeedbackAsync(conn, bizId, sessionId);

        // Insert issue
        await using var ins = new NpgsqlCommand("""
            INSERT INTO issues (business_id, feedback_id, session_id,
                                title, status, priority)
            VALUES (@biz, @fb, @sid,
                    'AI fields round-trip',
                    'open'::issue_status, 'medium'::issue_priority)
            RETURNING id
            """, conn);
        ins.Parameters.AddWithValue("biz", bizId);
        ins.Parameters.AddWithValue("fb",  feedbackId);
        ins.Parameters.AddWithValue("sid", sessionId);
        var issueId = (Guid)(await ins.ExecuteScalarAsync())!;

        // Simulate what the orchestrator writes
        await using var upd = new NpgsqlCommand("""
            UPDATE issues
               SET ai_category            = 'hvac',
                   ai_category_confidence = 0.9125,
                   ai_priority_score      = 75,
                   ai_fallback            = FALSE,
                   status                 = 'assigned'::issue_status,
                   priority               = 'high'::issue_priority
             WHERE id = @id
            """, conn);
        upd.Parameters.AddWithValue("id", issueId);
        await upd.ExecuteNonQueryAsync();

        // Read back
        await using var read = new NpgsqlCommand("""
            SELECT ai_category, ai_category_confidence, ai_priority_score,
                   ai_fallback, status::text, priority::text
              FROM issues WHERE id = @id
            """, conn);
        read.Parameters.AddWithValue("id", issueId);

        await using var rdr = await read.ExecuteReaderAsync();
        (await rdr.ReadAsync()).Should().BeTrue();

        rdr.GetString(0).Should().Be("hvac");
        rdr.GetDecimal(1).Should().Be(0.9125m);
        rdr.GetInt32(2).Should().Be(75);
        rdr.GetBoolean(3).Should().BeFalse();
        rdr.GetString(4).Should().Be("assigned");
        rdr.GetString(5).Should().Be("high");
    }

    [Fact]
    public async Task AiCategoryConfidence_CheckConstraintRejectsOutOfRange()
    {
        await using var conn = _fx.OpenConnection();
        var bizId     = await SeedBusinessAsync(conn, $"ai-chk-{Guid.NewGuid():N}");
        await SetTenantAsync(conn, bizId);
        var sessionId  = await SeedSessionAsync(conn, bizId);
        var feedbackId = await SeedFeedbackAsync(conn, bizId, sessionId);

        await using var ins = new NpgsqlCommand("""
            INSERT INTO issues (business_id, feedback_id, session_id,
                                title, status, priority)
            VALUES (@biz, @fb, @sid,
                    'Check constraint test',
                    'open'::issue_status, 'low'::issue_priority)
            RETURNING id
            """, conn);
        ins.Parameters.AddWithValue("biz", bizId);
        ins.Parameters.AddWithValue("fb",  feedbackId);
        ins.Parameters.AddWithValue("sid", sessionId);
        var issueId = (Guid)(await ins.ExecuteScalarAsync())!;

        await using var badUpd = new NpgsqlCommand("""
            UPDATE issues SET ai_category_confidence = 1.5 WHERE id = @id
            """, conn);
        badUpd.Parameters.AddWithValue("id", issueId);

        var act = async () => await badUpd.ExecuteNonQueryAsync();
        await act.Should()
            .ThrowAsync<Npgsql.PostgresException>()
            .Where(e => e.SqlState == "23514",
                "CHECK constraint must reject ai_category_confidence > 1");
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static async Task<Guid> SeedBusinessAsync(NpgsqlConnection conn, string slug)
    {
        await using var cmd = new NpgsqlCommand(
            "INSERT INTO businesses (slug, name) VALUES (@s, @n) RETURNING id", conn);
        cmd.Parameters.AddWithValue("s", slug);
        cmd.Parameters.AddWithValue("n", $"AI Test {slug}");
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task<Guid> SeedSessionAsync(NpgsqlConnection conn, Guid businessId)
    {
        await using var cmd = new NpgsqlCommand(
            "INSERT INTO feedback_sessions (business_id) VALUES (@b) RETURNING id", conn);
        cmd.Parameters.AddWithValue("b", businessId);
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task<Guid> SeedFeedbackAsync(
        NpgsqlConnection conn, Guid businessId, Guid sessionId)
    {
        await using var cmd = new NpgsqlCommand("""
            INSERT INTO feedback (business_id, session_id, rating)
            VALUES (@b, @s, 2) RETURNING id
            """, conn);
        cmd.Parameters.AddWithValue("b", businessId);
        cmd.Parameters.AddWithValue("s", sessionId);
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task SetTenantAsync(NpgsqlConnection conn, Guid businessId)
    {
        await using var cmd = new NpgsqlCommand(
            $"SET app.current_business_id = '{businessId}'; SET app.is_platform_admin = 'false';",
            conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<List<T>> ScalarListAsync<T>(NpgsqlConnection conn, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var rdr = await cmd.ExecuteReaderAsync();
        var list = new List<T>();
        while (await rdr.ReadAsync()) list.Add((T)rdr.GetValue(0));
        return list;
    }
}
