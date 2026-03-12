using JZVerse.Business.Abstractions.Authentication;
using JZVerse.Business.Abstractions.Models;
using JZVerse.Business.Central.Security.Arguments;
using JZVerse.Business.Central.Security.Database;
using JZVerse.Business.Central.Security.Results;
using JZVerse.Business.Infrastructure.Authentication;
using JZVerse.DataAccess.Abstractions;
using Microsoft.Extensions.Options;

namespace JZVerse.Business.Central.Security.Services;

/// <summary>
/// 认证服务
/// </summary>
public sealed class AuthenticationService
{
    private readonly IDbExecutor _dbExecutor;
    private readonly ISnowflakeIdGenerator _idGenerator;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly JwtOptions _jwtOptions;

    public AuthenticationService(
        IDbExecutor dbExecutor,
        ISnowflakeIdGenerator idGenerator,
        IJwtTokenService jwtTokenService,
        IOptions<JwtOptions> jwtOptions)
    {
        _dbExecutor = dbExecutor;
        _idGenerator = idGenerator;
        _jwtTokenService = jwtTokenService;
        _jwtOptions = jwtOptions.Value;
    }

    /// <summary>
    /// 用户注册
    /// </summary>
    public async Task<ResultUserId> RegisterAsync(ArgRegister arg, CancellationToken cancellationToken = default)
    {
        // 检查用户名是否存在
        var usernameExists = await _dbExecutor.ExecuteScalarAsync<bool>(
            SecuritySql.CheckUsernameExists,
            new { arg.Username },
            cancellationToken);

        if (usernameExists)
            throw new BusinessException(400, "用户名已存在");

        // 检查邮箱是否存在
        var emailExists = await _dbExecutor.ExecuteScalarAsync<bool>(
            SecuritySql.CheckEmailExists,
            new { arg.Email },
            cancellationToken);

        if (emailExists)
            throw new BusinessException(400, "邮箱已被注册");

        // 创建用户
        var userId = _idGenerator.GenerateGuid();
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(arg.Password, workFactor: 12);
        var now = DateTimeOffset.UtcNow;

        await _dbExecutor.ExecuteAsync(SecuritySql.CreateUser, new
        {
            Id = userId,
            arg.Username,
            PasswordHash = passwordHash,
            arg.Email,
            arg.Phone,
            Status = (int)UserStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        }, cancellationToken);

        // 分配默认角色
        var defaultRole = await _dbExecutor.QueryFirstOrDefaultAsync<RoleEntity>(
            SecuritySql.GetRoleByCode,
            new { Code = "user" },
            cancellationToken);

        if (defaultRole is not null)
        {
            await _dbExecutor.ExecuteAsync(SecuritySql.AssignRoleToUser, new
            {
                UserId = userId,
                RoleId = defaultRole.Id
            }, cancellationToken);
        }

        return new ResultUserId(userId);
    }

    /// <summary>
    /// 用户登录
    /// </summary>
    public async Task<ResultLoginToken> LoginAsync(ArgLogin arg, CancellationToken cancellationToken = default)
    {
        // 查询用户
        var user = await _dbExecutor.QueryFirstOrDefaultAsync<UserEntity>(
            SecuritySql.GetUserByUsername,
            new { arg.Username },
            cancellationToken);

        if (user is null)
            throw new AuthenticationException("用户名或密码错误");

        // 验证密码
        if (!BCrypt.Net.BCrypt.Verify(arg.Password, user.PasswordHash))
            throw new AuthenticationException("用户名或密码错误");

        // 检查用户状态
        if (user.Status == (int)UserStatus.Disabled)
            throw new AuthenticationException("账户已被禁用");

        // 获取用户角色
        var roles = await _dbExecutor.QueryAsync<RoleEntity>(
            SecuritySql.GetUserRoles,
            new { UserId = user.Id },
            cancellationToken);

        var roleNames = roles.Select(r => r.Code).ToList();

        // 生成 Token
        var accessToken = await _jwtTokenService.GenerateAccessTokenAsync(
            user.Id,
            user.Username,
            roleNames);

        var refreshToken = await _jwtTokenService.GenerateRefreshTokenAsync(user.Id);

        return new ResultLoginToken
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = _jwtOptions.ExpirationMinutes * 60,
            User = new ResultUserInfo
            {
                UserId = user.Id,
                Username = user.Username,
                Email = user.Email,
                Roles = roleNames
            }
        };
    }

    /// <summary>
    /// 刷新 Token
    /// </summary>
    public async Task<ResultLoginToken> RefreshTokenAsync(ArgRefreshToken arg, CancellationToken cancellationToken = default)
    {
        // 验证刷新令牌
        var userId = await _jwtTokenService.ValidateRefreshTokenAsync(arg.RefreshToken);

        if (userId is null)
            throw new AuthenticationException("刷新令牌无效或已过期");

        // 查询用户
        var user = await _dbExecutor.QueryFirstOrDefaultAsync<UserEntity>(
            SecuritySql.GetUserById,
            new { Id = userId.Value },
            cancellationToken);

        if (user is null || user.Status == (int)UserStatus.Disabled)
            throw new AuthenticationException("用户不存在或已被禁用");

        // 获取用户角色
        var roles = await _dbExecutor.QueryAsync<RoleEntity>(
            SecuritySql.GetUserRoles,
            new { UserId = user.Id },
            cancellationToken);

        var roleNames = roles.Select(r => r.Code).ToList();

        // 生成新的 Token
        var accessToken = await _jwtTokenService.GenerateAccessTokenAsync(
            user.Id,
            user.Username,
            roleNames);

        var refreshToken = await _jwtTokenService.GenerateRefreshTokenAsync(user.Id);

        return new ResultLoginToken
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = _jwtOptions.ExpirationMinutes * 60,
            User = new ResultUserInfo
            {
                UserId = user.Id,
                Username = user.Username,
                Email = user.Email,
                Roles = roleNames
            }
        };
    }

    /// <summary>
    /// 登出
    /// </summary>
    public async Task LogoutAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await _jwtTokenService.RevokeRefreshTokenAsync(userId);
    }

    /// <summary>
    /// 获取当前用户信息
    /// </summary>
    public async Task<ResultUserInfo> GetCurrentUserInfoAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbExecutor.QueryFirstOrDefaultAsync<UserEntity>(
            SecuritySql.GetUserById,
            new { Id = userId },
            cancellationToken);

        if (user is null)
            throw new ResourceNotFoundException("用户", userId);

        var roles = await _dbExecutor.QueryAsync<RoleEntity>(
            SecuritySql.GetUserRoles,
            new { UserId = user.Id },
            cancellationToken);

        return new ResultUserInfo
        {
            UserId = user.Id,
            Username = user.Username,
            Email = user.Email,
            Roles = roles.Select(r => r.Code).ToList()
        };
    }
}
