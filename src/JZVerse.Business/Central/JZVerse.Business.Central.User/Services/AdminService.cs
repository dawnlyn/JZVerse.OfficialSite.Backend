using BCrypt.Net;
using JZVerse.Business.Abstractions.Authentication;
using JZVerse.Business.Abstractions.Models;
using JZVerse.Business.Central.User.Arguments;
using JZVerse.Business.Central.User.Database;
using JZVerse.Business.Central.User.Results;
using JZVerse.DataAccess.Abstractions;

namespace JZVerse.Business.Central.User.Services;

/// <summary>
/// 管理员服务
/// </summary>
public sealed class AdminService
{
    private readonly IDbExecutor _db;
    private readonly ISnowflakeIdGenerator _idGenerator;
    private readonly IJwtTokenService _jwtTokenService;

    public AdminService(
        IDbExecutor db,
        ISnowflakeIdGenerator idGenerator,
        IJwtTokenService jwtTokenService)
    {
        _db = db;
        _idGenerator = idGenerator;
        _jwtTokenService = jwtTokenService;
    }

    /// <summary>
    /// 管理员登录
    /// </summary>
    public async Task<ResultAdminLogin> LoginAsync(ArgAdminLogin arg, string? loginIp, string? loginDevice)
    {
        var admin = await _db.QueryFirstOrDefaultAsync<AdminEntity>(
            UserSql.GetAdminByUsername,
            new { arg.Username });

        if (admin is null)
            throw new AuthenticationException("用户名或密码错误");

        if (!BCrypt.Net.BCrypt.Verify(arg.Password, admin.PasswordHash))
            throw new AuthenticationException("用户名或密码错误");

        if (admin.Status == AdminStatus.Frozen)
            throw new AuthenticationException("账号已被冻结");

        // 更新登录信息
        await _db.ExecuteAsync(UserSql.UpdateAdminLoginInfo, new
        {
            Id = admin.Id,
            LastLoginAt = DateTime.UtcNow,
            LastLoginIp = loginIp,
            LastLoginDevice = loginDevice,
            UpdatedAt = DateTime.UtcNow
        });

        // 生成 Token
        var additionalClaims = new Dictionary<string, string>
        {
            ["admin_type"] = ((int)admin.Type).ToString()
        };

        var accessToken = await _jwtTokenService.GenerateAccessTokenAsync(
            admin.Id, admin.Username, ["admin", admin.Type.ToString().ToLower()], additionalClaims);
        var refreshToken = await _jwtTokenService.GenerateRefreshTokenAsync(admin.Id);

        return new ResultAdminLogin
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = 7200, // 2 小时
            Admin = MapToAdminInfo(admin)
        };
    }

    /// <summary>
    /// 创建管理员
    /// </summary>
    public async Task<ResultAdminInfo> CreateAsync(ArgCreateAdmin arg)
    {
        var exists = await _db.ExecuteScalarAsync<bool>(
            UserSql.CheckAdminUsernameExists,
            new { arg.Username });

        if (exists)
            throw new ValidationException("用户名已存在");

        var admin = new AdminEntity
        {
            Id = _idGenerator.GenerateGuid(),
            Username = arg.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(arg.Password, workFactor: 12),
            Email = arg.Email,
            Phone = arg.Phone,
            Type = AdminType.Admin,
            Status = AdminStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        await _db.ExecuteAsync(UserSql.CreateAdmin, admin);

        return MapToAdminInfo(admin);
    }

    /// <summary>
    /// 获取管理员信息
    /// </summary>
    public async Task<ResultAdminInfo?> GetByIdAsync(Guid id)
    {
        var admin = await _db.QueryFirstOrDefaultAsync<AdminEntity>(
            UserSql.GetAdminById,
            new { Id = id });

        return admin is null ? null : MapToAdminInfo(admin);
    }

    /// <summary>
    /// 更新管理员密码
    /// </summary>
    public async Task<bool> UpdatePasswordAsync(Guid adminId, ArgUpdateAdminPassword arg)
    {
        var admin = await _db.QueryFirstOrDefaultAsync<AdminEntity>(
            UserSql.GetAdminById,
            new { Id = adminId });

        if (admin is null)
            throw new ResourceNotFoundException("管理员不存在");

        if (!BCrypt.Net.BCrypt.Verify(arg.OldPassword, admin.PasswordHash))
            throw new ValidationException("原密码错误");

        var newPasswordHash = BCrypt.Net.BCrypt.HashPassword(arg.NewPassword, workFactor: 12);

        var affected = await _db.ExecuteAsync(UserSql.UpdateAdminPassword, new
        {
            Id = adminId,
            PasswordHash = newPasswordHash,
            UpdatedAt = DateTime.UtcNow
        });

        return affected > 0;
    }

    /// <summary>
    /// 更新管理员资料
    /// </summary>
    public async Task<bool> UpdateProfileAsync(Guid adminId, ArgUpdateAdminProfile arg)
    {
        var affected = await _db.ExecuteAsync(UserSql.UpdateAdminProfile, new
        {
            Id = adminId,
            arg.Email,
            arg.Phone,
            arg.Avatar,
            UpdatedAt = DateTime.UtcNow
        });

        return affected > 0;
    }

    /// <summary>
    /// 冻结/解冻管理员
    /// </summary>
    public async Task<bool> UpdateStatusAsync(Guid adminId, AdminStatus status)
    {
        var affected = await _db.ExecuteAsync(UserSql.UpdateAdminStatus, new
        {
            Id = adminId,
            Status = status,
            UpdatedAt = DateTime.UtcNow
        });

        return affected > 0;
    }

    /// <summary>
    /// 获取管理员列表
    /// </summary>
    public async Task<PagedResult<ResultAdminInfo>> GetListAsync(int pageIndex, int pageSize)
    {
        var offset = (pageIndex - 1) * pageSize;

        var admins = await _db.QueryAsync<AdminEntity>(
            UserSql.GetAdminList,
            new { Limit = pageSize, Offset = offset });

        var total = await _db.ExecuteScalarAsync<int>(UserSql.GetAdminCount);

        return new PagedResult<ResultAdminInfo>
        {
            Items = admins.Select(MapToAdminInfo).ToList(),
            TotalCount = total,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
    }

    private static ResultAdminInfo MapToAdminInfo(AdminEntity entity) => new()
    {
        Id = entity.Id,
        Username = entity.Username,
        Email = entity.Email,
        Phone = entity.Phone,
        Avatar = entity.Avatar,
        Type = entity.Type,
        Status = entity.Status,
        LastLoginAt = entity.LastLoginAt,
        LastLoginIp = entity.LastLoginIp,
        CreatedAt = entity.CreatedAt
    };
}
