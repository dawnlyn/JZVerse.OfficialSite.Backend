using System.Net;
using JZVerse.Business.Central.Security.Services.Interfaces;

namespace JZVerse.Business.Central.Security.Middleware;

/// <summary>
/// 攻击防护中间件
/// </summary>
public class AttackProtectionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IAttackProtectionService _attackProtection;
    private readonly IAuditService _auditService;
    private readonly ILogger<AttackProtectionMiddleware> _logger;
    private readonly AttackProtectionOptions _options;

    public AttackProtectionMiddleware(
        RequestDelegate next,
        IAttackProtectionService attackProtection,
        IAuditService auditService,
        ILogger<AttackProtectionMiddleware> logger,
        AttackProtectionOptions options)
    {
        _next = next;
        _attackProtection = attackProtection;
        _auditService = auditService;
        _logger = logger;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 检查IP是否被封禁
        var ipAddress = GetClientIpAddress(context);
        var banInfo = await _attackProtection.CheckIpBannedAsync(ipAddress);
        if (banInfo != null)
        {
            _logger.LogWarning("Request from banned IP: {IpAddress}, Reason: {Reason}", 
                ipAddress, banInfo.Reason);
            
            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                Code = 403,
                Message = "您的IP已被封禁，请联系管理员"
            });
            return;
        }

        // 构建安全请求
        var securityRequest = await BuildSecurityRequestAsync(context);

        // 综合安全检测
        var checkResults = await _attackProtection.ComprehensiveCheckAsync(securityRequest);
        var attacks = checkResults.Where(r => r.IsAttack).ToList();

        if (attacks.Any())
        {
            // 记录攻击日志
            foreach (var attack in attacks)
            {
                _logger.LogWarning("Attack detected: {AttackType} from {IpAddress}, Risk: {RiskLevel}, Pattern: {Pattern}",
                    attack.AttackType, ipAddress, attack.RiskLevel, attack.Pattern);
            }

            // 如果配置了拦截
            if (_options.BlockAttacks)
            {
                var highRiskAttacks = attacks.Where(a => a.RiskLevel == "High" || a.RiskLevel == "Critical").ToList();
                if (highRiskAttacks.Any())
                {
                    // 高风险攻击直接封禁IP
                    await _attackProtection.BanIpAsync(ipAddress, 
                        $"Detected {highRiskAttacks.First().AttackType} attack", 
                        _options.IpBanDurationMinutes);

                    context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        Code = 403,
                        Message = "检测到恶意请求，您的IP已被临时封禁"
                    });
                    return;
                }

                // 中等风险返回400
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                await context.Response.WriteAsJsonAsync(new
                {
                    Code = 400,
                    Message = "请求包含非法内容",
                    Details = _options.IncludeDetailsInResponse 
                        ? attacks.Select(a => a.Message).ToList() 
                        : null
                });
                return;
            }
        }

        // 添加安全响应头
        AddSecurityHeaders(context.Response);

        await _next(context);
    }

    private async Task<SecurityRequest> BuildSecurityRequestAsync(HttpContext context)
    {
        var request = context.Request;
        var securityRequest = new SecurityRequest
        {
            IpAddress = GetClientIpAddress(context),
            Path = request.Path.ToString(),
            Method = request.Method,
            UserAgent = request.Headers.UserAgent.ToString(),
            Referer = request.Headers.Referer.ToString(),
            Origin = request.Headers.Origin.ToString(),
            Headers = request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()),
            Cookies = request.Cookies.ToDictionary(c => c.Key, c => c.Value)
        };

        // 读取查询参数
        securityRequest.QueryParams = request.Query.ToDictionary(q => q.Key, q => q.Value.ToString());

        // 读取表单数据
        if (request.HasFormContentType && request.Form != null)
        {
            securityRequest.FormData = request.Form.ToDictionary(f => f.Key, f => f.Value.ToString());
        }

        // 读取请求体（如果是文本内容）
        if (request.ContentType != null && 
            (request.ContentType.Contains("application/json") || request.ContentType.Contains("text/")))
        {
            request.EnableBuffering();
            using var reader = new StreamReader(request.Body, leaveOpen: true);
            securityRequest.Body = await reader.ReadToEndAsync();
            request.Body.Position = 0;
        }

        return securityRequest;
    }

    private string GetClientIpAddress(HttpContext context)
    {
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private void AddSecurityHeaders(HttpResponse response)
    {
        // 防止XSS
        response.Headers["X-XSS-Protection"] = "1; mode=block";
        
        // 防止点击劫持
        response.Headers["X-Frame-Options"] = "DENY";
        
        // 防止MIME类型嗅探
        response.Headers["X-Content-Type-Options"] = "nosniff";
        
        // 内容安全策略
        response.Headers["Content-Security-Policy"] = _options.ContentSecurityPolicy;
        
        // Referrer策略
        response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        
        // 权限策略
        response.Headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
    }
}

/// <summary>
/// 攻击防护选项
/// </summary>
public class AttackProtectionOptions
{
    /// <summary>
    /// 是否拦截攻击
    /// </summary>
    public bool BlockAttacks { get; set; } = true;

    /// <summary>
    /// 是否在响应中包含详细信息
    /// </summary>
    public bool IncludeDetailsInResponse { get; set; } = false;

    /// <summary>
    /// IP封禁时长（分钟，0表示永久）
    /// </summary>
    public int IpBanDurationMinutes { get; set; } = 60;

    /// <summary>
    /// 内容安全策略
    /// </summary>
    public string ContentSecurityPolicy { get; set; } = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline';";
}
