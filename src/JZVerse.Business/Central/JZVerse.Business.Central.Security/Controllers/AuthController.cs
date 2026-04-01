using JZVerse.Business.Central.Security.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JZVerse.Business.Central.Security.Controllers;

/// <summary>
/// 认证控制器 - 提供Token生成、验证、刷新等服务
/// </summary>
[ApiController]
[Route("api/v1/security/auth")]
public class AuthController : ControllerBase
{
    private readonly ITokenService _tokenService;
    private readonly IPasswordService _passwordService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        ITokenService tokenService,
        IPasswordService passwordService,
        ILogger<AuthController> logger)
    {
        _tokenService = tokenService;
        _passwordService = passwordService;
        _logger = logger;
    }

    /// <summary>
    /// 生成Token对（内部服务调用）
    /// </summary>
    [HttpPost("generate-token")]
    [Authorize(Roles = "Service")] // 仅允许内部服务调用
    public async Task<ActionResult<TokenResult>> GenerateToken(GenerateTokenRequest request)
    {
        var result = await _tokenService.GenerateTokensAsync(
            request.UserId,
            request.Username,
            request.Claims,
            request.IpAddress ?? HttpContext.Connection.RemoteIpAddress?.ToString(),
            request.UserAgent ?? Request.Headers.UserAgent.ToString());

        if (!result.Success)
        {
            return BadRequest(new { Code = 400, Message = result.ErrorMessage });
        }

        return Ok(result);
    }

    /// <summary>
    /// 验证Token
    /// </summary>
    [HttpPost("validate-token")]
    [AllowAnonymous]
    public async Task<ActionResult<TokenValidationResult>> ValidateToken(ValidateTokenRequest request)
    {
        var result = await _tokenService.ValidateTokenAsync(request.Token);
        return Ok(result);
    }

    /// <summary>
    /// 刷新Token
    /// </summary>
    [HttpPost("refresh-token")]
    [AllowAnonymous]
    public async Task<ActionResult<TokenResult>> RefreshToken(RefreshTokenRequest request)
    {
        var result = await _tokenService.RefreshTokensAsync(request.RefreshToken);

        if (!result.Success)
        {
            return BadRequest(new { Code = 400, Message = result.ErrorMessage });
        }

        return Ok(result);
    }

    /// <summary>
    /// 撤销Token
    /// </summary>
    [HttpPost("revoke-token")]
    [Authorize]
    public async Task<ActionResult> RevokeToken(RevokeTokenRequest request)
    {
        var result = await _tokenService.RevokeTokenAsync(request.Token);
        if (!result)
        {
            return BadRequest(new { Code = 400, Message = "撤销Token失败" });
        }

        return Ok(new { Code = 200, Message = "Token已撤销" });
    }

    /// <summary>
    /// 撤销用户所有Token
    /// </summary>
    [HttpPost("revoke-user-tokens/{userId:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> RevokeUserTokens(Guid userId)
    {
        var result = await _tokenService.RevokeAllUserTokensAsync(userId);
        if (!result)
        {
            return BadRequest(new { Code = 400, Message = "撤销用户Token失败" });
        }

        return Ok(new { Code = 200, Message = "用户所有Token已撤销" });
    }

    /// <summary>
    /// 密码哈希（供用户中心调用）
    /// </summary>
    [HttpPost("hash-password")]
    [Authorize(Roles = "Service")]
    public async Task<ActionResult<HashPasswordResponse>> HashPassword(HashPasswordRequest request)
    {
        var hash = await _passwordService.HashPasswordAsync(request.Password);
        return Ok(new HashPasswordResponse { Hash = hash });
    }

    /// <summary>
    /// 验证密码（供用户中心调用）
    /// </summary>
    [HttpPost("verify-password")]
    [Authorize(Roles = "Service")]
    public async Task<ActionResult<VerifyPasswordResponse>> VerifyPassword(VerifyPasswordRequest request)
    {
        var isValid = await _passwordService.VerifyPasswordAsync(request.Password, request.Hash);
        return Ok(new VerifyPasswordResponse { IsValid = isValid });
    }

    /// <summary>
    /// 检查密码强度
    /// </summary>
    [HttpPost("check-password-strength")]
    [AllowAnonymous]
    public ActionResult<PasswordStrengthResponse> CheckPasswordStrength(CheckPasswordRequest request)
    {
        var strength = _passwordService.CheckPasswordStrength(request.Password);
        return Ok(new PasswordStrengthResponse 
        { 
            Strength = strength.ToString(),
            IsStrong = strength >= PasswordStrength.Medium
        });
    }
}

// 请求/响应模型
public class GenerateTokenRequest
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public Dictionary<string, string>? Claims { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}

public class ValidateTokenRequest
{
    public string Token { get; set; } = string.Empty;
}

public class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

public class RevokeTokenRequest
{
    public string Token { get; set; } = string.Empty;
}

public class HashPasswordRequest
{
    public string Password { get; set; } = string.Empty;
}

public class HashPasswordResponse
{
    public string Hash { get; set; } = string.Empty;
}

public class VerifyPasswordRequest
{
    public string Password { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
}

public class VerifyPasswordResponse
{
    public bool IsValid { get; set; }
}

public class CheckPasswordRequest
{
    public string Password { get; set; } = string.Empty;
}

public class PasswordStrengthResponse
{
    public string Strength { get; set; } = string.Empty;
    public bool IsStrong { get; set; }
}
