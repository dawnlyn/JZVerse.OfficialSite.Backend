using System.Data;
using Dapper;
using JZVerse.Business.Blog.Arguments;
using JZVerse.Business.Blog.Database;
using JZVerse.Business.Blog.Services;
using JZVerse.DataAccess.Abstractions;
using JZVerse.DataAccess.Abstractions.Models;
using Npgsql;

namespace JZVerse.Business.Tests.Integration;

/// <summary>
/// 文章服务集成测试
/// </summary>
[TestFixture]
public class ArticleServiceIntegrationTests : PostgresTestBase
{
    private ArticleService _articleService = null!;
    private Guid _testCategoryId;

    protected override string GetTableCreationSql() => """
        -- 文章分类表
        CREATE TABLE IF NOT EXISTS categories (
            id UUID PRIMARY KEY,
            name VARCHAR(100) NOT NULL,
            description TEXT,
            icon VARCHAR(50),
            sort_order INT DEFAULT 0,
            article_count INT DEFAULT 0,
            created_at TIMESTAMP NOT NULL DEFAULT NOW(),
            updated_at TIMESTAMP
        );

        -- 文章表
        CREATE TABLE IF NOT EXISTS articles (
            id UUID PRIMARY KEY,
            title VARCHAR(200) NOT NULL,
            summary VARCHAR(500),
            content TEXT,
            cover_image VARCHAR(500),
            category_id UUID REFERENCES categories(id),
            author_id UUID,
            author_name VARCHAR(100),
            status INT DEFAULT 1,
            view_count INT DEFAULT 0,
            like_count INT DEFAULT 0,
            comment_count INT DEFAULT 0,
            is_recommended BOOLEAN DEFAULT FALSE,
            is_top BOOLEAN DEFAULT FALSE,
            published_at TIMESTAMP,
            scheduled_at TIMESTAMP,
            created_at TIMESTAMP NOT NULL DEFAULT NOW(),
            updated_at TIMESTAMP,
            deleted_at TIMESTAMP
        );

        -- 标签表
        CREATE TABLE IF NOT EXISTS tags (
            id UUID PRIMARY KEY,
            name VARCHAR(50) NOT NULL UNIQUE,
            color VARCHAR(20),
            article_count INT DEFAULT 0,
            created_at TIMESTAMP NOT NULL DEFAULT NOW()
        );

        -- 文章标签关联表
        CREATE TABLE IF NOT EXISTS article_tags (
            article_id UUID REFERENCES articles(id),
            tag_id UUID REFERENCES tags(id),
            created_at TIMESTAMP NOT NULL DEFAULT NOW(),
            PRIMARY KEY (article_id, tag_id)
        );
        """;

    [SetUp]
    public async Task SetUp()
    {
        var connectionFactory = new TestConnectionFactory(ConnectionString);
        var dbExecutor = new TestDbExecutor(connectionFactory);
        var idGenerator = new TestSnowflakeIdGenerator();

        _articleService = new ArticleService(dbExecutor, idGenerator);

        // 创建测试分类
        _testCategoryId = Guid.NewGuid();
        await ExecuteSqlAsync($"""
            INSERT INTO categories (id, name, description, created_at)
            VALUES ('{_testCategoryId}', 'Test Category', 'Test Description', NOW())
            ON CONFLICT (id) DO NOTHING
            """);
    }

    [TearDown]
    public async Task TearDown()
    {
        // 清理测试数据
        await ExecuteSqlAsync("DELETE FROM article_tags");
        await ExecuteSqlAsync("DELETE FROM articles");
        await ExecuteSqlAsync("DELETE FROM tags");
    }

    #region Integration Tests

    [Test]
    public async Task SaveAsync_NewArticle_ShouldPersistToDatabase()
    {
        // Arrange
        var authorId = Guid.NewGuid();
        var arg = new ArgSaveArticle
        {
            Title = "Integration Test Article",
            Summary = "Test Summary",
            Content = "Test Content",
            CategoryId = _testCategoryId
        };

        // Act
        var articleId = await _articleService.SaveAsync(arg, authorId, "TestAuthor");

        // Assert
        articleId.Should().NotBe(Guid.Empty);

        var count = await QueryScalarAsync<int>($"SELECT COUNT(*) FROM articles WHERE id = '{articleId}'");
        count.Should().Be(1);
    }

