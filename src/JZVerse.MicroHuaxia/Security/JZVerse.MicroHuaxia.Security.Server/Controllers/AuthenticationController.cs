using JZVerse.MicroHuaxia.Security.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace JZVerse.MicroHuaxia.Security.Server.Controllers;

/// <summary>
/// 认证控制器
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class AuthenticationController : ControllerBase
{
    private readonly IAuthenticationService _authService;
    private readonly ILogger<AuthenticationController> _logger;

    public AuthenticationController(
        IAuthenticationService authService,
        ILogger<AuthenticationController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// 使用令牌认证
    /// </summary>
    [HttpPost("token")]
    public async Task<IActionResult> AuthenticateWithToken([FromBody] TokenRequest request)
    {
        var result = await _authService.AuthenticateWithTokenAsync(
            request.Token, 
            request.TokenType);

        if (result.Succeeded)
        {
            return Ok(new { success = true, identity = result.Identity });
        }

        return Unauthorized(new { success = false, message = result.FailureReason });
    }

    /// <summary>
    /// 生成服务令牌
    /// </summary>
    [HttpPost("service-token")]
    public async Task<IActionResult> GenerateServiceToken([FromBody] GenerateTokenRequest request)
    {
        var token = await _authService.GenerateServiceTokenAsync(request.Identity);
        return Ok(token);
    }

    /// <summary>
    /// 刷新令牌
    /// </summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        try
        {
            var token = await _authService.RefreshTokenAsync(request.RefreshToken);
            return Ok(token);
        }
        catch (SecurityException ex)
        {
            return Unauthorized(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// 吊销令牌
    /// </summary>
    [HttpPost("revoke")]
    public async Task<IActionResult> RevokeToken([FromBody] RevokeTokenRequest request)
    {
        var result = await _authService.RevokeTokenAsync(request.Token);
        return Ok(new { success = result });
    }
}

/// <summary>
/// 令牌认证请求
/// </summary>
public class TokenRequest
{
    /// <summary>
    /// 令牌
    /// </summary>
    public string Token { get; set; } = null!;

    /// <summary>
    /// 令牌类型
    /// </summary>
    public TokenType TokenType { get; set; } = TokenType.AccessToken;
}

/// <summary>
/// 生成令牌请求
/// </summary>
public class GenerateTokenRequest
{
    /// <summary>
    /// 身份信息
    /// </summary>
    public SecurityIdentity Identity { get; set; } = null!;
}

/// <summary>
/// 刷新令牌请求
/// </summary>
public class RefreshTokenRequest
{
    /// <summary>
    /// 刷新令牌
    /// </summary>
    public string RefreshToken { get; set; } = null!;
}

/// <summary>
/// 吊销令牌请求
/// </summary>
public class RevokeTokenRequest
{
    /// <summary>
    /// 令牌
    /// </summary>
    public string Token { get; set; } = null!;
}
