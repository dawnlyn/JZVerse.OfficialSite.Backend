using JZVerse.Business.Abstractions.Validation;

namespace JZVerse.Business.Project.Arguments;

/// <summary>
/// 保存项目入参
/// </summary>
public sealed record ArgSaveProject
{
    public Guid? Id { get; init; }

    [Required(ErrorMessage = "项目名称不能为空")]
    [StringLength(MinLength = 1, MaxLength = 100, ErrorMessage = "项目名称长度必须在1-100之间")]
    public string Name { get; init; } = "";

    [StringLength(MaxLength = 300, ErrorMessage = "简介最大300字")]
    public string? Summary { get; init; }

    public string? Description { get; init; }
    public string? CoverImage { get; init; }

    public Guid CategoryId { get; init; }

    public string? TechStack { get; init; }
    public string? DevelopmentCycle { get; init; }
    public string? SourceUrl { get; init; }
    public string? DemoUrl { get; init; }
    public int Progress { get; init; } = 1;
    public bool IsRecommended { get; init; }
}

/// <summary>
/// 查询项目列表入参（后台）
/// </summary>
public sealed record ArgQueryProjectsAdmin
{
    public int? Status { get; init; }
    public Guid? CategoryId { get; init; }
    public int? Progress { get; init; }
    public string? Keyword { get; init; }

    [Range(1, 100, ErrorMessage = "每页数量必须在1-100之间")]
    public int PageSize { get; init; } = 20;

    [Range(1, int.MaxValue, ErrorMessage = "页码必须大于0")]
    public int PageIndex { get; init; } = 1;
}

/// <summary>
/// 查询项目列表入参（前端）
/// </summary>
public sealed record ArgQueryProjectsPublic
{
    public Guid? CategoryId { get; init; }
    public int? Progress { get; init; }
    public string? Keyword { get; init; }

    [Range(1, 50, ErrorMessage = "每页数量必须在1-50之间")]
    public int PageSize { get; init; } = 12;

    [Range(1, int.MaxValue, ErrorMessage = "页码必须大于0")]
    public int PageIndex { get; init; } = 1;
}