    [Test]
    public async Task SaveAsync_UpdateArticle_ShouldUpdateInDatabase()
    {
        // Arrange
        var authorId = Guid.NewGuid();
        var createArg = new ArgSaveArticle
        {
            Title = "Original Title",
            Summary = "Original Summary",
            Content = "Original Content",
            CategoryId = _testCategoryId
        };

        var articleId = await _articleService.SaveAsync(createArg, authorId, "TestAuthor");

        var updateArg = new ArgSaveArticle
        {
            Id = articleId,
            Title = "Updated Title",
            Summary = "Updated Summary",
            Content = "Updated Content",
            CategoryId = _testCategoryId
        };

        // Act
        var resultId = await _articleService.SaveAsync(updateArg, authorId, "TestAuthor");

        // Assert
        resultId.Should().Be(articleId);

        var title = await QueryScalarAsync<string>($"SELECT title FROM articles WHERE id = '{articleId}'");
        title.Should().Be("Updated Title");
    }

    [Test]
    public async Task GetByIdAsync_ExistingArticle_ShouldReturnFromDatabase()
    {
        // Arrange
        var authorId = Guid.NewGuid();
        var arg = new ArgSaveArticle
        {
            Title = "Test Article for Get",
            Summary = "Test Summary",
            Content = "Test Content",
            CategoryId = _testCategoryId
        };

        var articleId = await _articleService.SaveAsync(arg, authorId, "TestAuthor");

        // Act
        var result = await _articleService.GetByIdAsync(articleId);

        // Assert
        result.Should().NotBeNull();
        result!.Title.Should().Be("Test Article for Get");
        result.CategoryName.Should().Be("Test Category");
    }

    [Test]
    public async Task PublishAsync_DraftArticle_ShouldChangeStatusToPublished()
    {
        // Arrange
        var authorId = Guid.NewGuid();
        var arg = new ArgSaveArticle
        {
            Title = "Draft Article",
            Summary = "Test Summary",
            Content = "Test Content",
            CategoryId = _testCategoryId
        };

        var articleId = await _articleService.SaveAsync(arg, authorId, "TestAuthor");

        // Act
        var result = await _articleService.PublishAsync(articleId);

        // Assert
        result.Should().BeTrue();

        var status = await QueryScalarAsync<int>($"SELECT status FROM articles WHERE id = '{articleId}'");
        status.Should().Be((int)ArticleStatus.Published);
    }

    [Test]
    public async Task DeleteAsync_ExistingArticle_ShouldSoftDelete()
    {
        // Arrange
        var authorId = Guid.NewGuid();
        var arg = new ArgSaveArticle
        {
            Title = "Article to Delete",
            Summary = "Test Summary",
            Content = "Test Content",
            CategoryId = _testCategoryId
        };

        var articleId = await _articleService.SaveAsync(arg, authorId, "TestAuthor");

        // Act
        var result = await _articleService.DeleteAsync(articleId);

        // Assert
        result.Should().BeTrue();

        var deletedAt = await QueryScalarAsync<DateTime?>($"SELECT deleted_at FROM articles WHERE id = '{articleId}'");
        deletedAt.Should().NotBeNull();
    }

    [Test]
    public async Task GetListAdminAsync_WithMultipleArticles_ShouldReturnPaged()
    {
        // Arrange
        var authorId = Guid.NewGuid();

        for (int i = 1; i <= 5; i++)
        {
            var arg = new ArgSaveArticle
            {
                Title = $"Article {i}",
                Summary = "Test Summary",
                Content = "Test Content",
                CategoryId = _testCategoryId
            };
            await _articleService.SaveAsync(arg, authorId, "TestAuthor");
        }

        var queryArg = new ArgQueryArticlesAdmin
        {
            PageIndex = 1,
            PageSize = 3
        };

        // Act
        var result = await _articleService.GetListAdminAsync(queryArg);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(3);
        result.TotalCount.Should().Be(5);
        result.TotalPages.Should().Be(2);
    }

    #endregion
}

#region Test Helpers

/// <summary>
/// 测试用连接工厂
/// </summary>
internal sealed class TestConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public TestConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public DatabaseType DatabaseType => DatabaseType.PostgreSql;
    public IDbDialect Dialect => new TestDbDialect();

    public IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);

    public async Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        return conn;
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// 测试用 SQL 方言
/// </summary>
internal sealed class TestDbDialect : IDbDialect
{
    public DatabaseType DatabaseType => DatabaseType.PostgreSql;
    public string ParameterPrefix => "@";
    public bool SupportsReturning => true;
    
