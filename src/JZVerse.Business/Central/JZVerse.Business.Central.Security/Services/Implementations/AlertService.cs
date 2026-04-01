using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using JZVerse.Business.Central.Security.Database;
using JZVerse.Business.Central.Security.Database.Models;
using JZVerse.Business.Central.Security.Services.Interfaces;
using JZVerse.MicroHuaxia.DataAccess.Abstractions;
using Microsoft.Extensions.Logging;

namespace JZVerse.Business.Central.Security.Services.Implementations;

/// <summary>
/// 告警服务实现
/// </summary>
public sealed class AlertService : IAlertService
{
    private readonly IDbExecutor _db;
    private readonly ILogger<AlertService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    // 告警规则内存缓存
    private readonly ConcurrentDictionary<Guid, AlertRule> _ruleCache = new();
    private readonly ConcurrentDictionary<string, DateTime> _ruleCooldownCache = new();

    public AlertService(
        IDbExecutor db,
        ILogger<AlertService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    #region 告警记录管理

    /// <inheritdoc />
    public async Task<SecurityAlert> CreateAlertAsync(CreateAlertRequest alert)
    {
        try
        {
            var securityAlert = new SecurityAlert
            {
                Id = Guid.NewGuid(),
                AlertType = alert.AlertType,
                UserId = alert.UserId,
                Title = alert.Title,
                Content = alert.Content,
                Severity = alert.Severity,
                RelatedData = alert.RelatedData != null ? JsonSerializer.Serialize(alert.RelatedData) : null,
                SourceIp = alert.SourceIp,
                Status = AlertStatuses.Pending,
                IsPushed = false,
                CreatedAt = DateTime.UtcNow
            };

            await _db.ExecuteAsync(SecuritySql.InsertAlert, securityAlert);

            _logger.LogInformation(
                "告警已创建 - Id: {Id}, 类型: {AlertType}, 级别: {Severity}, 标题: {Title}",
                securityAlert.Id, securityAlert.AlertType, securityAlert.Severity, securityAlert.Title);

            // 自动推送
            if (alert.AutoPush)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await PushAlertAsync(securityAlert);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "自动推送告警失败 - AlertId: {AlertId}", securityAlert.Id);
                    }
                });
            }

            return securityAlert;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建告警失败");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<SecurityAlert> CreateAlertAsync(
        string alertType,
        string severity,
        string title,
        string content,
        Guid? userId = null,
        string? sourceIp = null,
        object? relatedData = null)
    {
        var request = new CreateAlertRequest
        {
            AlertType = alertType,
            Severity = severity,
            Title = title,
            Content = content,
            UserId = userId,
            SourceIp = sourceIp,
            RelatedData = relatedData,
            AutoPush = true
        };

        return await CreateAlertAsync(request);
    }

    /// <inheritdoc />
    public async Task<(List<SecurityAlert> Alerts, int Total)> QueryAlertsAsync(AlertQuery query)
    {
        try
        {
            var offset = (query.PageIndex - 1) * query.PageSize;

            var parameters = new
            {
                AlertType = query.AlertType,
                Severity = query.Severity,
                Status = query.Status,
                UserId = query.UserId,
                IsPushed = query.IsPushed,
                StartTime = query.StartTime,
                EndTime = query.EndTime,
                Limit = query.PageSize,
                Offset = offset
            };

            var alerts = await _db.QueryAsync<SecurityAlert>(SecuritySql.QueryAlerts, parameters);
            var total = await _db.ExecuteScalarAsync<int>(SecuritySql.CountAlerts, parameters);

            return (alerts.ToList(), total);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查询告警列表失败");
            return (new List<SecurityAlert>(), 0);
        }
    }

    /// <inheritdoc />
    public async Task<SecurityAlert?> GetAlertByIdAsync(Guid id)
    {
        try
        {
            const string sql = "SELECT * FROM security_alerts WHERE id = @Id LIMIT 1";
            return await _db.QueryFirstOrDefaultAsync<SecurityAlert>(sql, new { Id = id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取告警详情失败 - Id: {Id}", id);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> HandleAlertAsync(Guid id, string handler, string? remark = null, string status = AlertStatuses.Resolved)
    {
        try
        {
            var affected = await _db.ExecuteAsync(SecuritySql.UpdateAlertStatus, new
            {
                Id = id,
                Status = status,
                Handler = handler,
                HandleRemark = remark,
                ResolvedAt = DateTime.UtcNow
            });

            _logger.LogInformation(
                "告警已处理 - Id: {Id}, 处理人: {Handler}, 状态: {Status}",
                id, handler, status);

            return affected > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理告警失败 - Id: {Id}", id);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> IgnoreAlertAsync(Guid id, string handler, string? remark = null)
    {
        return await HandleAlertAsync(id, handler, remark, AlertStatuses.Ignored);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAlertAsync(Guid id)
    {
        try
        {
            const string sql = "DELETE FROM security_alerts WHERE id = @Id";
            var affected = await _db.ExecuteAsync(sql, new { Id = id });

            _logger.LogInformation("告警已删除 - Id: {Id}", id);
            return affected > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除告警失败 - Id: {Id}", id);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<int> GetPendingCountAsync()
    {
        try
        {
            const string sql = @"
                SELECT COUNT(*) FROM security_alerts 
                WHERE status IN (@Pending, @Processing)";

            return await _db.ExecuteScalarAsync<int>(sql, new
            {
                Pending = AlertStatuses.Pending,
                Processing = AlertStatuses.Processing
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取待处理告警数量失败");
            return 0;
        }
    }

    #endregion

    #region 告警规则

    /// <inheritdoc />
    public async Task<(bool ShouldAlert, List<AlertRule> MatchedRules)> CheckAlertRulesAsync(AlertContext context)
    {
        var matchedRules = new List<AlertRule>();

        try
        {
            // 从缓存获取规则
            var rules = await GetAlertRulesAsync();

            foreach (var rule in rules.Where(r => r.IsEnabled))
            {
                // 检查冷却时间
                var cooldownKey = $"{rule.Id}:{context.SourceIp}";
                if (_ruleCooldownCache.TryGetValue(cooldownKey, out var lastTriggered))
                {
                    if (DateTime.UtcNow - lastTriggered < TimeSpan.FromMinutes(rule.CooldownMinutes))
                    {
                        continue; // 冷却中
                    }
                }

                // 规则匹配
                if (MatchRule(rule, context))
                {
                    matchedRules.Add(rule);
                    _ruleCooldownCache[cooldownKey] = DateTime.UtcNow;
                }
            }

            return (matchedRules.Count > 0, matchedRules);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查告警规则失败");
            return (false, matchedRules);
        }
    }

    private static bool MatchRule(AlertRule rule, AlertContext context)
    {
        // 解析条件（简单的键值对条件，如：EventType=AbnormalLogin;FailedAttempts>=5）
        var conditions = rule.Condition.Split(';', StringSplitOptions.RemoveEmptyEntries);

        foreach (var condition in conditions)
        {
            var parts = condition.Split(new[] { "=", ">=", "<=", ">", "<" }, StringSplitOptions.None);
            if (parts.Length < 2) continue;

            var key = parts[0].Trim();
            var value = parts[1].Trim();

            var match = key.ToLowerInvariant() switch
            {
                "eventtype" => context.EventType.Equals(value, StringComparison.OrdinalIgnoreCase),
                "sourceip" => context.SourceIp.Equals(value, StringComparison.OrdinalIgnoreCase),
                "userid" => context.UserId?.ToString() == value,
                "failedattempts" => int.TryParse(value, out var threshold) && context.FailedAttempts >= threshold,
                "requestpath" => context.RequestPath?.Contains(value) ?? false,
                _ => true
            };

            if (!match) return false;
        }

        return true;
    }

    /// <inheritdoc />
    public async Task<AlertRule> AddAlertRuleAsync(AlertRule rule)
    {
        try
        {
            rule.Id = Guid.NewGuid();
            rule.CreatedAt = DateTime.UtcNow;

            const string sql = @"
                INSERT INTO security_alert_rules 
                (id, rule_name, alert_type, severity, condition, title, content, cooldown_minutes, is_enabled, created_at)
                VALUES 
                (@Id, @RuleName, @AlertType, @Severity, @Condition, @Title, @Content, @CooldownMinutes, @IsEnabled, @CreatedAt)";

            await _db.ExecuteAsync(sql, rule);

            // 更新缓存
            _ruleCache[rule.Id] = rule;

            _logger.LogInformation("告警规则已添加 - Id: {Id}, 名称: {RuleName}", rule.Id, rule.RuleName);

            return rule;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "添加告警规则失败");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> UpdateAlertRuleAsync(AlertRule rule)
    {
        try
        {
            const string sql = @"
                UPDATE security_alert_rules 
                SET rule_name = @RuleName, alert_type = @AlertType, severity = @Severity,
                    condition = @Condition, title = @Title, content = @Content,
                    cooldown_minutes = @CooldownMinutes, is_enabled = @IsEnabled
                WHERE id = @Id";

            var affected = await _db.ExecuteAsync(sql, rule);

            if (affected > 0)
            {
                // 更新缓存
                _ruleCache[rule.Id] = rule;
            }

            _logger.LogInformation("告警规则已更新 - Id: {Id}", rule.Id);
            return affected > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新告警规则失败 - Id: {Id}", rule.Id);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAlertRuleAsync(Guid id)
    {
        try
        {
            const string sql = "DELETE FROM security_alert_rules WHERE id = @Id";
            var affected = await _db.ExecuteAsync(sql, new { Id = id });

            // 移除缓存
            _ruleCache.TryRemove(id, out _);

            _logger.LogInformation("告警规则已删除 - Id: {Id}", id);
            return affected > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除告警规则失败 - Id: {Id}", id);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<List<AlertRule>> GetAlertRulesAsync()
    {
        try
        {
            // 如果缓存为空，从数据库加载
            if (_ruleCache.IsEmpty)
            {
                const string sql = "SELECT * FROM security_alert_rules ORDER BY created_at DESC";
                var rules = await _db.QueryAsync<AlertRule>(sql);

                foreach (var rule in rules)
                {
                    _ruleCache[rule.Id] = rule;
                }
            }

            return _ruleCache.Values.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取告警规则列表失败");
            return new List<AlertRule>();
        }
    }

    #endregion

    #region 推送渠道

    /// <inheritdoc />
    public async Task<PushChannel> AddPushChannelAsync(CreatePushChannelRequest channel)
    {
        try
        {
            var pushChannel = new PushChannel
            {
                Id = Guid.NewGuid(),
                ChannelType = channel.ChannelType,
                ChannelName = channel.ChannelName,
                ConfigJson = channel.ConfigJson,
                IsEnabled = true,
                SupportedSeverities = channel.SupportedSeverities,
                SupportedAlertTypes = channel.SupportedAlertTypes,
                DailyPushLimit = channel.DailyPushLimit,
                Description = channel.Description,
                TodayPushCount = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _db.ExecuteAsync(SecuritySql.InsertPushChannel, pushChannel);

            _logger.LogInformation(
                "推送渠道已添加 - Id: {Id}, 名称: {ChannelName}, 类型: {ChannelType}",
                pushChannel.Id, pushChannel.ChannelName, pushChannel.ChannelType);

            return pushChannel;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "添加推送渠道失败");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> UpdatePushChannelAsync(Guid id, UpdatePushChannelRequest channel)
    {
        try
        {
            var parameters = new
            {
                Id = id,
                channel.ChannelName,
                channel.ConfigJson,
                channel.IsEnabled,
                channel.SupportedSeverities,
                channel.SupportedAlertTypes,
                channel.DailyPushLimit,
                channel.Description,
                UpdatedAt = DateTime.UtcNow
            };

            var affected = await _db.ExecuteAsync(SecuritySql.UpdatePushChannel, parameters);

            _logger.LogInformation("推送渠道已更新 - Id: {Id}", id);
            return affected > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新推送渠道失败 - Id: {Id}", id);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeletePushChannelAsync(Guid id)
    {
        try
        {
            var affected = await _db.ExecuteAsync(SecuritySql.DeletePushChannel, new { Id = id });

            _logger.LogInformation("推送渠道已删除 - Id: {Id}", id);
            return affected > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除推送渠道失败 - Id: {Id}", id);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<List<PushChannel>> GetPushChannelsAsync(bool includeDisabled = false)
    {
        try
        {
            string sql;
            if (includeDisabled)
            {
                sql = "SELECT * FROM security_push_channels ORDER BY created_at DESC";
            }
            else
            {
                sql = SecuritySql.GetAllEnabledPushChannels;
            }

            var channels = await _db.QueryAsync<PushChannel>(sql);
            return channels.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取推送渠道列表失败");
            return new List<PushChannel>();
        }
    }

    /// <inheritdoc />
    public async Task<PushTestResult> TestPushChannelAsync(Guid id)
    {
        try
        {
            var channel = await _db.QueryFirstOrDefaultAsync<PushChannel>(
                SecuritySql.GetPushChannelById, new { Id = id });

            if (channel == null)
            {
                return new PushTestResult
                {
                    Success = false,
                    ErrorMessage = "推送渠道不存在"
                };
            }

            var testMessage = new PushMessage
            {
                Title = "测试消息",
                Content = $"这是一条来自 {channel.ChannelName} 的测试消息",
                Url = null
            };

            var result = await PushToChannelAsync(id, testMessage);

            return new PushTestResult
            {
                Success = result.Success,
                ErrorMessage = result.ErrorMessage,
                Response = result.MessageId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "测试推送渠道失败 - Id: {Id}", id);
            return new PushTestResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    #endregion

    #region 告警推送

    /// <inheritdoc />
    public async Task<PushResult> PushAlertAsync(SecurityAlert alert)
    {
        try
        {
            // 获取所有启用的推送渠道
            var channels = await GetPushChannelsAsync();
            if (channels.Count == 0)
            {
                return new PushResult
                {
                    Success = false,
                    ErrorMessage = "没有可用的推送渠道"
                };
            }

            // 过滤支持该告警级别和类型的渠道
            var eligibleChannels = channels.Where(c =>
                IsSeveritySupported(c, alert.Severity) &&
                IsAlertTypeSupported(c, alert.AlertType) &&
                (c.DailyPushLimit == 0 || c.TodayPushCount < c.DailyPushLimit)).ToList();

            if (eligibleChannels.Count == 0)
            {
                return new PushResult
                {
                    Success = false,
                    ErrorMessage = "没有符合条件的推送渠道"
                };
            }

            // 构建推送消息
            var message = new PushMessage
            {
                Title = alert.Title,
                Content = alert.Content,
                Url = $"/security/alerts/{alert.Id}",
                Extra = new Dictionary<string, string>
                {
                    { "AlertId", alert.Id.ToString() },
                    { "AlertType", alert.AlertType },
                    { "Severity", alert.Severity },
                    { "CreatedAt", alert.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss") }
                }
            };

            // 推送到所有符合条件的渠道
            var successChannels = new List<string>();
            var errors = new List<string>();

            foreach (var channel in eligibleChannels)
            {
                try
                {
                    var result = await PushToChannelInternalAsync(channel, message);
                    if (result.Success)
                    {
                        successChannels.Add(channel.ChannelName);

                        // 更新推送计数
                        await _db.ExecuteAsync(SecuritySql.IncrementPushCount, new
                        {
                            Id = channel.Id,
                            LastPushAt = DateTime.UtcNow
                        });
                    }
                    else
                    {
                        errors.Add($"{channel.ChannelName}: {result.ErrorMessage}");
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"{channel.ChannelName}: {ex.Message}");
                }
            }

            // 更新告警推送状态
            if (successChannels.Count > 0)
            {
                await _db.ExecuteAsync(SecuritySql.MarkAlertPushed, new
                {
                    Id = alert.Id,
                    PushedAt = DateTime.UtcNow,
                    PushChannels = string.Join(",", successChannels)
                });
            }

            return new PushResult
            {
                Success = successChannels.Count > 0,
                ErrorMessage = errors.Count > 0 ? string.Join("; ", errors) : null,
                MessageId = successChannels.Count > 0 ? string.Join(",", successChannels) : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "推送告警失败 - AlertId: {AlertId}", alert.Id);
            return new PushResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <inheritdoc />
    public async Task<BatchPushResult> PushAlertsAsync(List<Guid> alertIds)
    {
        var result = new BatchPushResult
        {
            Total = alertIds.Count,
            Success = 0,
            Failed = 0,
            Errors = new List<string>()
        };

        foreach (var alertId in alertIds)
        {
            try
            {
                var alert = await GetAlertByIdAsync(alertId);
                if (alert == null)
                {
                    result.Failed++;
                    result.Errors.Add($"告警不存在: {alertId}");
                    continue;
                }

                var pushResult = await PushAlertAsync(alert);
                if (pushResult.Success)
                {
                    result.Success++;
                }
                else
                {
                    result.Failed++;
                    result.Errors.Add($"{alertId}: {pushResult.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Errors.Add($"{alertId}: {ex.Message}");
            }
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<PushResult> PushToChannelAsync(Guid channelId, PushMessage message)
    {
        try
        {
            var channel = await _db.QueryFirstOrDefaultAsync<PushChannel>(
                SecuritySql.GetPushChannelById, new { Id = channelId });

            if (channel == null)
            {
                return new PushResult
                {
                    Success = false,
                    ErrorMessage = "推送渠道不存在"
                };
            }

            if (!channel.IsEnabled)
            {
                return new PushResult
                {
                    Success = false,
                    ErrorMessage = "推送渠道已禁用"
                };
            }

            return await PushToChannelInternalAsync(channel, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "推送到指定渠道失败 - ChannelId: {ChannelId}", channelId);
            return new PushResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<PushResult> PushToChannelInternalAsync(PushChannel channel, PushMessage message)
    {
        return channel.ChannelType.ToLowerInvariant() switch
        {
            ChannelTypes.WeChatWork => await PushToWeChatWorkAsync(channel, message),
            ChannelTypes.DingTalk => await PushToDingTalkAsync(channel, message),
            ChannelTypes.FeiShu => await PushToFeiShuAsync(channel, message),
            ChannelTypes.Webhook => await PushToWebhookAsync(channel, message),
            _ => new PushResult { Success = false, ErrorMessage = $"不支持的渠道类型: {channel.ChannelType}" }
        };
    }

    /// <summary>
    /// 推送到企业微信
    /// </summary>
    private async Task<PushResult> PushToWeChatWorkAsync(PushChannel channel, PushMessage message)
    {
        try
        {
            var config = JsonSerializer.Deserialize<WeChatWorkConfig>(channel.ConfigJson);
            if (config == null || string.IsNullOrEmpty(config.WebhookKey))
            {
                return new PushResult { Success = false, ErrorMessage = "企业微信配置无效" };
            }

            var client = _httpClientFactory.CreateClient();
            var webhookUrl = $"https://qyapi.weixin.qq.com/cgi-bin/webhook/send?key={config.WebhookKey}";

            // 构建企业微信markdown消息
            var payload = new
            {
                msgtype = "markdown",
                markdown = new
                {
                    content = $"## {message.Title}\n\n{message.Content}\n\n" +
                              $"> **时间**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                              (message.Url != null ? $"> [查看详情]({message.Url})" : "")
                }
            };

            var response = await client.PostAsJsonAsync(webhookUrl, payload);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<JsonElement>(responseContent);
                if (result.GetProperty("errcode").GetInt32() == 0)
                {
                    return new PushResult { Success = true, MessageId = result.GetProperty("msgid").GetString() };
                }
                else
                {
                    return new PushResult
                    {
                        Success = false,
                        ErrorMessage = $"企业微信API返回错误: {result.GetProperty("errmsg").GetString()}"
                    };
                }
            }

            return new PushResult
            {
                Success = false,
                ErrorMessage = $"HTTP错误: {response.StatusCode}, {responseContent}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "推送到企业微信失败");
            return new PushResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <summary>
    /// 推送到钉钉
    /// </summary>
    private async Task<PushResult> PushToDingTalkAsync(PushChannel channel, PushMessage message)
    {
        try
        {
            var config = JsonSerializer.Deserialize<DingTalkConfig>(channel.ConfigJson);
            if (config == null || string.IsNullOrEmpty(config.WebhookToken))
            {
                return new PushResult { Success = false, ErrorMessage = "钉钉配置无效" };
            }

            var client = _httpClientFactory.CreateClient();

            // 钉钉需要签名
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var sign = GenerateDingTalkSign(config.Secret ?? "", timestamp);

            var webhookUrl = $"https://oapi.dingtalk.com/robot/send?access_token={config.WebhookToken}&timestamp={timestamp}&sign={sign}";

            // 构建钉钉markdown消息
            var payload = new
            {
                msgtype = "markdown",
                markdown = new
                {
                    title = message.Title,
                    text = $"### {message.Title}\n\n{message.Content}\n\n" +
                           $"> 时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                           (message.Url != null ? $"> [查看详情]({message.Url})" : "")
                }
            };

            var response = await client.PostAsJsonAsync(webhookUrl, payload);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<JsonElement>(responseContent);
                if (result.GetProperty("errcode").GetInt32() == 0)
                {
                    return new PushResult { Success = true };
                }
                else
                {
                    return new PushResult
                    {
                        Success = false,
                        ErrorMessage = $"钉钉API返回错误: {result.GetProperty("errmsg").GetString()}"
                    };
                }
            }

            return new PushResult
            {
                Success = false,
                ErrorMessage = $"HTTP错误: {response.StatusCode}, {responseContent}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "推送到钉钉失败");
            return new PushResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private static string GenerateDingTalkSign(string secret, long timestamp)
    {
        var stringToSign = $"{timestamp}\n{secret}";
        using var hmac = new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign));
        return Uri.EscapeDataString(Convert.ToBase64String(hash));
    }

    /// <summary>
    /// 推送到飞书
    /// </summary>
    private async Task<PushResult> PushToFeiShuAsync(PushChannel channel, PushMessage message)
    {
        try
        {
            var config = JsonSerializer.Deserialize<FeiShuConfig>(channel.ConfigJson);
            if (config == null || string.IsNullOrEmpty(config.WebhookToken))
            {
                return new PushResult { Success = false, ErrorMessage = "飞书配置无效" };
            }

            var client = _httpClientFactory.CreateClient();
            var webhookUrl = $"https://open.feishu.cn/open-apis/bot/v2/hook/{config.WebhookToken}";

            // 构建飞书富文本消息
            var payload = new
            {
                msg_type = "post",
                content = new
                {
                    post = new
                    {
                        zh_cn = new
                        {
                            title = message.Title,
                            content = new[]
                            {
                                new[]
                                {
                                    new { tag = "text", text = message.Content },
                                    new { tag = "text", text = $"\n\n时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}" }
                                }
                            }
                        }
                    }
                }
            };

            var response = await client.PostAsJsonAsync(webhookUrl, payload);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<JsonElement>(responseContent);
                if (result.GetProperty("code").GetInt32() == 0)
                {
                    return new PushResult { Success = true };
                }
                else
                {
                    return new PushResult
                    {
                        Success = false,
                        ErrorMessage = $"飞书API返回错误: {result.GetProperty("msg").GetString()}"
                    };
                }
            }

            return new PushResult
            {
                Success = false,
                ErrorMessage = $"HTTP错误: {response.StatusCode}, {responseContent}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "推送到飞书失败");
            return new PushResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <summary>
    /// 推送到通用Webhook
    /// </summary>
    private async Task<PushResult> PushToWebhookAsync(PushChannel channel, PushMessage message)
    {
        try
        {
            // 从配置中解析webhook URL
            var config = JsonSerializer.Deserialize<Dictionary<string, string>>(channel.ConfigJson);
            if (config == null || !config.TryGetValue("url", out var webhookUrl) || string.IsNullOrEmpty(webhookUrl))
            {
                return new PushResult { Success = false, ErrorMessage = "Webhook配置无效" };
            }

            var client = _httpClientFactory.CreateClient();

            var payload = new
            {
                title = message.Title,
                content = message.Content,
                url = message.Url,
                extra = message.Extra,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            var response = await client.PostAsJsonAsync(webhookUrl, payload);
            var responseContent = await response.Content.ReadAsStringAsync();

            return new PushResult
            {
                Success = response.IsSuccessStatusCode,
                MessageId = responseContent,
                ErrorMessage = response.IsSuccessStatusCode ? null : $"HTTP错误: {response.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "推送到Webhook失败");
            return new PushResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private static bool IsSeveritySupported(PushChannel channel, string severity)
    {
        if (string.IsNullOrEmpty(channel.SupportedSeverities))
        {
            return true;
        }

        var supportedList = channel.SupportedSeverities.Split(',', StringSplitOptions.RemoveEmptyEntries);
        return supportedList.Any(s => s.Trim().Equals(severity, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsAlertTypeSupported(PushChannel channel, string alertType)
    {
        if (string.IsNullOrEmpty(channel.SupportedAlertTypes))
        {
            return true;
        }

        var supportedList = channel.SupportedAlertTypes.Split(',', StringSplitOptions.RemoveEmptyEntries);
        return supportedList.Any(s => s.Trim().Equals(alertType, StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region 统计

    /// <inheritdoc />
    public async Task<AlertStatistics> GetStatisticsAsync(DateTime startTime, DateTime endTime)
    {
        try
        {
            const string sql = @"
                SELECT 
                    COUNT(*) AS total,
                    COUNT(*) FILTER (WHERE status IN ('Pending', 'Processing')) AS pending,
                    COUNT(*) FILTER (WHERE status = 'Resolved') AS resolved,
                    COUNT(*) FILTER (WHERE status = 'Ignored') AS ignored,
                    alert_type AS type,
                    severity
                FROM security_alerts
                WHERE created_at >= @StartTime AND created_at <= @EndTime
                GROUP BY alert_type, severity";

            var results = await _db.QueryAsync<AlertStatRow>(sql, new
            {
                StartTime = startTime,
                EndTime = endTime
            });

            var stats = new AlertStatistics
            {
                ByType = new Dictionary<string, int>(),
                BySeverity = new Dictionary<string, int>()
            };

            foreach (var row in results)
            {
                stats.Total += row.Total;
                stats.Pending += row.Pending;
                stats.Resolved += row.Resolved;
                stats.Ignored += row.Ignored;

                if (!string.IsNullOrEmpty(row.Type))
                {
                    stats.ByType[row.Type] = stats.ByType.GetValueOrDefault(row.Type) + row.Total;
                }

                if (!string.IsNullOrEmpty(row.Severity))
                {
                    stats.BySeverity[row.Severity] = stats.BySeverity.GetValueOrDefault(row.Severity) + row.Total;
                }
            }

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取告警统计失败");
            return new AlertStatistics
            {
                ByType = new Dictionary<string, int>(),
                BySeverity = new Dictionary<string, int>()
            };
        }
    }

    private class AlertStatRow
    {
        public int Total { get; set; }
        public int Pending { get; set; }
        public int Resolved { get; set; }
        public int Ignored { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
    }

    #endregion

    #region 控制器所需方法

    /// <inheritdoc />
    public async Task<(List<SecurityAlert> Items, int Total)> GetAlertsAsync(
        string? alertType,
        string? severity,
        string? status,
        Guid? userId,
        DateTime? startTime,
        DateTime? endTime,
        int pageIndex,
        int pageSize)
    {
        var query = new AlertQuery
        {
            AlertType = alertType,
            Severity = severity,
            Status = status,
            UserId = userId,
            StartTime = startTime,
            EndTime = endTime,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
        return await QueryAlertsAsync(query);
    }

    /// <inheritdoc />
    public async Task<List<SecurityAlert>> GetPendingAlertsAsync(int count)
    {
        try
        {
            const string sql = @"
                SELECT * FROM security_alerts 
                WHERE status = @Pending
                ORDER BY created_at DESC
                LIMIT @Count";

            var alerts = await _db.QueryAsync<SecurityAlert>(sql, new
            {
                Pending = AlertStatuses.Pending,
                Count = count
            });
            return alerts.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取待处理告警列表失败");
            return new List<SecurityAlert>();
        }
    }

    /// <inheritdoc />
    public async Task<bool> ProcessAlertAsync(Guid id, string processor, string action, string? remark)
    {
        return await HandleAlertAsync(id, processor, remark, AlertStatuses.Resolved);
    }

    /// <inheritdoc />
    public async Task<bool> IgnoreAlertAsync(Guid id, string reason)
    {
        return await HandleAlertAsync(id, "System", reason, AlertStatuses.Ignored);
    }

    /// <inheritdoc />
    public async Task<bool> UpdateAlertRuleAsync(string ruleId, UpdateAlertRuleConfigRequest config)
    {
        try
        {
            if (!Guid.TryParse(ruleId, out var id))
            {
                return false;
            }

            const string sql = @"
                UPDATE security_alert_rules 
                SET cooldown_minutes = @CooldownMinutes,
                    is_enabled = @IsEnabled
                WHERE id = @Id";

            var affected = await _db.ExecuteAsync(sql, new
            {
                Id = id,
                CooldownMinutes = config.CooldownMinutes,
                IsEnabled = config.IsEnabled
            });

            // 更新缓存
            if (_ruleCache.TryGetValue(id, out var rule))
            {
                rule.CooldownMinutes = config.CooldownMinutes;
                rule.IsEnabled = config.IsEnabled;
            }

            return affected > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新告警规则配置失败 - RuleId: {RuleId}", ruleId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task ProcessAndPushAlertAsync(SecurityAlert alert)
    {
        // 保存告警到数据库
        await _db.ExecuteAsync(SecuritySql.InsertAlert, alert);

        // 推送告警
        await PushAlertAsync(alert);
    }

    #endregion
}
