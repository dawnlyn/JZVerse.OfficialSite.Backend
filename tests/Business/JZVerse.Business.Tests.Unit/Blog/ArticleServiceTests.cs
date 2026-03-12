using JZVerse.Business.Abstractions.Models;
using JZVerse.Business.Blog.Arguments;
using JZVerse.Business.Blog.Database;
using JZVerse.Business.Blog.Services;
using JZVerse.DataAccess.Abstractions;

namespace JZVerse.Business.Tests.Unit.Blog;

/// <summary>
/// 文章服务单元测试
/// </summary>
[TestFixture]
public class ArticleServiceTests
{
    private Mock<IDbExecutor> _dbExecutorMock = null!;
    private Mock<ISnowflakeIdGenerator> _idGeneratorMock = null!;
    private ArticleService _articleService = null!;

    [SetUp]
    public void SetUp()
    {
        _dbExecutorMock = new Mock<IDbExecutor>();
        _idGeneratorMock = new Mock<ISnowflakeIdGenerator>();
        _articleService = new ArticleService(_dbExecutorMock.Object, _idGeneratorMock.Object);
    }

    #region SaveAsync Tests

    [Test]
    public async Task SaveAsync_NewArticle_ShouldCreateAndReturnNewId()
    {
        // Arrange
        var newId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        _idGeneratorMock.Setup(x => x.GenerateGuid()).Returns(newId);
        _dbExecutorMock.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>(), default))
            .ReturnsAsync(1);

        var arg = new ArgSaveArticle
        {
            Title = "Test Article",
            Summary = "Test Summary",
            Content = "Test Content",
            CategoryId = categoryId,
            Tags = ["tag1", "tag2"]
        };

        _dbExecutorMock.Setup(x => x.QueryAsync<TagEntity>(It.IsAny<string>(), It.IsAny<object>(), default))
            .ReturnsAsync([
                new TagEntity { Id = Guid.NewGuid(), Name = "tag1" },
                new TagEntity { Id = Guid.NewGuid(), Name = "tag2" }
            ]);

        // Act
        var result = await _articleService.SaveAsync(arg, authorId, "TestAuthor");

        // Assert
        result.Should().Be(newId);
        _idGeneratorMock.Verify(x => x.GenerateGuid(), Times.AtLeastOnce);
        _dbExecutorMock.Verify(x => x.ExecuteAsync(BlogSql.CreateArticle, It.IsAny<object>(), default), Times.Once);
    }

    [Test]
    public async Task SaveAsync_ExistingArticle_ShouldUpdateAndReturnExistingId()
    {
        // Arrange
        var existingId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        _dbExecutorMock.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>(), default))
            .ReturnsAsync(1);

        var arg = new ArgSaveArticle
        {
            Id = existingId,
            Title = "Updated Article",
            Summary = "Updated Summary",
            Content = "Updated Content",
            CategoryId = categoryId
        };

        // Act
        var result = await _articleService.SaveAsync(arg, authorId, "TestAuthor");

        // Assert
        result.Should().Be(existingId);
        _idGeneratorMock.Verify(x => x.GenerateGuid(), Times.Never);
        _dbExecutorMock.Verify(x => x.ExecuteAsync(BlogSql.UpdateArticle, It.IsAny<object>(), default), Times.Once);
    }

    [Test]
    public async Task SaveAsync_WithTags_ShouldProcessTags()
    {
        // Arrange
        var newId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        _idGeneratorMock.Setup(x => x.GenerateGuid()).Returns(newId);
        _dbExecutorMock.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>(), default))
            .ReturnsAsync(1);
        _dbExecutorMock.Setup(x => x.QueryAsync<TagEntity>(BlogSql.GetAllTags, It.IsAny<object>(), default))
            .ReturnsAsync([new TagEntity { Id = Guid.NewGuid(), Name = "existing-tag" }]);

        var arg = new ArgSaveArticle
        {
            Title = "Test Article",
            Summary = "Test Summary",
            Content = "Test Content",
            CategoryId = categoryId,
            Tags = ["existing-tag", "new-tag"]
        };

        // Act
        await _articleService.SaveAsync(arg, authorId, "TestAuthor");

        // Assert
        _dbExecutorMock.Verify(x => x.ExecuteAsync(BlogSql.DeleteArticleTags, It.IsAny<object>(), default), Times.Once);
        _dbExecutorMock.Verify(x => x.ExecuteAsync(BlogSql.CreateTag, It.IsAny<object>(), default), Times.AtLeast(1));
    }

    #endregion

    #region GetByIdAsync Tests

    [Test]
    public async Task GetByIdAsync_ExistingArticle_ShouldReturnArticleDetail()
    {
        // Arrange
        var articleId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var article = new ArticleEntity
        {
            Id = articleId,
            Title = "Test Article",
            Summary = "Test Summary",
            Content = "Test Content",
            CategoryId = categoryId,
            AuthorName = "Author",
            Status = ArticleStatus.Published,
            ViewCount = 100,
            CreatedAt = DateTime.UtcNow
        };

        _dbExecutorMock.Setup(x => x.QueryFirstOrDefaultAsync<ArticleEntity>(BlogSql.GetArticleById, It.IsAny<object>(), default))
            .ReturnsAsync(article);
        _dbExecutorMock.Setup(x => x.QueryAsync<TagEntity>(BlogSql.GetArticleTags, It.IsAny<object>(), default))
            .ReturnsAsync([new TagEntity { Id = Guid.NewGuid(), Name = "tag1" }]);
        _dbExecutorMock.Setup(x => x.QueryAsync<CategoryEntity>(BlogSql.GetAllCategories, It.IsAny<object>(), default))
            .ReturnsAsync([new CategoryEntity { Id = categoryId, Name = "Test Category" }]);

        // Act
        var result = await _articleService.GetByIdAsync(articleId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(articleId);
        result.Title.Should().Be("Test Article");
        result.CategoryName.Should().Be("Test Category");
        result.Tags.Should().HaveCount(1);
    }

    [Test]
    public async Task GetByIdAsync_NonExistingArticle_ShouldReturnNull()
    {
        // Arrange
        var articleId = Guid.NewGuid();
        _dbExecutorMock.Setup(x => x.QueryFirstOrDefaultAsync<ArticleEntity>(BlogSql.GetArticleById, It.IsAny<object>(), default))
            .ReturnsAsync((ArticleEntity?)null);

        // Act
        var result = await _articleService.GetByIdAsync(articleId);

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public async Task GetByIdAsync_WithIncrementView_ShouldIncrementViewCount()
    {
        // Arrange
        var articleId = Guid.NewGuid();
        var article = new ArticleEntity
        {
            Id = articleId,
            Title = "Test",
            CategoryId = Guid.NewGuid(),
            ViewCount = 100
        };

        _dbExecutorMock.Setup(x => x.QueryFirstOrDefaultAsync<ArticleEntity>(BlogSql.GetArticleById, It.IsAny<object>(), default))
            .ReturnsAsync(article);
        _dbExecutorMock.Setup(x => x.QueryAsync<TagEntity>(BlogSql.GetArticleTags, It.IsAny<object>(), default))
            .ReturnsAsync([]);
        _dbExecutorMock.Setup(x => x.QueryAsync<CategoryEntity>(BlogSql.GetAllCategories, It.IsAny<object>(), default))
            .ReturnsAsync([]);

        // Act
        var result = await _articleService.GetByIdAsync(articleId, incrementView: true);

        // Assert
        _dbExecutorMock.Verify(x => x.ExecuteAsync(BlogSql.IncrementViewCount, It.IsAny<object>(), default), Times.Once);
        result!.ViewCount.Should().Be(101);
    }

    #endregion

    #region PublishAsync Tests

    [Test]
    public async Task PublishAsync_ExistingArticle_ShouldPublishAndReturnTrue()
    {
        // Arrange
        var articleId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        _dbExecutorMock.Setup(x => x.ExecuteAsync(BlogSql.PublishArticle, It.IsAny<object>(), default))
            .ReturnsAsync(1);
        _dbExecutorMock.Setup(x => x.QueryFirstOrDefaultAsync<ArticleEntity>(BlogSql.GetArticleById, It.IsAny<object>(), default))
            .ReturnsAsync(new ArticleEntity { Id = articleId, CategoryId = categoryId });
        _dbExecutorMock.Setup(x => x.ExecuteAsync(BlogSql.UpdateCategoryArticleCount, It.IsAny<object>(), default))
            .ReturnsAsync(1);

        // Act
        var result = await _articleService.PublishAsync(articleId);

        // Assert
        result.Should().BeTrue();
        _dbExecutorMock.Verify(x => x.ExecuteAsync(BlogSql.PublishArticle, It.IsAny<object>(), default), Times.Once);
        _dbExecutorMock.Verify(x => x.ExecuteAsync(BlogSql.UpdateCategoryArticleCount, It.IsAny<object>(), default), Times.Once);
    }

    [Test]
    public async Task PublishAsync_NonExistingArticle_ShouldReturnFalse()
    {
        // Arrange
        var articleId = Guid.NewGuid();
        _dbExecutorMock.Setup(x => x.ExecuteAsync(BlogSql.PublishArticle, It.IsAny<object>(), default))
            .ReturnsAsync(0);

        // Act
        var result = await _articleService.PublishAsync(articleId);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region DeleteAsync Tests

    [Test]
    public async Task DeleteAsync_ExistingArticle_ShouldSoftDeleteAndReturnTrue()
    {
        // Arrange
        var articleId = Guid.NewGuid();
        _dbExecutorMock.Setup(x => x.ExecuteAsync(BlogSql.SoftDeleteArticle, It.IsAny<object>(), default))
            .ReturnsAsync(1);

        // Act
        var result = await _articleService.DeleteAsync(articleId);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public async Task DeleteAsync_NonExistingArticle_ShouldReturnFalse()
    {
        // Arrange
        var articleId = Guid.NewGuid();
        _dbExecutorMock.Setup(x => x.ExecuteAsync(BlogSql.SoftDeleteArticle, It.IsAny<object>(), default))
            .ReturnsAsync(0);

        // Act
        var result = await _articleService.DeleteAsync(articleId);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region GetListAdminAsync Tests

    [Test]
    public async Task GetListAdminAsync_ShouldReturnPagedResult()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var articles = new List<ArticleEntity>
        {
            new() { Id = Guid.NewGuid(), Title = "Article 1", CategoryId = categoryId, Status = ArticleStatus.Published },
            new() { Id = Guid.NewGuid(), Title = "Article 2", CategoryId = categoryId, Status = ArticleStatus.Draft }
        };

        _dbExecutorMock.Setup(x => x.QueryAsync<ArticleEntity>(BlogSql.GetArticleListAdmin, It.IsAny<object>(), default))
            .ReturnsAsync(articles);
        _dbExecutorMock.Setup(x => x.ExecuteScalarAsync<int>(BlogSql.GetArticleCount, It.IsAny<object>(), default))
            .ReturnsAsync(2);
        _dbExecutorMock.Setup(x => x.QueryAsync<CategoryEntity>(BlogSql.GetAllCategories, It.IsAny<object>(), default))
            .ReturnsAsync([new CategoryEntity { Id = categoryId, Name = "Test Category" }]);

        var arg = new ArgQueryArticlesAdmin { PageIndex = 1, PageSize = 10 };

        // Act
        var result = await _articleService.GetListAdminAsync(arg);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
        result.PageIndex.Should().Be(1);
        result.PageSize.Should().Be(10);
    }

    #endregion
}
