using Npgsql;
using Testcontainers.PostgreSql;

namespace JZVerse.Business.Tests.Integration;

/// <summary>
/// PostgreSQL 测试容器基础类
/// </summary>
public abstract class PostgresTestBase : IAsyncDisposable
{
    protected PostgreSqlContainer PostgresContainer { get; private set; } = null!;
    protected string ConnectionString => PostgresContainer.GetConnectionString();

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        PostgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("test_db")
            .WithUsername("test_user")
            .WithPassword("test_password")
            .Build();

        await PostgresContainer.StartAsync();
        await InitializeDatabaseAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await PostgresContainer.DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await PostgresContainer.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    protected virtual async Task InitializeDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        // 创建测试所需的表结构
        await CreateTablesAsync(connection);
    }

    protected virtual async Task CreateTablesAsync(NpgsqlConnection connection)
    {
        var sql = GetTableCreationSql();
        if (!string.IsNullOrEmpty(sql))
        {
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }
    }

    protected virtual string GetTableCreationSql() => string.Empty;

    protected async Task ExecuteSqlAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    protected async Task<T?> QueryScalarAsync<T>(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        var result = await command.ExecuteScalarAsync();
        return result is DBNull ? default : (T?)result;
    }
}
