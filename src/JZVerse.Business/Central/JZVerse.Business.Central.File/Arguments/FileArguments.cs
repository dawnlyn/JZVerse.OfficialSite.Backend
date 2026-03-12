using JZVerse.Business.Abstractions.Validation;

namespace JZVerse.Business.Central.File.Arguments;

/// <summary>
/// 文件上传入参
/// </summary>
public sealed record ArgUploadFile
{
    [Required(ErrorMessage = "模块不能为空")]
    public string Module { get; init; } = "";

    public Guid? ContentId { get; init; }
}

/// <summary>
/// 查询文件列表入参
/// </summary>
public sealed record ArgQueryFiles
{
    public string? Module { get; init; }
    public string? FileType { get; init; }
    public int? Status { get; init; }
    public string? Keyword { get; init; }
    public DateTime? StartTime { get; init; }
    public DateTime? EndTime { get; init; }

    [Range(1, 100, ErrorMessage = "每页数量必须在1-100之间")]
    public int PageSize { get; init; } = 20;

    [Range(1, int.MaxValue, ErrorMessage = "页码必须大于0")]
    public int PageIndex { get; init; } = 1;
}

/// <summary>
/// 批量操作文件入参
/// </summary>
public sealed record ArgBatchFileOperation
{
    [Required(ErrorMessage = "文件ID列表不能为空")]
    public Guid[] FileIds { get; init; } = [];
}

/// <summary>
/// 关联文件到内容入参
/// </summary>
public sealed record ArgLinkFileToContent
{
    [Required(ErrorMessage = "文件ID不能为空")]
    public Guid FileId { get; init; }

    [Required(ErrorMessage = "内容ID不能为空")]
    public Guid ContentId { get; init; }
}

/// <summary>
/// 查询上传日志入参
/// </summary>
public sealed record ArgQueryUploadLogs
{
    public DateTime? StartTime { get; init; }
    public DateTime? EndTime { get; init; }
    public bool? IsSuccess { get; init; }

    [Range(1, 100, ErrorMessage = "每页数量必须在1-100之间")]
    public int PageSize { get; init; } = 20;

    [Range(1, int.MaxValue, ErrorMessage = "页码必须大于0")]
    public int PageIndex { get; init; } = 1;
}
