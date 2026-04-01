namespace JZVerse.Business.Central.Security.Database;

/// <summary>
/// 安全中心数据库SQL语句
/// </summary>
public static class SecuritySql
{
    #region Token表

    public const string CreateTokenTable = @"
CREATE TABLE IF NOT EXISTS security_tokens (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL,
    token TEXT NOT NULL,
    refresh_token TEXT NOT NULL,
    expires_at TIMESTAMP NOT NULL,
    refresh_token_expires_at TIMESTAMP NOT NULL,
    is_revoked BOOLEAN DEFAULT FALSE,
    revoked_at TIMESTAMP NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    created_ip VARCHAR(50) NULL,
    user_agent TEXT NULL,
    CONSTRAINT idx_security_tokens_user_id UNIQUE (user_id, token)
);
CREATE INDEX IF NOT EXISTS idx_security_tokens_token ON security_tokens(token);
CREATE INDEX IF NOT EXISTS idx_security_tokens_refresh_token ON security_tokens(refresh_token);
CREATE INDEX IF NOT EXISTS idx_security_tokens_expires_at ON security_tokens(expires_at);
";

    public const string InsertToken = @"
INSERT INTO security_tokens (id, user_id, token, refresh_token, expires_at, refresh_token_expires_at, created_ip, user_agent, created_at)
VALUES (@Id, @UserId, @Token, @RefreshToken, @ExpiresAt, @RefreshTokenExpiresAt, @CreatedIp, @UserAgent, @CreatedAt)
";

    public const string GetTokenByValue = @"
SELECT * FROM security_tokens WHERE token = @Token LIMIT 1
";

    public const string GetTokenByRefreshToken = @"
SELECT * FROM security_tokens WHERE refresh_token = @RefreshToken LIMIT 1
";

    public const string RevokeToken = @"
UPDATE security_tokens 
SET is_revoked = TRUE, revoked_at = @RevokedAt 
WHERE token = @Token
";

    public const string DeleteExpiredTokens = @"
DELETE FROM security_tokens WHERE expires_at < @Now OR (is_revoked = TRUE AND revoked_at < @RevokedBefore)
";

    #endregion

    #region 审计日志表

    public const string CreateAuditLogTable = @"
CREATE TABLE IF NOT EXISTS security_audit_logs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NULL,
    username VARCHAR(100) NULL,
    operation_type VARCHAR(50) NOT NULL,
    module VARCHAR(50) NOT NULL,
    operation_content TEXT NOT NULL,
    request_data JSONB NULL,
    response_data JSONB NULL,
    result VARCHAR(20) NOT NULL,
    error_message TEXT NULL,
    ip_address VARCHAR(50) NOT NULL,
    user_agent TEXT NULL,
    request_path VARCHAR(500) NOT NULL,
    http_method VARCHAR(10) NOT NULL,
    duration_ms BIGINT NULL,
    is_sensitive BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX IF NOT EXISTS idx_audit_logs_user_id ON security_audit_logs(user_id);
CREATE INDEX IF NOT EXISTS idx_audit_logs_operation_type ON security_audit_logs(operation_type);
CREATE INDEX IF NOT EXISTS idx_audit_logs_module ON security_audit_logs(module);
CREATE INDEX IF NOT EXISTS idx_audit_logs_created_at ON security_audit_logs(created_at);
CREATE INDEX IF NOT EXISTS idx_audit_logs_is_sensitive ON security_audit_logs(is_sensitive);
";

    public const string InsertAuditLog = @"
INSERT INTO security_audit_logs 
(id, user_id, username, operation_type, module, operation_content, request_data, response_data, result, error_message, 
ip_address, user_agent, request_path, http_method, duration_ms, is_sensitive, created_at)
VALUES 
(@Id, @UserId, @Username, @OperationType, @Module, @OperationContent, @RequestData::jsonb, @ResponseData::jsonb, @Result, @ErrorMessage,
@IpAddress, @UserAgent, @RequestPath, @HttpMethod, @DurationMs, @IsSensitive, @CreatedAt)
";

