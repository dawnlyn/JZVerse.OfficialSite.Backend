using JZVerse.Business.Abstractions.Authentication;
using JZVerse.Business.Abstractions.Models;
using JZVerse.Business.Central.File.Arguments;
using JZVerse.Business.Central.File.Results;
using JZVerse.Business.Central.File.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace JZVerse.Business.Central.File.Controllers;

/// <summary>
/// 文件控制器
/// </summary>
[ApiController]
[Route("api/v1/files")]
public sealed class FileController : ControllerBase
{
    private readonly FileService _fileService;
    private readonly IUserContext _userContext;

    public FileController(FileService fileService, IUserContext userContext)
    {
        _fileService = fileService;
        _userContext = userContext;
    }

    /// <summary>
    /// 上传文件
    /// </summary>
    [HttpPost("upload")]
    [Authorize]
    [RequestSizeLimit(2L * 1024 * 1024 * 1024)] // 2GB
    public async Task<ResultUploadFile> UploadAsync(IFormFile file, [FromQuery] string module, [FromQuery] Guid? contentId = null)
    {
        return await _fileService.UploadAsync(file, module, _userContext.UserId, _userContext.Username, contentId);
    }

    /// <summary>
    /// 批量上传文件
    /// </summary>
    [HttpPost("upload/batch")]
    [Authorize]
    [RequestSizeLimit(2L * 1024 * 1024 * 1024)]
    public async Task<IReadOnlyList<ResultUploadFile>> BatchUploadAsync(IFormFileCollection files, [FromQuery] string module, [FromQuery] Guid? contentId = null)
    {
        var results = new List<ResultUploadFile>();
        foreach (var file in files)
        {
            var result = await _fileService.UploadAsync(file, module, _userContext.UserId, _userContext.Username, contentId);
            results.Add(result);
        }
        return results;
    }

    /// <summary>
    /// 获取文件信息
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ResultFileInfo?> GetByIdAsync(Guid id)
    {
        return await _fileService.GetByIdAsync(id);
    }

    /// <summary>
    /// 预览文件
    /// </summary>
    [HttpGet("preview/{id}")]
    public async Task<IActionResult> PreviewAsync(Guid id)
    {
        var result = await _fileService.GetFileStreamAsync(id, isDownload: false);
        if (result is null)
            return NotFound();

        return File(result.Value.stream, result.Value.contentType);
    }

    /// <summary>
    /// 下载文件
    /// </summary>
    [HttpGet("download/{id}")]
    public async Task<IActionResult> DownloadAsync(Guid id)
    {
        var result = await _fileService.GetFileStreamAsync(id, isDownload: true);
        if (result is null)
            return NotFound();

        return File(result.Value.stream, result.Value.contentType, result.Value.fileName);
    }

    /// <summary>
    /// 获取文件列表
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<PagedResult<ResultFileInfo>> GetListAsync([FromQuery] ArgQueryFiles arg)
    {
        return await _fileService.GetListAsync(arg);
    }

    /// <summary>
    /// 获取内容关联的文件
    /// </summary>
    [HttpGet("content/{module}/{contentId}")]
    public async Task<IReadOnlyList<ResultFileInfo>> GetByContentAsync(string module, Guid contentId)
    {
        return await _fileService.GetByContentAsync(module, contentId);
    }

    /// <summary>
    /// 禁用文件
    /// </summary>
    [HttpPut("{id}/disable")]
    [Authorize]
    public async Task<bool> DisableAsync(Guid id)
    {
        return await _fileService.DisableAsync(id);
    }

    /// <summary>
    /// 启用文件
    /// </summary>
    [HttpPut("{id}/enable")]
    [Authorize]
    public async Task<bool> EnableAsync(Guid id)
    {
        return await _fileService.EnableAsync(id);
    }

    /// <summary>
    /// 删除文件（移到回收站）
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize]
    public async Task<bool> DeleteAsync(Guid id)
    {
        return await _fileService.DeleteAsync(id);
    }

    /// <summary>
    /// 批量删除文件
    /// </summary>
    [HttpDelete("batch")]
    [Authorize]
    public async Task<int> BatchDeleteAsync([FromBody] ArgBatchFileOperation arg)
    {
        return await _fileService.BatchDeleteAsync(arg.FileIds);
    }

    /// <summary>
    /// 恢复文件
    /// </summary>
    [HttpPut("{id}/restore")]
    [Authorize]
    public async Task<bool> RestoreAsync(Guid id)
    {
        return await _fileService.RestoreAsync(id);
    }

    /// <summary>
    /// 关联文件到内容
    /// </summary>
    [HttpPut("link")]
    [Authorize]
    public async Task<bool> LinkToContentAsync([FromBody] ArgLinkFileToContent arg)
    {
        return await _fileService.LinkToContentAsync(arg.FileId, arg.ContentId);
    }

    /// <summary>
    /// 获取存储统计
    /// </summary>
    [HttpGet("stats/storage")]
    [Authorize]
    public async Task<ResultStorageStats> GetStorageStatsAsync()
    {
        return await _fileService.GetStorageStatsAsync();
    }

    /// <summary>
    /// 获取回收站文件列表
    /// </summary>
    [HttpGet("recycle")]
    [Authorize]
    public async Task<PagedResult<ResultDeletedFile>> GetDeletedFilesAsync([FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 20)
    {
        return await _fileService.GetDeletedFilesAsync(pageIndex, pageSize);
    }

    /// <summary>
    /// 获取上传日志
    /// </summary>
    [HttpGet("logs/upload")]
    [Authorize]
    public async Task<PagedResult<ResultUploadLog>> GetUploadLogsAsync([FromQuery] ArgQueryUploadLogs arg)
    {
        return await _fileService.GetUploadLogsAsync(arg);
    }
}
