using JZVerse.Business.Abstractions.Models;
using JZVerse.Business.Blog.Arguments;
using JZVerse.Business.Blog.Database;
using JZVerse.Business.Blog.Results;
using JZVerse.MicroHuaxia.DataAccess.Abstractions;

namespace JZVerse.Business.Blog.Services;

/// <summary>
/// 文章服务
/// </summary>
public sealed class ArticleService
{
    private readonly IDbExecutor _db;
    private readonly ISnowflakeIdGenerator _idGenerator;

    public ArticleService(IDbExecutor db, ISnowflakeIdGenerator idGenerator)
    {
        _db = db;
        _idGenerator = idGenerator;
    }

    /// <summary>
    /// 保存文章（创建或更新）
    /// </summary>
    public async Task<Guid> SaveAsync(ArgSaveArticle arg, Guid authorId, string? authorName)
    {
        var isNew = !arg.Id.HasValue || arg.Id.Value == Guid.Empty;
        var articleId = isNew ? _idGenerator.GenerateGuid() : arg.Id!.Value;

        if (isNew)
        {
            var article = new ArticleEntity
            {
                Id = articleId,
                Title = arg.Title,
                Summary = arg.Summary,
                Content = arg.Content,
                CoverImage = arg.CoverImage,
                CategoryId = arg.CategoryId,
                AuthorId = authorId,
                AuthorName = authorName,
                Status = arg.ScheduledAt.HasValue ? ArticleStatus.Scheduled : ArticleStatus.Draft,
                ViewCount = 0,
                LikeCount = 0,
                CommentCount = 0,
                IsRecommended = arg.IsRecommended,
                IsTop = arg.IsTop,
                ScheduledAt = arg.ScheduledAt,
                CreatedAt = DateTime.UtcNow,
            };

            await _db.ExecuteAsync(BlogSql.CreateArticle, article);
        }
        else
        {
            await _db.ExecuteAsync(
                BlogSql.UpdateArticle,
                new
                {
                    Id = articleId,
                    arg.Title,
                    arg.Summary,
                    arg.Content,
                    arg.CoverImage,
                    arg.CategoryId,
                    arg.IsRecommended,
                    arg.IsTop,
                    arg.ScheduledAt,
                    UpdatedAt = DateTime.UtcNow,
                }
            );
        }

        // 处理标签
        if (arg.Tags is not null)
        {
            await _db.ExecuteAsync(BlogSql.DeleteArticleTags, new { ArticleId = articleId });
            foreach (var tagName in arg.Tags)
            {
                var tagId = _idGenerator.GenerateGuid();
                await _db.ExecuteAsync(
                    BlogSql.CreateTag,
                    new TagEntity
                    {
                        Id = tagId,
                        Name = tagName,
                        ArticleCount = 0,
                        CreatedAt = DateTime.UtcNow,
                    }
                );

                // 获取标签 ID（可能是已存在的）
                var tags = await _db.QueryAsync<TagEntity>(BlogSql.GetAllTags);
                var existingTag = tags.FirstOrDefault(t => t.Name == tagName);
                if (existingTag is not null)
                {
                    await _db.ExecuteAsync(
                        BlogSql.AddArticleTag,
                        new
                        {
                            ArticleId = articleId,
                            TagId = existingTag.Id,
                            CreatedAt = DateTime.UtcNow,
                        }
                    );
                }
            }
        }

        return articleId;
    }