    public const string QueryAuditLogs = @"
SELECT * FROM security_audit_logs 
WHERE (@UserId IS NULL OR user_id = @UserId)
AND (@OperationType IS NULL OR operation_type = @OperationType)
AND (@Module IS NULL OR module = @Module)
AND (@Result IS NULL OR result = @Result)
AND (@StartTime IS NULL OR created_at >= @StartTime)
AND (@EndTime IS NULL OR created_at <= @EndTime)
AND (@IsSensitive IS NULL OR is_sensitive = @IsSensitive)
ORDER BY created_at DESC
LIMIT @Limit OFFSET @Offset
";

    public const string CountAuditLogs = @"
SELECT COUNT(*) FROM security_audit_logs 
WHERE (@UserId IS NULL OR user_id = @UserId)
AND (@OperationType IS NULL OR operation_type = @OperationType)
AND (@Module IS NULL OR module = @Module)
AND (@Result IS NULL OR result = @Result)
AND (@StartTime IS NULL OR created_at >= @StartTime)
AND (@EndTime IS NULL OR created_at <= @EndTime)
AND (@IsSensitive IS NULL OR is_sensitive = @IsSensitive)
";

    #endregion

    #region 攻击日志表

    public const string CreateAttackLogTable = @"
CREATE TABLE IF NOT EXISTS security_attack_logs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    attack_type VARCHAR(50) NOT NULL,
    source_ip VARCHAR(50) NOT NULL,
    target_url VARCHAR(500) NOT NULL,
    http_method VARCHAR(10) NOT NULL,
    request_data TEXT NULL,
    request_headers JSONB NULL,
    attack_pattern VARCHAR(200) NULL,
    risk_level VARCHAR(20) NOT NULL,
    is_intercepted BOOLEAN DEFAULT TRUE,
    intercept_action VARCHAR(50) NULL,
    user_agent TEXT NULL,
    is_handled BOOLEAN DEFAULT FALSE,
    handler VARCHAR(100) NULL,
    handled_at TIMESTAMP NULL,
    handle_remark TEXT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX IF NOT EXISTS idx_attack_logs_source_ip ON security_attack_logs(source_ip);
CREATE INDEX IF NOT EXISTS idx_attack_logs_attack_type ON security_attack_logs(attack_type);
CREATE INDEX IF NOT EXISTS idx_attack_logs_risk_level ON security_attack_logs(risk_level);
CREATE INDEX IF NOT EXISTS idx_attack_logs_is_intercepted ON security_attack_logs(is_intercepted);
CREATE INDEX IF NOT EXISTS idx_attack_logs_is_handled ON security_attack_logs(is_handled);
CREATE INDEX IF NOT EXISTS idx_attack_logs_created_at ON security_attack_logs(created_at);
";

    public const string InsertAttackLog = @"
INSERT INTO security_attack_logs 
(id, attack_type, source_ip, target_url, http_method, request_data, request_headers, attack_pattern, risk_level, 
is_intercepted, intercept_action, user_agent, created_at)
VALUES 
(@Id, @AttackType, @SourceIp, @TargetUrl, @HttpMethod, @RequestData, @RequestHeaders::jsonb, @AttackPattern, @RiskLevel,
@IsIntercepted, @InterceptAction, @UserAgent, @CreatedAt)
";

    public const string QueryAttackLogs = @"
SELECT * FROM security_attack_logs 
WHERE (@AttackType IS NULL OR attack_type = @AttackType)
AND (@SourceIp IS NULL OR source_ip = @SourceIp)
AND (@RiskLevel IS NULL OR risk_level = @RiskLevel)
AND (@IsHandled IS NULL OR is_handled = @IsHandled)
AND (@StartTime IS NULL OR created_at >= @StartTime)
AND (@EndTime IS NULL OR created_at <= @EndTime)
ORDER BY created_at DESC
LIMIT @Limit OFFSET @Offset
";

    public const string CountAttackLogs = @"
SELECT COUNT(*) FROM security_attack_logs 
WHERE (@AttackType IS NULL OR attack_type = @AttackType)
AND (@SourceIp IS NULL OR source_ip = @SourceIp)
AND (@RiskLevel IS NULL OR risk_level = @RiskLevel)
AND (@IsHandled IS NULL OR is_handled = @IsHandled)
AND (@StartTime IS NULL OR created_at >= @StartTime)
AND (@EndTime IS NULL OR created_at <= @EndTime)
";

