using JZVerse.Business.Abstractions.Authentication;
using JZVerse.Business.Abstractions.Models;
using JZVerse.Business.Central.Security.Arguments;
using JZVerse.Business.Central.Security.Results;
using JZVerse.Business.Central.Security.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JZVerse.Business.Central.Security.Controllers;

/// <summary>
/// 认证控制器
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthenticationController : ControllerBase
{
    private readonly AuthenticationService _authenticationService;
    private readonly IUserContext _userContext;

    public AuthenticationController(
        AuthenticationService authenticationService,
        IUserContext userContext)
    {
        _authenticationService = authenticationService;
        _userContext = userContext;
    }

    /// <summary>
    /// 用户登录
    /// </summary>
    [HttpPost("login")]
    public async Task<ApiResponse<ResultLoginToken>> Login(
        [FromBody] ArgLogin arg,
        CancellationToken cancellationToken)
    {
        var result = await _authenticationService.LoginAsync(arg, cancellationToken);
        return ApiResponse<ResultLoginToken>.Success(result);
    }

    /// <summary>
    /// 刷新 Token
    /// </summary>
    [HttpPost("refresh")]
    public async Task<ApiResponse<ResultLoginToken>> RefreshToken(
        [FromBody] ArgRefreshToken arg,
        CancellationToken cancellationToken)
    {
        var result = await _authenticationService.RefreshTokenAsync(arg, cancellationToken);
        return ApiResponse<ResultLoginToken>.Success(result);
    }

    /// <summary>
    /// 登出
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<ApiResponse> Logout(CancellationToken cancellationToken)
    {
        await _authenticationService.LogoutAsync(_userContext.UserId, cancellationToken);
        return ApiResponse.Success("登出成功");
    }

    /// <summary>
    /// 验证当前 Token 并获取用户信息
    /// </summary>
    [HttpGet("verify")]
    [Authorize]
    public async Task<ApiResponse<ResultUserInfo>> Verify(CancellationToken cancellationToken)
    {
        var result = await _authenticationService.GetCurrentUserInfoAsync(
            _userContext.UserId,
            cancellationToken);
        return ApiResponse<ResultUserInfo>.Success(result);
    }
}
