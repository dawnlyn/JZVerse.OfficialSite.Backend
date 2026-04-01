namespace JZVerse.Business.Central.Security.Services.Interfaces;

/// <summary>
/// 攻击防护服务接口
/// </summary>
public interface IAttackProtectionService
{
    #region SQL注入检测

    /// <summary>
    /// 检查SQL注入攻击
    /// </summary>
    /// <param name="input">输入内容</param>
    /// <returns>检测结果</returns>
    AttackCheckResult CheckSqlInjection(string input);

    /// <summary>
    /// 检查SQL注入攻击（批量）
    /// </summary>
    /// <param name="inputs">输入内容字典</param>
    /// <returns>检测结果列表</returns>
    List<AttackCheckResult> CheckSqlInjectionBatch(Dictionary<string, string> inputs);

    #endregion

    #region XSS检测

    /// <summary>
    /// 检查XSS攻击
    /// </summary>
    /// <param name="input">输入内容</param>
    /// <returns>检测结果</returns>
    AttackCheckResult CheckXss(string input);

    /// <summary>
    /// 对输入内容进行HTML编码（防XSS）
    /// </summary>
    /// <param name="input">输入内容</param>
    /// <returns>编码后的内容</returns>
    string HtmlEncode(string input);

    /// <summary>
    /// 对输入内容进行HTML解码
    /// </summary>
    /// <param name="input">编码内容</param>
    /// <returns>解码后的内容</returns>
    string HtmlDecode(string input);

    #endregion

    #region CSRF检测

    /// <summary>
    /// 生成CSRF Token
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <returns>CSRF Token</returns>
    string GenerateCsrfToken(string sessionId);

    /// <summary>
    /// 验证CSRF Token
    /// </summary>
    /// <param name="token">Token</param>
    /// <param name="sessionId">会话ID</param>
    /// <returns>是否有效</returns>
    bool ValidateCsrfToken(string token, string sessionId);

    /// <summary>
    /// 检查请求是否存在CSRF风险
    /// </summary>
    /// <param name="referer">Referer头</param>
    /// <param name="origin">Origin头</param>
    /// <param name="allowedDomains">允许的域名列表</param>
    /// <returns>检测结果</returns>
    AttackCheckResult CheckCsrfRisk(string? referer, string? origin, List<string> allowedDomains);

    #endregion

    #region 路径遍历检测

    /// <summary>
    /// 检查路径遍历攻击
    /// </summary>
    /// <param name="path">路径</param>
    /// <returns>检测结果</returns>
    AttackCheckResult CheckPathTraversal(string path);

    #endregion

    #region 命令注入检测

    /// <summary>
    /// 检查命令注入攻击
    /// </summary>
    /// <param name="input">输入内容</param>
    /// <returns>检测结果</returns>
    AttackCheckResult CheckCommandInjection(string input);

    #endregion

    #region IP封禁

    /// <summary>
    /// 封禁IP
    /// </summary>
    /// <param name="ipAddress">IP地址</param>
    /// <param name="reason">封禁原因</param>
    /// <param name="expireMinutes">过期时间（分钟，0表示永久）</param>
    /// <returns>是否成功</returns>
    Task<bool> BanIpAsync(string ipAddress, string reason, int expireMinutes = 0);

    /// <summary>
    /// 解封IP
    /// </summary>
    /// <param name="ipAddress">IP地址</param>
    /// <returns>是否成功</returns>
    Task<bool> UnbanIpAsync(string ipAddress);

    /// <summary>
    /// 检查IP是否被封禁
    /// </summary>
    /// <param name="ipAddress">IP地址</param>
    /// <returns>封禁信息</returns>
    Task<IpBanInfo?> CheckIpBannedAsync(string ipAddress);

    /// <summary>
    /// 获取封禁IP列表
    /// </summary>
    /// <param name="pageIndex">页码</param>
    /// <param name="pageSize">每页数量</param>
    /// <returns>封禁IP列表</returns>
    Task<(List<IpBanInfo> Items, int Total)> GetBannedIpsAsync(int pageIndex = 1, int pageSize = 20);

    #endregion

    #region 综合检测

    /// <summary>
    /// 对请求进行综合安全检测
    /// </summary>
    /// <param name="request">请求信息</param>
    /// <returns>检测结果列表</returns>
    Task<List<AttackCheckResult>> ComprehensiveCheckAsync(SecurityRequest request);

    #endregion
}

/// <summary>
/// 攻击检测结果
/// </summary>
public class AttackCheckResult
{
    /// <summary>
    /// 是否检测到攻击
    /// </summary>
    public bool IsAttack { get; set; }

    /// <summary>
    /// 攻击类型
    /// </summary>
    public string AttackType { get; set; } = string.Empty;

    /// <summary>
    /// 风险等级
    /// </summary>
    public string RiskLevel { get; set; } = string.Empty;

    /// <summary>
    /// 检测到的攻击特征/模式
    /// </summary>
    public string? Pattern { get; set; }

    /// <summary>
    /// 匹配的输入字段
    /// </summary>
    public string? FieldName { get; set; }

    /// <summary>
    /// 匹配的内容片段
    /// </summary>
    public string? MatchedContent { get; set; }

    /// <summary>
    /// 描述信息
    /// </summary>
    public string Message { get; set; } = string.Empty;

    public static AttackCheckResult Safe()
    {
        return new AttackCheckResult { IsAttack = false };
    }

    public static AttackCheckResult Detected(string attackType, string riskLevel, string message, string? pattern = null, string? fieldName = null, string? matchedContent = null)
    {
        return new AttackCheckResult
        {
            IsAttack = true,
            AttackType = attackType,
            RiskLevel = riskLevel,
            Pattern = pattern,
            FieldName = fieldName,
            MatchedContent = matchedContent,
            Message = message
        };
    }
}

/// <summary>
/// 安全请求信息
/// </summary>
public class SecurityRequest
{
    /// <summary>
    /// IP地址
    /// </summary>
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    /// 请求路径
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// HTTP方法
    /// </summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>
    /// 查询参数
    /// </summary>
    public Dictionary<string, string> QueryParams { get; set; } = new();

    /// <summary>
    /// 表单数据
    /// </summary>
    public Dictionary<string, string> FormData { get; set; } = new();

    /// <summary>
    /// 请求体内容
    /// </summary>
    public string? Body { get; set; }

    /// <summary>
    /// 请求头
    /// </summary>
    public Dictionary<string, string> Headers { get; set; } = new();

    /// <summary>
    /// Cookies
    /// </summary>
    public Dictionary<string, string> Cookies { get; set; } = new();

    /// <summary>
    /// 用户代理
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Referer
    /// </summary>
    public string? Referer { get; set; }

    /// <summary>
    /// Origin
    /// </summary>
    public string? Origin { get; set; }
}

/// <summary>
/// IP封禁信息
/// </summary>
public class IpBanInfo
{
    /// <summary>
    /// IP地址
    /// </summary>
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    /// 封禁原因
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// 封禁时间
    /// </summary>
    public DateTime BannedAt { get; set; }

    /// <summary>
    /// 过期时间
    /// </summary>
    public DateTime? ExpireAt { get; set; }

    /// <summary>
    /// 是否永久封禁
    /// </summary>
    public bool IsPermanent => ExpireAt == null;

    /// <summary>
    /// 攻击次数
    /// </summary>
    public int AttackCount { get; set; }
}
