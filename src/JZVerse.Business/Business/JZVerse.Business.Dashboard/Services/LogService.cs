using JZVerse.Business.Abstractions.Models;
using JZVerse.Business.Dashboard.Arguments;
using JZVerse.Business.Dashboard.Database;
using JZVerse.Business.Dashboard.Results;
using JZVerse.DataAccess.Abstractions;

namespace JZVerse.Business.Dashboard.Services;

/// <summary>
/// 日志服务
/// </summary>
public sealed class LogService
{
    private readonly IDbExecutor _db;
    private readonly ISnowflakeIdGenerator _idGenerator;

    public LogService(IDbExecutor db, ISnowflakeIdGenerator idGenerator)
    {
        _db = db;
        _idGenerator = idGenerator;
    }

    #region Operation Log

    /// <summary>
    /// 记录操作日志
    /// </summary>
    public async Task<Guid> LogOperationAsync(
        Guid? operatorId,
        string? operatorName,
        string module,
        string action,
        string? description,
        string? requestMethod,
        string? requestPath,
        string? requestBody,
        string? responseBody,
        string? clientIp,
        string? userAgent,
        int statusCode,
        long executionTime)
    {
        var log = new OperationLogEntity
        {
            Id = _idGenerator.GenerateGuid(),
            OperatorId = operatorId,
            OperatorName = operatorName,
            Module = module,
            Action = action,
            Description = description,
            RequestMethod = requestMethod,
            RequestPath = requestPath,
            RequestBody = requestBody,
            ResponseBody = responseBody,
            ClientIp = clientIp,
            UserAgent = userAgent,
            StatusCode = statusCode,
            ExecutionTime = executionTime,
            CreatedAt = DateTime.UtcNow
        };

        await _db.ExecuteAsync(DashboardSql.CreateOperationLog, log);
        return log.Id;
    }

    /// <summary>
    /// 获取操作日志详情
    /// </summary>
    public async Task<ResultOperationLog?> GetOperationLogByIdAsync(Guid id)
    {
        var log = await _db.QueryFirstOrDefaultAsync<OperationLogEntity>(
            DashboardSql.GetOperationLogById,
            new { Id = id });

        return log is null ? null : MapToOperationLogResult(log);
    }

    /// <summary>
    /// 查询操作日志列表
    /// </summary>
    public async Task<PagedResult<ResultOperationLog>> GetOperationLogsAsync(ArgQueryOperationLogs arg)
    {
        var offset = (arg.PageIndex - 1) * arg.PageSize;

        var logs = await _db.QueryAsync<OperationLogEntity>(DashboardSql.GetOperationLogs, new
        {
            arg.Module,
            arg.Keyword,
            arg.StartTime,
            arg.EndTime,
            Limit = arg.PageSize,
            Offset = offset
        });

        var total = await _db.ExecuteScalarAsync<int>(DashboardSql.GetOperationLogCount, new
        {
            arg.Module,
            arg.Keyword,
            arg.StartTime,
            arg.EndTime
        });

        return new PagedResult<ResultOperationLog>
        {
            Items = logs.Select(MapToOperationLogResult).ToList(),
            TotalCount = total,
            PageIndex = arg.PageIndex,
            PageSize = arg.PageSize
        };
    }

    /// <summary>
    /// 清理过期操作日志
    /// </summary>
    public async Task<int> CleanupOperationLogsAsync(int daysToKeep)
    {
        var beforeTime = DateTime.UtcNow.AddDays(-daysToKeep);
        return await _db.ExecuteAsync(DashboardSql.DeleteOperationLogs, new { BeforeTime = beforeTime });
    }

    #endregion

    #region Exception Log

