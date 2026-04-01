namespace JZVerse.Business.Central.Security.Services.Interfaces;

/// <summary>
/// 密码服务接口
/// </summary>
public interface IPasswordService
{
    /// <summary>
    /// 生成密码哈希
    /// </summary>
    /// <param name="password">明文密码</param>
    /// <returns>密码哈希（包含盐值）</returns>
    Task<string> HashPasswordAsync(string password);

    /// <summary>
    /// 验证密码
    /// </summary>
    /// <param name="password">明文密码</param>
    /// <param name="hashedPassword">密码哈希</param>
    /// <returns>是否匹配</returns>
    Task<bool> VerifyPasswordAsync(string password, string hashedPassword);

    /// <summary>
    /// 生成随机盐值
    /// </summary>
    /// <param name="length">盐值长度</param>
    /// <returns>盐值</returns>
    string GenerateSalt(int length = 32);

    /// <summary>
    /// 检查密码强度
    /// </summary>
    /// <param name="password">明文密码</param>
    /// <returns>密码强度检查结果</returns>
    PasswordStrength CheckPasswordStrength(string password);

    /// <summary>
    /// 生成随机密码
    /// </summary>
    /// <param name="length">密码长度</param>
    /// <param name="includeSpecialChars">是否包含特殊字符</param>
    /// <returns>随机密码</returns>
    string GenerateRandomPassword(int length = 16, bool includeSpecialChars = true);
}

/// <summary>
/// 密码强度
/// </summary>
public enum PasswordStrength
{
    /// <summary>
    /// 非常弱
    /// </summary>
    VeryWeak,

    /// <summary>
    /// 弱
    /// </summary>
    Weak,

    /// <summary>
    /// 中等
    /// </summary>
    Medium,

    /// <summary>
    /// 强
    /// </summary>
    Strong,

    /// <summary>
    /// 非常强
    /// </summary>
    VeryStrong
}

/// <summary>
/// 密码强度检查结果
/// </summary>
public class PasswordStrengthResult
{
    /// <summary>
    /// 密码强度
    /// </summary>
    public PasswordStrength Strength { get; set; }

    /// <summary>
    /// 强度分数（0-100）
    /// </summary>
    public int Score { get; set; }

    /// <summary>
    /// 是否通过验证
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// 失败原因
    /// </summary>
    public List<string> Failures { get; set; } = new();

    /// <summary>
    /// 建议
    /// </summary>
    public List<string> Suggestions { get; set; } = new();
}
