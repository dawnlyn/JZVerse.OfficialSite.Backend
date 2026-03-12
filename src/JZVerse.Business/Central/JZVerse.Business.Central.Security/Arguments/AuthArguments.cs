using JZVerse.Business.Abstractions.Validation;

namespace JZVerse.Business.Central.Security.Arguments;

/// <summary>
/// 登录入参
/// </summary>
public sealed record ArgLogin
{
    [Required(ErrorMessage = "用户名不能为空")]
    [StringLength(MinLength = 3, MaxLength = 50, ErrorMessage = "用户名长度必须在3-50之间")]
    public string Username { get; init; } = "";

    [Required(ErrorMessage = "密码不能为空")]
    [StringLength(MinLength = 6, MaxLength = 100, ErrorMessage = "密码长度必须在6-100之间")]
    public string Password { get; init; } = "";
}

/// <summary>
/// 注册入参
/// </summary>
public sealed record ArgRegister
{
    [Required(ErrorMessage = "用户名不能为空")]
    [StringLength(MinLength = 3, MaxLength = 50, ErrorMessage = "用户名长度必须在3-50之间")]
    [Regex(@"^[a-zA-Z0-9_]+$", ErrorMessage = "用户名只能包含字母、数字和下划线")]
    public string Username { get; init; } = "";

    [Required(ErrorMessage = "密码不能为空")]
    [StringLength(MinLength = 6, MaxLength = 100, ErrorMessage = "密码长度必须在6-100之间")]
    public string Password { get; init; } = "";

    [Required(ErrorMessage = "邮箱不能为空")]
    [Email(ErrorMessage = "邮箱格式不正确")]
    public string Email { get; init; } = "";

    [Phone(ErrorMessage = "手机号格式不正确")]
    public string? Phone { get; init; }
}

/// <summary>
/// 刷新 Token 入参
/// </summary>
public sealed record ArgRefreshToken
{
    [Required(ErrorMessage = "刷新令牌不能为空")]
    public string RefreshToken { get; init; } = "";
}
