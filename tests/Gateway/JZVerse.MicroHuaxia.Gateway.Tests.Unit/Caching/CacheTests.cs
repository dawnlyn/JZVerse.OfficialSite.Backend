using FluentAssertions;
using JZVerse.MicroHuaxia.Gateway.Abstractions.Caching;
using JZVerse.MicroHuaxia.Gateway.Abstractions.Routing;
using JZVerse.MicroHuaxia.Gateway.Core.Caching;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace JZVerse.MicroHuaxia.Gateway.Tests.Unit.Caching;

[TestFixture]
public class InMemoryGatewayCacheTests
{
    private InMemoryGatewayCache _cache = null!;
    private Mock<ILogger<InMemoryGatewayCache>> _loggerMock = null!;

    [SetUp]
    public void SetUp()
    {
        _loggerMock = new Mock<ILogger<InMemoryGatewayCache>>();
        _cache = new InMemoryGatewayCache(_loggerMock.Object);
    }

    [Test]
    public async Task SetAsync_ThenTryGetAsync_ShouldReturnCachedResponse()
    {
        // Arrange
        var response = CreateTestCachedResponse();
        var ttl = TimeSpan.FromMinutes(5);

        // Act
        await _cache.SetAsync("test-key", response, ttl);
        var result = await _cache.TryGetAsync("test-key");

        // Assert
        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(200);
        result.Body.Should().BeEquivalentTo(response.Body);
    }

    [Test]
    public async Task TryGetAsync_NonExistingKey_ShouldReturnNull()
    {
        // Act
        var result = await _cache.TryGetAsync("non-existing");

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public async Task RemoveAsync_ExistingKey_ShouldRemove()
    {
        // Arrange
        var response = CreateTestCachedResponse();
        await _cache.SetAsync("test-key", response, TimeSpan.FromMinutes(5));

        // Act
        await _cache.RemoveAsync("test-key");
        var result = await _cache.TryGetAsync("test-key");

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public async Task ClearAsync_ShouldRemoveAllEntries()
    {
        // Arrange
        var response = CreateTestCachedResponse();
        await _cache.SetAsync("key-1", response, TimeSpan.FromMinutes(5));
        await _cache.SetAsync("key-2", response, TimeSpan.FromMinutes(5));

        // Act
        await _cache.ClearAsync();
        var result1 = await _cache.TryGetAsync("key-1");
        var result2 = await _cache.TryGetAsync("key-2");

        // Assert
        result1.Should().BeNull();
        result2.Should().BeNull();
    }

    private static CachedResponse CreateTestCachedResponse()
    {
        return new CachedResponse
        {
            StatusCode = 200,
            Headers = new Dictionary<string, string[]>
            {
                ["Content-Type"] = ["application/json"]
            },
            Body = """{"message":"test"}"""u8.ToArray(),
            ContentType = "application/json"
        };
    }
}

[TestFixture]
public class CacheKeyGeneratorTests
{
    private CacheKeyGenerator _keyGenerator = null!;

    [SetUp]
    public void SetUp()
    {
        _keyGenerator = new CacheKeyGenerator();
    }

    [Test]
    public void GenerateKey_SameRequest_ShouldGenerateSameKey()
    {
        // Arrange
        var context1 = CreateHttpContext("/api/users", "GET");
        var context2 = CreateHttpContext("/api/users", "GET");
        var cacheConfig = new RouteCache();

        // Act
        var key1 = _keyGenerator.GenerateKey(context1, cacheConfig);
        var key2 = _keyGenerator.GenerateKey(context2, cacheConfig);

        // Assert
        key1.Should().Be(key2);
    }

    [Test]
    public void GenerateKey_DifferentPaths_ShouldGenerateDifferentKeys()
    {
        // Arrange
        var context1 = CreateHttpContext("/api/users", "GET");
        var context2 = CreateHttpContext("/api/products", "GET");
        var cacheConfig = new RouteCache();

        // Act
        var key1 = _keyGenerator.GenerateKey(context1, cacheConfig);
        var key2 = _keyGenerator.GenerateKey(context2, cacheConfig);

        // Assert
        key1.Should().NotBe(key2);
    }

    [Test]
    public void GenerateKey_DifferentMethods_ShouldGenerateDifferentKeys()
    {
        // Arrange
        var context1 = CreateHttpContext("/api/users", "GET");
        var context2 = CreateHttpContext("/api/users", "POST");
        var cacheConfig = new RouteCache();

        // Act
        var key1 = _keyGenerator.GenerateKey(context1, cacheConfig);
        var key2 = _keyGenerator.GenerateKey(context2, cacheConfig);

        // Assert
        key1.Should().NotBe(key2);
    }

    [Test]
    public void GenerateKey_WithVaryByHeader_ShouldIncludeHeaderValue()
    {
        // Arrange
        var context1 = CreateHttpContext("/api/users", "GET");
        context1.Request.Headers["Accept-Language"] = "en-US";
        var context2 = CreateHttpContext("/api/users", "GET");
        context2.Request.Headers["Accept-Language"] = "zh-CN";
        var cacheConfig = new RouteCache { VaryByHeader = ["Accept-Language"] };

        // Act
        var key1 = _keyGenerator.GenerateKey(context1, cacheConfig);
        var key2 = _keyGenerator.GenerateKey(context2, cacheConfig);

        // Assert
        key1.Should().NotBe(key2);
    }

    [Test]
    public void GenerateKey_WithVaryByQuery_ShouldIncludeQueryValue()
    {
        // Arrange
        var context1 = CreateHttpContext("/api/users?page=1", "GET");
        var context2 = CreateHttpContext("/api/users?page=2", "GET");
        var cacheConfig = new RouteCache { VaryByQuery = ["page"] };

        // Act
        var key1 = _keyGenerator.GenerateKey(context1, cacheConfig);
        var key2 = _keyGenerator.GenerateKey(context2, cacheConfig);

        // Assert
        key1.Should().NotBe(key2);
    }

    private static HttpContext CreateHttpContext(string pathAndQuery, string method)
    {
        var context = new DefaultHttpContext();
        
        // 解析路径和查询字符串
        var parts = pathAndQuery.Split('?', 2);
        context.Request.Path = parts[0];
        if (parts.Length > 1)
        {
            context.Request.QueryString = new QueryString("?" + parts[1]);
        }
        
        context.Request.Method = method;
        return context;
    }
}
