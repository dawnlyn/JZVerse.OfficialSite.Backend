using System.Security.Claims;
using JZVerse.Business.Abstractions.Authentication;
using JZVerse.Business.Abstractions.Models;
using Microsoft.AspNetCore.Http;

namespace JZVerse.Business.Infrastructure.Authentication;

/// <summary>
/// 用户上下文实现（Scoped 生命周期）
/// </summary>
public sealed class UserContext : IUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private ClaimsPrincipal? _user;
    private Guid? _userId;
    private string? _username;
    private IReadOnlyList<string>? _roles;

    public UserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _user ??= _httpContextAccessor.HttpContext?.User;

    /// <inheritdoc />
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    /// <inheritdoc />
    public Guid UserId
    {
        get
        {
            if (!TryGetUserId(out var userId))
                throw new AuthenticationException("用户未认证");

            return userId;
        }
    }

    /// <inheritdoc />
    public string Username
    {
        get
        {
            if (_username is not null)
                return _username;

            if (!IsAuthenticated)
                throw new AuthenticationException("用户未认证");

            _username = User?.Identity?.Name
                ?? User?.FindFirst(ClaimTypes.Name)?.Value
                ?? throw new AuthenticationException("无法获取用户名");

            return _username;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<string> Roles
    {
        get
        {
            if (_roles is not null)
                return _roles;

            if (!IsAuthenticated)
                return [];

            _roles = User?.Claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .ToList() ?? [];

            return _roles;
        }
    }

    /// <inheritdoc />
    public bool HasRole(string role)
    {
        return Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public bool HasAnyRole(params string[] roles)
    {
        return roles.Any(role => HasRole(role));
    }

    /// <inheritdoc />
    public bool HasAllRoles(params string[] roles)
    {
        return roles.All(role => HasRole(role));
    }

    /// <inheritdoc />
    public string? GetClaim(string claimType)
    {
        return User?.FindFirst(claimType)?.Value;
    }

    /// <inheritdoc />
    public bool TryGetUserId(out Guid userId)
    {
        userId = Guid.Empty;

        if (_userId.HasValue)
        {
            userId = _userId.Value;
            return true;
        }

        if (!IsAuthenticated)
            return false;

        var userIdClaim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out userId))
            return false;

        _userId = userId;
        return true;
    }
}