    /// <summary>
    /// 获取文章详情
    /// </summary>
    public async Task<ResultArticleDetail?> GetByIdAsync(Guid id, bool incrementView = false)
    {
        var article = await _db.QueryFirstOrDefaultAsync<ArticleEntity>(BlogSql.GetArticleById, new { Id = id });
        if (article is null)
            return null;

        if (incrementView)
        {
            await _db.ExecuteAsync(BlogSql.IncrementViewCount, new { Id = id });
        }

        var tags = await _db.QueryAsync<TagEntity>(BlogSql.GetArticleTags, new { ArticleId = id });
        var categories = await _db.QueryAsync<CategoryEntity>(BlogSql.GetAllCategories);
        var category = categories.FirstOrDefault(c => c.Id == article.CategoryId);

        return new ResultArticleDetail
        {
            Id = article.Id,
            Title = article.Title,
            Summary = article.Summary,
            Content = article.Content,
            CoverImage = article.CoverImage,
            CategoryId = article.CategoryId,
            CategoryName = category?.Name,
            AuthorName = article.AuthorName,
            Status = article.Status,
            ViewCount = article.ViewCount + (incrementView ? 1 : 0),
            LikeCount = article.LikeCount,
            CommentCount = article.CommentCount,
            IsRecommended = article.IsRecommended,
            IsTop = article.IsTop,
            PublishedAt = article.PublishedAt,
            ScheduledAt = article.ScheduledAt,
            CreatedAt = article.CreatedAt,
            Tags = tags.Select(t => new ResultTag
                {
                    Id = t.Id,
                    Name = t.Name,
                    Color = t.Color,
                    ArticleCount = t.ArticleCount,
                })
                .ToList(),
        };
    }

    /// <summary>
    /// 获取文章列表（后台管理）
    /// </summary>
    public async Task<PagedResult<ResultArticleItem>> GetListAdminAsync(ArgQueryArticlesAdmin arg)
    {
        var offset = (arg.PageIndex - 1) * arg.PageSize;

        var articles = await _db.QueryAsync<ArticleEntity>(
            BlogSql.GetArticleListAdmin,
            new
            {
                arg.Status,
                arg.CategoryId,
                arg.Keyword,
                Limit = arg.PageSize,
                Offset = offset,
            }
        );

        var total = await _db.ExecuteScalarAsync<int>(
            BlogSql.GetArticleCount,
            new
            {
                arg.Status,
                arg.CategoryId,
                arg.Keyword,
            }
        );

        var categories = await _db.QueryAsync<CategoryEntity>(BlogSql.GetAllCategories);

        return new PagedResult<ResultArticleItem>
        {
            Items = articles.Select(a => MapToArticleItem(a, categories)).ToList(),
            TotalCount = total,
            PageIndex = arg.PageIndex,
            PageSize = arg.PageSize,
        };
    }

    /// <summary>
    /// 获取文章列表（前端展示）
    /// </summary>
    public async Task<PagedResult<ResultArticleItem>> GetListPublicAsync(ArgQueryArticlesPublic arg)
    {
        var offset = (arg.PageIndex - 1) * arg.PageSize;

        var articles = await _db.QueryAsync<ArticleEntity>(
            BlogSql.GetArticleListPublic,
            new
            {
                arg.CategoryId,
                arg.TagId,
                arg.Keyword,
                Limit = arg.PageSize,
                Offset = offset,
            }
        );

        var total = await _db.ExecuteScalarAsync<int>(
            BlogSql.GetArticleCountPublic,
            new
            {
                arg.CategoryId,
                arg.TagId,
                arg.Keyword,
            }
        );

        var categories = await _db.QueryAsync<CategoryEntity>(BlogSql.GetAllCategories);

        return new PagedResult<ResultArticleItem>
        {
            Items = articles.Select(a => MapToArticleItem(a, categories)).ToList(),
            TotalCount = total,
            PageIndex = arg.PageIndex,
            PageSize = arg.PageSize,
        };
    }

    /// <summary>
    /// 发布文章
    /// </summary>
    public async Task<bool> PublishAsync(Guid id)
    {
        var affected = await _db.ExecuteAsync(
            BlogSql.PublishArticle,
            new
            {
                Id = id,
                PublishedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            }
        );

        if (affected > 0)
        {
            var article = await _db.QueryFirstOrDefaultAsync<ArticleEntity>(BlogSql.GetArticleById, new { Id = id });
            if (article is not null)
            {
                await _db.ExecuteAsync(BlogSql.UpdateCategoryArticleCount, new { Id = article.CategoryId });
            }
        }

        return affected > 0;
    }