    /// <summary>
    /// 记录异常日志
    /// </summary>
    public async Task<Guid> LogExceptionAsync(
        string module,
        string exceptionType,
        string message,
        string? stackTrace,
        string? requestPath,
        string? requestBody,
        string? clientIp)
    {
        var log = new ExceptionLogEntity
        {
            Id = _idGenerator.GenerateGuid(),
            Module = module,
            ExceptionType = exceptionType,
            Message = message,
            StackTrace = stackTrace,
            RequestPath = requestPath,
            RequestBody = requestBody,
            ClientIp = clientIp,
            Status = ExceptionLogStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        await _db.ExecuteAsync(DashboardSql.CreateExceptionLog, log);
        return log.Id;
    }

    /// <summary>
    /// 获取异常日志详情
    /// </summary>
    public async Task<ResultExceptionLog?> GetExceptionLogByIdAsync(Guid id)
    {
        var log = await _db.QueryFirstOrDefaultAsync<ExceptionLogEntity>(
            DashboardSql.GetExceptionLogById,
            new { Id = id });

        return log is null ? null : MapToExceptionLogResult(log);
    }

    /// <summary>
    /// 查询异常日志列表
    /// </summary>
    public async Task<PagedResult<ResultExceptionLog>> GetExceptionLogsAsync(ArgQueryExceptionLogs arg)
    {
        var offset = (arg.PageIndex - 1) * arg.PageSize;

        var logs = await _db.QueryAsync<ExceptionLogEntity>(DashboardSql.GetExceptionLogs, new
        {
            arg.Module,
            arg.Status,
            arg.Keyword,
            arg.StartTime,
            arg.EndTime,
            Limit = arg.PageSize,
            Offset = offset
        });

        var total = await _db.ExecuteScalarAsync<int>(DashboardSql.GetExceptionLogCount, new
        {
            arg.Module,
            arg.Status,
            arg.Keyword,
            arg.StartTime,
            arg.EndTime
        });

        return new PagedResult<ResultExceptionLog>
        {
            Items = logs.Select(MapToExceptionLogResult).ToList(),
            TotalCount = total,
            PageIndex = arg.PageIndex,
            PageSize = arg.PageSize
        };
    }

    /// <summary>
    /// 处理异常日志
    /// </summary>
    public async Task<bool> HandleExceptionLogAsync(ArgHandleExceptionLog arg, Guid handlerId)
    {
        var affected = await _db.ExecuteAsync(DashboardSql.UpdateExceptionLogStatus, new
        {
            arg.Id,
            arg.Status,
            arg.Solution,
            HandlerId = handlerId,
            HandledAt = DateTime.UtcNow
        });

        return affected > 0;
    }

    #endregion

    #region Login Log

    /// <summary>
    /// 记录登录日志
    /// </summary>
    public async Task<Guid> LogLoginAsync(
        Guid? userId,
        string? username,
        LoginLogType loginType,
        string? clientIp,
        string? location,
        string? userAgent,
        string? deviceType,
        string? browser,
        string? os,
        bool isSuccess,
        string? failReason,
        bool isAbnormal = false,
        string? abnormalReason = null)
    {
        var log = new LoginLogEntity
        {
            Id = _idGenerator.GenerateGuid(),
            UserId = userId,
            Username = username,
            LoginType = loginType,
            ClientIp = clientIp,
            Location = location,
            UserAgent = userAgent,
            DeviceType = deviceType,
            Browser = browser,
            Os = os,
            IsSuccess = isSuccess,
            FailReason = failReason,
            IsAbnormal = isAbnormal,
            AbnormalReason = abnormalReason,
            CreatedAt = DateTime.UtcNow
        };

        await _db.ExecuteAsync(DashboardSql.CreateLoginLog, log);
        return log.Id;
    }

    /// <summary>
    /// 获取登录日志详情
    /// </summary>
    public async Task<ResultLoginLog?> GetLoginLogByIdAsync(Guid id)
    {
        var log = await _db.QueryFirstOrDefaultAsync<LoginLogEntity>(
            DashboardSql.GetLoginLogById,
            new { Id = id });

        return log is null ? null : MapToLoginLogResult(log);
    }

    /// <summary>
    /// 查询登录日志列表
    /// </summary>
    public async Task<PagedResult<ResultLoginLog>> GetLoginLogsAsync(ArgQueryLoginLogs arg)
    {
        var offset = (arg.PageIndex - 1) * arg.PageSize;

        var logs = await _db.QueryAsync<LoginLogEntity>(DashboardSql.GetLoginLogs, new
        {
            arg.LoginType,
            arg.IsSuccess,
            arg.IsAbnormal,
            arg.Keyword,
            arg.StartTime,
            arg.EndTime,
            Limit = arg.PageSize,
            Offset = offset
        });

        var total = await _db.ExecuteScalarAsync<int>(DashboardSql.GetLoginLogCount, new
        {
            arg.LoginType,
            arg.IsSuccess,
            arg.IsAbnormal,
            arg.Keyword,
            arg.StartTime,
            arg.EndTime
        });

        return new PagedResult<ResultLoginLog>
        {
            Items = logs.Select(MapToLoginLogResult).ToList(),
            TotalCount = total,
            PageIndex = arg.PageIndex,
            PageSize = arg.PageSize
        };
    }

    /// <summary>
    /// 获取用户最近登录记录
    /// </summary>
    public async Task<IReadOnlyList<ResultLoginLog>> GetRecentLoginsByUserIdAsync(Guid userId, int limit = 10)
    {
        var logs = await _db.QueryAsync<LoginLogEntity>(
            DashboardSql.GetRecentLoginsByUserId,
            new { UserId = userId, Limit = limit });

        return logs.Select(MapToLoginLogResult).ToList();
    }

    #endregion

    #region Mapping

    private static ResultOperationLog MapToOperationLogResult(OperationLogEntity entity) => new()
    {
        Id = entity.Id,
        OperatorId = entity.OperatorId,
        OperatorName = entity.OperatorName,
        Module = entity.Module,
        Action = entity.Action,
        Description = entity.Description,
        RequestMethod = entity.RequestMethod,
        RequestPath = entity.RequestPath,
        RequestBody = entity.RequestBody,
        ResponseBody = entity.ResponseBody,
        ClientIp = entity.ClientIp,
        StatusCode = entity.StatusCode,
        ExecutionTime = entity.ExecutionTime,
        CreatedAt = entity.CreatedAt
    };

    private static ResultExceptionLog MapToExceptionLogResult(ExceptionLogEntity entity) => new()
    {
        Id = entity.Id,
        Module = entity.Module,
        ExceptionType = entity.ExceptionType,
        Message = entity.Message,
        StackTrace = entity.StackTrace,
        RequestPath = entity.RequestPath,
        RequestBody = entity.RequestBody,
        ClientIp = entity.ClientIp,
        Status = entity.Status,
        Solution = entity.Solution,
        HandlerId = entity.HandlerId,
        HandledAt = entity.HandledAt,
        CreatedAt = entity.CreatedAt
    };

    private static ResultLoginLog MapToLoginLogResult(LoginLogEntity entity) => new()
    {
        Id = entity.Id,
        UserId = entity.UserId,
        Username = entity.Username,
        LoginType = entity.LoginType,
        ClientIp = entity.ClientIp,
        Location = entity.Location,
        DeviceType = entity.DeviceType,
        Browser = entity.Browser,
        Os = entity.Os,
        IsSuccess = entity.IsSuccess,
        FailReason = entity.FailReason,
        IsAbnormal = entity.IsAbnormal,
        AbnormalReason = entity.AbnormalReason,
        CreatedAt = entity.CreatedAt
    };

    #endregion
}