    public const string HandleAttackLog = @"
UPDATE security_attack_logs 
SET is_handled = TRUE, handler = @Handler, handled_at = @HandledAt, handle_remark = @HandleRemark
WHERE id = @Id
";

    #endregion

    #region 限流记录表

    public const string CreateRateLimitTable = @"
CREATE TABLE IF NOT EXISTS security_rate_limits (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    key_type VARCHAR(50) NOT NULL,
    key_value VARCHAR(200) NOT NULL,
    request_count INTEGER DEFAULT 0,
    window_start TIMESTAMP NOT NULL,
    window_end TIMESTAMP NOT NULL,
    is_limited BOOLEAN DEFAULT FALSE,
    limit_triggered_count INTEGER DEFAULT 0,
    last_request_at TIMESTAMP NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT idx_rate_limit_key UNIQUE (key_type, key_value, window_start)
);
CREATE INDEX IF NOT EXISTS idx_rate_limits_key ON security_rate_limits(key_type, key_value);
CREATE INDEX IF NOT EXISTS idx_rate_limits_window_end ON security_rate_limits(window_end);
";

    public const string UpsertRateLimit = @"
INSERT INTO security_rate_limits 
(id, key_type, key_value, request_count, window_start, window_end, is_limited, limit_triggered_count, last_request_at, created_at, updated_at)
VALUES 
(@Id, @KeyType, @KeyValue, @RequestCount, @WindowStart, @WindowEnd, @IsLimited, @LimitTriggeredCount, @LastRequestAt, @CreatedAt, @UpdatedAt)
ON CONFLICT (key_type, key_value, window_start) 
DO UPDATE SET 
    request_count = EXCLUDED.request_count,
    is_limited = EXCLUDED.is_limited,
    limit_triggered_count = EXCLUDED.limit_triggered_count,
    last_request_at = EXCLUDED.last_request_at,
    updated_at = EXCLUDED.updated_at
";

    public const string GetRateLimit = @"
SELECT * FROM security_rate_limits 
WHERE key_type = @KeyType AND key_value = @KeyValue AND window_start = @WindowStart
LIMIT 1
";

    public const string DeleteExpiredRateLimits = @"
DELETE FROM security_rate_limits WHERE window_end < @Now
";

    #endregion

    #region 告警记录表