    /// <summary>
    /// 下架文章
    /// </summary>
    public async Task<bool> OfflineAsync(Guid id)
    {
        var article = await _db.QueryFirstOrDefaultAsync<ArticleEntity>(BlogSql.GetArticleById, new { Id = id });

        var affected = await _db.ExecuteAsync(BlogSql.OfflineArticle, new { Id = id, UpdatedAt = DateTime.UtcNow });

        if (affected > 0 && article is not null)
        {
            await _db.ExecuteAsync(BlogSql.UpdateCategoryArticleCount, new { Id = article.CategoryId });
        }

        return affected > 0;
    }

    /// <summary>
    /// 删除文章
    /// </summary>
    public async Task<bool> DeleteAsync(Guid id)
    {
        var affected = await _db.ExecuteAsync(BlogSql.SoftDeleteArticle, new { Id = id, DeletedAt = DateTime.UtcNow });
        return affected > 0;
    }

    /// <summary>
    /// 获取博客首页数据
    /// </summary>
    public async Task<ResultBlogHome> GetHomeDataAsync()
    {
        var recommended = await _db.QueryAsync<ArticleEntity>(BlogSql.GetRecommendedArticles, new { Limit = 5 });
        var latest = await _db.QueryAsync<ArticleEntity>(
            BlogSql.GetArticleListPublic,
            new
            {
                CategoryId = (long?)null,
                TagId = (long?)null,
                Keyword = (string?)null,
                Limit = 10,
                Offset = 0,
            }
        );
        var hot = await _db.QueryAsync<ArticleEntity>(
            BlogSql.GetHotArticles,
            new { StartTime = DateTime.UtcNow.AddDays(-30), Limit = 10 }
        );
        var categories = await _db.QueryAsync<CategoryEntity>(BlogSql.GetAllCategories);
        var tags = await _db.QueryAsync<TagEntity>(BlogSql.GetAllTags);

        return new ResultBlogHome
        {
            RecommendedArticles = recommended.Select(a => MapToArticleItem(a, categories)).ToList(),
            LatestArticles = latest.Select(a => MapToArticleItem(a, categories)).ToList(),
            HotArticles = hot.Select(a => MapToArticleItem(a, categories)).ToList(),
            Categories = categories
                .Select(c => new ResultCategory
                {
                    Id = c.Id,
                    Name = c.Name,
                    Description = c.Description,
                    Icon = c.Icon,
                    SortOrder = c.SortOrder,
                    ArticleCount = c.ArticleCount,
                })
                .ToList(),
            Tags = tags.Take(20)
                .Select(t => new ResultTag
                {
                    Id = t.Id,
                    Name = t.Name,
                    Color = t.Color,
                    ArticleCount = t.ArticleCount,
                })
                .ToList(),
        };
    }

    private static ResultArticleItem MapToArticleItem(ArticleEntity article, IEnumerable<CategoryEntity> categories)
    {
        var category = categories.FirstOrDefault(c => c.Id == article.CategoryId);
        return new ResultArticleItem
        {
            Id = article.Id,
            Title = article.Title,
            Summary = article.Summary,
            CoverImage = article.CoverImage,
            CategoryId = article.CategoryId,
            CategoryName = category?.Name,
            AuthorName = article.AuthorName,
            Status = article.Status,
            ViewCount = article.ViewCount,
            LikeCount = article.LikeCount,
            CommentCount = article.CommentCount,
            IsRecommended = article.IsRecommended,
            IsTop = article.IsTop,
            PublishedAt = article.PublishedAt,
            CreatedAt = article.CreatedAt,
        };
    }
}
