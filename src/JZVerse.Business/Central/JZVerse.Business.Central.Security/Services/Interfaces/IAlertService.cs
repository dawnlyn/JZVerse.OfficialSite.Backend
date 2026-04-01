using JZVerse.Business.Central.Security.Database.Models;

namespace JZVerse.Business.Central.Security.Services.Interfaces;

/// <summary>
/// 告警服务接口
/// </summary>
public interface IAlertService
{
    #region 告警记录管理

    /// <summary>
    /// 创建告警
    /// </summary>
    /// <param name="alert">告警信息</param>
    /// <returns>创建后的告警（包含ID）</returns>
    Task<SecurityAlert> CreateAlertAsync(CreateAlertRequest alert);

    /// <summary>
    /// 快速创建告警
    /// </summary>
    /// <param name="alertType">告警类型</param>
    /// <param name="severity">严重级别</param>
    /// <param name="title">标题</param>
    /// <param name="content">内容</param>
    /// <param name="userId">关联用户ID</param>
    /// <param name="sourceIp">来源IP</param>
    /// <param name="relatedData">关联数据</param>
    /// <returns>创建后的告警</returns>
    Task<SecurityAlert> CreateAlertAsync(
        string alertType,
        string severity,
        string title,
        string content,
        Guid? userId = null,
        string? sourceIp = null,
        object? relatedData = null);

    /// <summary>
    /// 获取告警列表
    /// </summary>
    /// <param name="query">查询条件</param>
    /// <returns>告警列表和总数</returns>
    Task<(List<SecurityAlert> Alerts, int Total)> QueryAlertsAsync(AlertQuery query);

    /// <summary>
    /// 获取告警列表（简化版）
    /// </summary>
    Task<(List<SecurityAlert> Items, int Total)> GetAlertsAsync(
        string? alertType,
        string? severity,
        string? status,
        Guid? userId,
        DateTime? startTime,
        DateTime? endTime,
        int pageIndex,
        int pageSize);

    /// <summary>
    /// 获取单条告警详情
    /// </summary>
    /// <param name="id">告警ID</param>
    /// <returns>告警信息</returns>
    Task<SecurityAlert?> GetAlertByIdAsync(Guid id);

    /// <summary>
    /// 处理告警
    /// </summary>
    /// <param name="id">告警ID</param>
    /// <param name="handler">处理人</param>
    /// <param name="remark">处理备注</param>
    /// <param name="status">处理后状态</param>
    /// <returns>是否成功</returns>
    Task<bool> HandleAlertAsync(Guid id, string handler, string? remark = null, string status = AlertStatuses.Resolved);

    /// <summary>
    /// 处理告警（带动作参数）
    /// </summary>
    Task<bool> ProcessAlertAsync(Guid id, string processor, string action, string? remark);

    /// <summary>
    /// 忽略告警
    /// </summary>
    /// <param name="id">告警ID</param>
    /// <param name="handler">处理人</param>
    /// <param name="remark">备注</param>
    /// <returns>是否成功</returns>
    Task<bool> IgnoreAlertAsync(Guid id, string handler, string? remark = null);

    /// <summary>
    /// 忽略告警（简化版）
    /// </summary>
    Task<bool> IgnoreAlertAsync(Guid id, string reason);

    /// <summary>
    /// 删除告警
    /// </summary>
    /// <param name="id">告警ID</param>
    /// <returns>是否成功</returns>
    Task<bool> DeleteAlertAsync(Guid id);

    /// <summary>
    /// 获取待处理告警数量
    /// </summary>
    /// <returns>数量</returns>
    Task<int> GetPendingCountAsync();

    /// <summary>
    /// 获取待处理告警列表
    /// </summary>
    Task<List<SecurityAlert>> GetPendingAlertsAsync(int count);

    #endregion

    #region 告警规则

    /// <summary>
    /// 检查是否需要触发告警
    /// </summary>
    /// <param name="context">告警上下文</param>
    /// <returns>是否需要告警及告警配置</returns>
    Task<(bool ShouldAlert, List<AlertRule> MatchedRules)> CheckAlertRulesAsync(AlertContext context);

    /// <summary>
    /// 添加告警规则
    /// </summary>
    /// <param name="rule">规则</param>
    /// <returns>添加后的规则</returns>
    Task<AlertRule> AddAlertRuleAsync(AlertRule rule);

    /// <summary>
    /// 更新告警规则
    /// </summary>
    /// <param name="rule">规则</param>
    /// <returns>是否成功</returns>
    Task<bool> UpdateAlertRuleAsync(AlertRule rule);

    /// <summary>
    /// 更新告警规则（简化版）
    /// </summary>
    Task<bool> UpdateAlertRuleAsync(string ruleId, UpdateAlertRuleConfigRequest config);

    /// <summary>
    /// 删除告警规则
    /// </summary>
    /// <param name="id">规则ID</param>
    /// <returns>是否成功</returns>
    Task<bool> DeleteAlertRuleAsync(Guid id);

    /// <summary>
    /// 获取所有告警规则
    /// </summary>
    /// <returns>规则列表</returns>
    Task<List<AlertRule>> GetAlertRulesAsync();

    #endregion

    #region 推送渠道

    /// <summary>
    /// 添加推送渠道
    /// </summary>
    /// <param name="channel">渠道配置</param>
    /// <returns>添加后的渠道</returns>
    Task<PushChannel> AddPushChannelAsync(CreatePushChannelRequest channel);