    public const string CreateAlertTable = @"
CREATE TABLE IF NOT EXISTS security_alerts (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    alert_type VARCHAR(50) NOT NULL,
    user_id UUID NULL,
    title VARCHAR(200) NOT NULL,
    content TEXT NOT NULL,
    severity VARCHAR(20) NOT NULL,
    related_data JSONB NULL,
    source_ip VARCHAR(50) NULL,
    status VARCHAR(20) DEFAULT 'Pending',
    is_pushed BOOLEAN DEFAULT FALSE,
    push_channels VARCHAR(200) NULL,
    pushed_at TIMESTAMP NULL,
    handler VARCHAR(100) NULL,
    handle_remark TEXT NULL,
    resolved_at TIMESTAMP NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX IF NOT EXISTS idx_alerts_user_id ON security_alerts(user_id);
CREATE INDEX IF NOT EXISTS idx_alerts_alert_type ON security_alerts(alert_type);
CREATE INDEX IF NOT EXISTS idx_alerts_severity ON security_alerts(severity);
CREATE INDEX IF NOT EXISTS idx_alerts_status ON security_alerts(status);
CREATE INDEX IF NOT EXISTS idx_alerts_is_pushed ON security_alerts(is_pushed);
CREATE INDEX IF NOT EXISTS idx_alerts_created_at ON security_alerts(created_at);
";

    public const string InsertAlert = @"
INSERT INTO security_alerts 
(id, alert_type, user_id, title, content, severity, related_data, source_ip, status, is_pushed, created_at)
VALUES 
(@Id, @AlertType, @UserId, @Title, @Content, @Severity, @RelatedData::jsonb, @SourceIp, @Status, @IsPushed, @CreatedAt)
";

    public const string QueryAlerts = @"
SELECT * FROM security_alerts 
WHERE (@AlertType IS NULL OR alert_type = @AlertType)
AND (@Severity IS NULL OR severity = @Severity)
AND (@Status IS NULL OR status = @Status)
AND (@UserId IS NULL OR user_id = @UserId)
AND (@IsPushed IS NULL OR is_pushed = @IsPushed)
AND (@StartTime IS NULL OR created_at >= @StartTime)
AND (@EndTime IS NULL OR created_at <= @EndTime)
ORDER BY created_at DESC
LIMIT @Limit OFFSET @Offset
";

    public const string CountAlerts = @"
SELECT COUNT(*) FROM security_alerts 
WHERE (@AlertType IS NULL OR alert_type = @AlertType)
AND (@Severity IS NULL OR severity = @Severity)
AND (@Status IS NULL OR status = @Status)
AND (@UserId IS NULL OR user_id = @UserId)
AND (@IsPushed IS NULL OR is_pushed = @IsPushed)
AND (@StartTime IS NULL OR created_at >= @StartTime)
AND (@EndTime IS NULL OR created_at <= @EndTime)
";

    public const string UpdateAlertStatus = @"
UPDATE security_alerts 
SET status = @Status, handler = @Handler, handle_remark = @HandleRemark, resolved_at = @ResolvedAt
WHERE id = @Id
";

    public const string MarkAlertPushed = @"
UPDATE security_alerts 
SET is_pushed = TRUE, pushed_at = @PushedAt, push_channels = @PushChannels
WHERE id = @Id
";

    #endregion

    #region 推送渠道配置表

    public const string CreatePushChannelTable = @"
CREATE TABLE IF NOT EXISTS security_push_channels (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    channel_type VARCHAR(50) NOT NULL,
    channel_name VARCHAR(100) NOT NULL,
    config_json JSONB NOT NULL,
    is_enabled BOOLEAN DEFAULT TRUE,
    supported_severities VARCHAR(200) NULL,
    supported_alert_types VARCHAR(500) NULL,
    daily_push_limit INTEGER DEFAULT 0,
    today_push_count INTEGER DEFAULT 0,
    last_push_at TIMESTAMP NULL,
    description TEXT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX IF NOT EXISTS idx_push_channels_type ON security_push_channels(channel_type);
CREATE INDEX IF NOT EXISTS idx_push_channels_enabled ON security_push_channels(is_enabled);
";

    public const string InsertPushChannel = @"
INSERT INTO security_push_channels 
(id, channel_type, channel_name, config_json, is_enabled, supported_severities, supported_alert_types, daily_push_limit, description, created_at, updated_at)
VALUES 
(@Id, @ChannelType, @ChannelName, @ConfigJson::jsonb, @IsEnabled, @SupportedSeverities, @SupportedAlertTypes, @DailyPushLimit, @Description, @CreatedAt, @UpdatedAt)
";

    public const string UpdatePushChannel = @"
UPDATE security_push_channels 
SET channel_name = @ChannelName, config_json = @ConfigJson::jsonb, is_enabled = @IsEnabled, 
supported_severities = @SupportedSeverities, supported_alert_types = @SupportedAlertTypes, 
daily_push_limit = @DailyPushLimit, description = @Description, updated_at = @UpdatedAt
WHERE id = @Id
";

    public const string DeletePushChannel = @"
DELETE FROM security_push_channels WHERE id = @Id
";

    public const string GetPushChannelById = @"
SELECT * FROM security_push_channels WHERE id = @Id LIMIT 1
";

    public const string GetAllEnabledPushChannels = @"
SELECT * FROM security_push_channels WHERE is_enabled = TRUE
";

    public const string IncrementPushCount = @"
UPDATE security_push_channels 
SET today_push_count = today_push_count + 1, last_push_at = @LastPushAt
WHERE id = @Id
";

    public const string ResetDailyPushCount = @"
UPDATE security_push_channels SET today_push_count = 0
";

    #endregion

    #region 所有建表语句

    public static readonly string[] AllCreateTableSqls = new[]
    {
        CreateTokenTable,
        CreateAuditLogTable,
        CreateAttackLogTable,
        CreateRateLimitTable,
        CreateAlertTable,
        CreatePushChannelTable
    };

    #endregion
}