    public string QuoteIdentifier(string identifier) => $"\"{identifier}\"";
    public string BuildPagedQuery(string sql, int offset, int limit) => $"{sql} OFFSET {offset} LIMIT {limit}";
    public string GetLastInsertIdCommand() => "SELECT lastval()";
    public string BuildInsertReturning(string tableName, IEnumerable<string> columns, string returningColumn) =>
        $"INSERT INTO {tableName} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", columns.Select(c => $"@{c}"))}) RETURNING {returningColumn}";
    public string GetCurrentTimestampFunction() => "NOW()";
    public string FormatBoolean(bool value) => value ? "TRUE" : "FALSE";
    public string GetGuidTypeName() => "UUID";
}

/// <summary>
/// 测试用数据库执行器
/// </summary>
internal sealed class TestDbExecutor : IDbExecutor
{
    private readonly IDbConnectionFactory _connectionFactory;

    public TestDbExecutor(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(((TestConnectionFactory)_connectionFactory).CreateConnection().ConnectionString);
        await conn.OpenAsync(cancellationToken);
        return await conn.QueryAsync<T>(sql, param);
    }

    public async Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? param = null, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(((TestConnectionFactory)_connectionFactory).CreateConnection().ConnectionString);
        await conn.OpenAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<T>(sql, param);
    }

    public async Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(((TestConnectionFactory)_connectionFactory).CreateConnection().ConnectionString);
        await conn.OpenAsync(cancellationToken);
        return await conn.QueryFirstOrDefaultAsync<T>(sql, param);
    }

    public async Task<int> ExecuteAsync(string sql, object? param = null, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(((TestConnectionFactory)_connectionFactory).CreateConnection().ConnectionString);
        await conn.OpenAsync(cancellationToken);
        return await conn.ExecuteAsync(sql, param);
    }

    public async Task<T?> ExecuteScalarAsync<T>(string sql, object? param = null, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(((TestConnectionFactory)_connectionFactory).CreateConnection().ConnectionString);
        await conn.OpenAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<T>(sql, param);
    }

    public async Task<T> WithTransactionAsync<T>(Func<IDbTransaction, Task<T>> action, IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(((TestConnectionFactory)_connectionFactory).CreateConnection().ConnectionString);
        await conn.OpenAsync(cancellationToken);
        using var trans = conn.BeginTransaction(isolationLevel);
        try
        {
            var result = await action(trans);
            trans.Commit();
            return result;
        }
        catch
        {
            trans.Rollback();
            throw;
        }
    }

    public async Task WithTransactionAsync(Func<IDbTransaction, Task> action, IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, CancellationToken cancellationToken = default)
    {
        await WithTransactionAsync(async tx =>
        {
            await action(tx);
            return 0;
        }, isolationLevel, cancellationToken);
    }

    public Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param, IDbTransaction transaction, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(transaction.Connection!.Query<T>(sql, param, transaction));
    }

    public Task<int> ExecuteAsync(string sql, object? param, IDbTransaction transaction, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(transaction.Connection!.Execute(sql, param, transaction));
    }

    public Task<T?> ExecuteScalarAsync<T>(string sql, object? param, IDbTransaction transaction, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(transaction.Connection!.ExecuteScalar<T>(sql, param, transaction));
    }
}

/// <summary>
/// 测试用雪花ID生成器
/// </summary>
internal sealed class TestSnowflakeIdGenerator : ISnowflakeIdGenerator
{
    private int _sequence = 0;
    private readonly object _lock = new();

    public int DatacenterId => 1;
    public int WorkerId => 1;

    public SnowflakeId Generate()
    {
        lock (_lock)
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - SnowflakeId.Epoch.ToUnixTimeMilliseconds();
            _sequence = (_sequence + 1) & 0x7FF;
            return new SnowflakeId(timestamp, DatacenterId, WorkerId, _sequence);
        }
    }

    public Guid GenerateGuid() => Generate().ToGuid();

    public IReadOnlyList<SnowflakeId> GenerateBatch(int count)
    {
        var result = new List<SnowflakeId>(count);
        for (int i = 0; i < count; i++)
            result.Add(Generate());
        return result;
    }

    public IReadOnlyList<Guid> GenerateGuidBatch(int count)
    {
        var result = new List<Guid>(count);
        for (int i = 0; i < count; i++)
            result.Add(GenerateGuid());
        return result;
    }

    public SnowflakeId FromGuid(Guid guid) => SnowflakeId.FromGuid(guid);
}

#endregion
