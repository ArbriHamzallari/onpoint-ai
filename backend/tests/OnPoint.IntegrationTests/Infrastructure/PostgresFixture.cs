using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace OnPoint.IntegrationTests.Infrastructure;

// Boots a disposable Postgres 16 container, applies every migration in
// database/migrations/ in numeric order, then creates a non-superuser role
// for tests. Superusers bypass RLS unconditionally — tests must run as the
// app role for RLS policies to take effect, matching production runtime.
public sealed class PostgresFixture : IAsyncLifetime
{
    private const string AppRole = "onpoint_app";
    private const string AppPassword = "onpoint_app_pw";

    private readonly PostgreSqlContainer _container;
    private string? _appConnectionString;

    public PostgresFixture()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16")
            .WithDatabase("onpoint_test")
            .WithUsername("onpoint")
            .WithPassword("onpoint_test_pw")
            .Build();
    }

    // Connection string for the non-superuser app role. RLS applies here.
    public string ConnectionString =>
        _appConnectionString ?? throw new InvalidOperationException(
            "PostgresFixture is not initialized — call InitializeAsync first.");

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await ApplyMigrationsAsync();
        await CreateAppRoleAsync();
        _appConnectionString = ComposeAppConnectionString();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    public NpgsqlConnection OpenConnection()
    {
        var conn = new NpgsqlConnection(ConnectionString);
        conn.Open();
        return conn;
    }

    private async Task ApplyMigrationsAsync()
    {
        var migrationsDir = Path.Combine(AppContext.BaseDirectory, "Migrations");
        var files = Directory
            .EnumerateFiles(migrationsDir, "*.sql")
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        if (files.Count == 0)
            throw new InvalidOperationException(
                $"No migration files found in {migrationsDir}. " +
                "Check the <Content Include> glob in OnPoint.IntegrationTests.csproj.");

        await using var conn = new NpgsqlConnection(_container.GetConnectionString());
        await conn.OpenAsync();

        foreach (var file in files)
        {
            var sql = await File.ReadAllTextAsync(file);
            await using var cmd = new NpgsqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private async Task CreateAppRoleAsync()
    {
        await using var conn = new NpgsqlConnection(_container.GetConnectionString());
        await conn.OpenAsync();

        var sql = $"""
            CREATE ROLE {AppRole}
                NOSUPERUSER NOBYPASSRLS LOGIN INHERIT
                PASSWORD '{AppPassword}';

            GRANT USAGE ON SCHEMA public TO {AppRole};
            GRANT ALL ON ALL TABLES    IN SCHEMA public TO {AppRole};
            GRANT ALL ON ALL SEQUENCES IN SCHEMA public TO {AppRole};
            GRANT ALL ON ALL FUNCTIONS IN SCHEMA public TO {AppRole};
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private string ComposeAppConnectionString()
    {
        var builder = new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Username = AppRole,
            Password = AppPassword,
        };
        return builder.ConnectionString;
    }
}

[CollectionDefinition("postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    // Marker class — xUnit reads the [CollectionDefinition] attribute.
}