    /// <summary>
    /// 更新推送渠道
    /// </summary>
    /// <param name="id">渠道ID</param>
    /// <param name="channel">渠道配置</param>
    /// <returns>是否成功</returns>
    Task<bool> UpdatePushChannelAsync(Guid id, UpdatePushChannelRequest channel);

    /// <summary>
    /// 删除推送渠道
    /// </summary>
    /// <param name="id">渠道ID</param>
    /// <returns>是否成功</returns>
    Task<bool> DeletePushChannelAsync(Guid id);

    /// <summary>
    /// 获取推送渠道列表
    /// </summary>
    /// <param name="includeDisabled">是否包含禁用的渠道</param>
    /// <returns>渠道列表</returns>
    Task<List<PushChannel>> GetPushChannelsAsync(bool includeDisabled = false);

    /// <summary>
    /// 测试推送渠道
    /// </summary>
    /// <param name="id">渠道ID</param>
    /// <returns>测试结果</returns>
    Task<PushTestResult> TestPushChannelAsync(Guid id);

    #endregion

    #region 告警推送

    /// <summary>
    /// 推送告警
    /// </summary>
    /// <param name="alert">告警信息</param>
    /// <returns>推送结果</returns>
    Task<PushResult> PushAlertAsync(SecurityAlert alert);

    /// <summary>
    /// 批量推送告警
    /// </summary>
    /// <param name="alertIds">告警ID列表</param>
    /// <returns>推送结果</returns>
    Task<BatchPushResult> PushAlertsAsync(List<Guid> alertIds);

    /// <summary>
    /// 处理并推送告警
    /// </summary>
    Task ProcessAndPushAlertAsync(SecurityAlert alert);

    /// <summary>
    /// 推送消息到指定渠道
    /// </summary>
    /// <param name="channelId">渠道ID</param>
    /// <param name="message">消息内容</param>
    /// <returns>推送结果</returns>
    Task<PushResult> PushToChannelAsync(Guid channelId, PushMessage message);

    #endregion

    #region 统计

    /// <summary>
    /// 获取告警统计
    /// </summary>
    /// <param name="startTime">开始时间</param>
    /// <param name="endTime">结束时间</param>
    /// <returns>统计数据</returns>
    Task<AlertStatistics> GetStatisticsAsync(DateTime startTime, DateTime endTime);

    #endregion
}

/// <summary>
/// 创建告警请求
/// </summary>
public class CreateAlertRequest
{
    public string AlertType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public string? SourceIp { get; set; }
    public object? RelatedData { get; set; }
    public bool AutoPush { get; set; } = true;
}

/// <summary>
/// 告警查询条件
/// </summary>
public class AlertQuery
{
    public string? AlertType { get; set; }
    public string? Severity { get; set; }
    public string? Status { get; set; }
    public Guid? UserId { get; set; }
    public bool? IsPushed { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// 告警上下文
/// </summary>
public class AlertContext
{
    public string EventType { get; set; } = string.Empty;
    public string SourceIp { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public string? Username { get; set; }
    public int FailedAttempts { get; set; }
    public string? RequestPath { get; set; }
    public object? EventData { get; set; }
}

/// <summary>
/// 告警规则
/// </summary>
public class AlertRule
{
    public Guid Id { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public string AlertType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Condition { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int CooldownMinutes { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 创建推送渠道请求
/// </summary>
public class CreatePushChannelRequest
{
    public string ChannelType { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;
    public string ConfigJson { get; set; } = string.Empty;
    public string? SupportedSeverities { get; set; }
    public string? SupportedAlertTypes { get; set; }
    public int DailyPushLimit { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// 更新告警规则配置请求
/// </summary>
public class UpdateAlertRuleConfigRequest
{
    public int Threshold { get; set; }
    public int TimeWindowMinutes { get; set; }
    public int CooldownMinutes { get; set; }
    public bool IsEnabled { get; set; }
}

/// <summary>
/// 更新推送渠道请求
/// </summary>
public class UpdatePushChannelRequest
{
    public string ChannelName { get; set; } = string.Empty;
    public string ConfigJson { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string? SupportedSeverities { get; set; }
    public string? SupportedAlertTypes { get; set; }
    public int DailyPushLimit { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// 推送消息
/// </summary>
public class PushMessage
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Url { get; set; }
    public Dictionary<string, string>? Extra { get; set; }
}

/// <summary>
/// 推送结果
/// </summary>
public class PushResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? MessageId { get; set; }
}

/// <summary>
/// 批量推送结果
/// </summary>
public class BatchPushResult
{
    public int Total { get; set; }
    public int Success { get; set; }
    public int Failed { get; set; }
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// 推送测试结果
/// </summary>
public class PushTestResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Response { get; set; }
}

/// <summary>
/// 告警统计
/// </summary>
public class AlertStatistics
{
    public int Total { get; set; }
    public int Pending { get; set; }
    public int Resolved { get; set; }
    public int Ignored { get; set; }
    public Dictionary<string, int> ByType { get; set; } = new();
    public Dictionary<string, int> BySeverity { get; set; } = new();
}

/// <summary>
/// 告警状态常量
/// </summary>
public static class AlertStatuses
{
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string Resolved = "Resolved";
    public const string Ignored = "Ignored";
}
