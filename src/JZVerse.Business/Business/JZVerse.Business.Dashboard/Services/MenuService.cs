using JZVerse.Business.Abstractions.Models;
using JZVerse.Business.Dashboard.Arguments;
using JZVerse.Business.Dashboard.Database;
using JZVerse.Business.Dashboard.Results;
using JZVerse.MicroHuaxia.DataAccess.Abstractions;

namespace JZVerse.Business.Dashboard.Services;

/// <summary>
/// 菜单权限服务
/// </summary>
public sealed class MenuService
{
    private readonly IDbExecutor _db;
    private readonly ISnowflakeIdGenerator _idGenerator;

    public MenuService(IDbExecutor db, ISnowflakeIdGenerator idGenerator)
    {
        _db = db;
        _idGenerator = idGenerator;
    }

    #region Menu Management

    /// <summary>
    /// 保存菜单
    /// </summary>
    public async Task<Guid> SaveMenuAsync(ArgSaveMenu arg)
    {
        var isNew = !arg.Id.HasValue || arg.Id.Value == Guid.Empty;
        var menuId = isNew ? _idGenerator.GenerateGuid() : arg.Id!.Value;

        if (isNew)
        {
            var menu = new MenuEntity
            {
                Id = menuId,
                ParentId = arg.ParentId,
                Name = arg.Name,
                Icon = arg.Icon,
                Path = arg.Path,
                Component = arg.Component,
                Permission = arg.Permission,
                SortOrder = arg.SortOrder,
                IsVisible = arg.IsVisible,
                IsEnabled = arg.IsEnabled,
                CreatedAt = DateTime.UtcNow
            };

            await _db.ExecuteAsync(DashboardSql.CreateMenu, menu);
        }
        else
        {
            await _db.ExecuteAsync(DashboardSql.UpdateMenu, new
            {
                Id = menuId,
                arg.Name,
                arg.Icon,
                arg.Path,
                arg.Component,
                arg.Permission,
                arg.SortOrder,
                arg.IsVisible,
                arg.IsEnabled,
                UpdatedAt = DateTime.UtcNow
            });
        }

        return menuId;
    }

    /// <summary>
    /// 获取菜单详情
    /// </summary>
    public async Task<ResultMenuInfo?> GetMenuByIdAsync(Guid id)
    {
        var menu = await _db.QueryFirstOrDefaultAsync<MenuEntity>(
            DashboardSql.GetMenuById,
            new { Id = id });

        return menu is null ? null : MapToMenuResult(menu);
    }

    /// <summary>
    /// 获取菜单树
    /// </summary>
    public async Task<IReadOnlyList<ResultMenuInfo>> GetMenuTreeAsync()
    {
        var menus = await _db.QueryAsync<MenuEntity>(DashboardSql.GetAllMenus);
        return BuildMenuTree(menus);
    }

    /// <summary>
    /// 删除菜单
    /// </summary>
    public async Task<bool> DeleteMenuAsync(Guid id)
    {
        // 检查是否有子菜单
        var children = await _db.QueryAsync<MenuEntity>(
            DashboardSql.GetMenusByParentId,
            new { ParentId = id });

        if (children.Any())
            throw new BusinessException(400, "该菜单下存在子菜单，无法删除");

        var affected = await _db.ExecuteAsync(DashboardSql.DeleteMenu, new { Id = id });
        return affected > 0;
    }

    #endregion

    #region Role Management

    /// <summary>
    /// 保存角色
    /// </summary>
    public async Task<Guid> SaveRoleAsync(ArgSaveRole arg)
    {
        var isNew = !arg.Id.HasValue || arg.Id.Value == Guid.Empty;
        var roleId = isNew ? _idGenerator.GenerateGuid() : arg.Id!.Value;

        if (isNew)
        {
            // 检查编码是否存在
            var existingRole = await _db.QueryFirstOrDefaultAsync<RoleEntity>(
                DashboardSql.GetRoleByCode,
                new { arg.Code });

            if (existingRole is not null)
                throw new BusinessException(400, "角色编码已存在");

            var role = new RoleEntity
            {
                Id = roleId,
                Code = arg.Code,
                Name = arg.Name,
                Description = arg.Description,
                IsSystem = false,
                IsEnabled = arg.IsEnabled,
                CreatedAt = DateTime.UtcNow
            };

            await _db.ExecuteAsync(DashboardSql.CreateRole, role);
        }
        else
        {
            await _db.ExecuteAsync(DashboardSql.UpdateRole, new
            {
                Id = roleId,
                arg.Name,
                arg.Description,
                arg.IsEnabled,
                UpdatedAt = DateTime.UtcNow
            });
        }

        return roleId;
    }

    /// <summary>
    /// 获取角色详情
    /// </summary>
    public async Task<ResultRoleInfo?> GetRoleByIdAsync(Guid id)
    {
        var role = await _db.QueryFirstOrDefaultAsync<RoleEntity>(
            DashboardSql.GetRoleById,
            new { Id = id });

        if (role is null) return null;

        var menus = await _db.QueryAsync<MenuEntity>(
            DashboardSql.GetRoleMenus,
            new { RoleId = id });

        return new ResultRoleInfo
        {
            Id = role.Id,
            Code = role.Code,
            Name = role.Name,
            Description = role.Description,
            IsSystem = role.IsSystem,
            IsEnabled = role.IsEnabled,
            CreatedAt = role.CreatedAt,
            MenuIds = menus.Select(m => m.Id).ToList()
        };
    }

    /// <summary>
    /// 获取所有角色
    /// </summary>
    public async Task<IReadOnlyList<ResultRoleInfo>> GetAllRolesAsync()
    {
        var roles = await _db.QueryAsync<RoleEntity>(DashboardSql.GetAllRoles);

        var result = new List<ResultRoleInfo>();
        foreach (var role in roles)
        {
            var menus = await _db.QueryAsync<MenuEntity>(
                DashboardSql.GetRoleMenus,
                new { RoleId = role.Id });

            result.Add(new ResultRoleInfo
            {
                Id = role.Id,
                Code = role.Code,
                Name = role.Name,
                Description = role.Description,
                IsSystem = role.IsSystem,
                IsEnabled = role.IsEnabled,
                CreatedAt = role.CreatedAt,
                MenuIds = menus.Select(m => m.Id).ToList()
            });
        }

        return result;
    }

    /// <summary>
    /// 删除角色
    /// </summary>
    public async Task<bool> DeleteRoleAsync(Guid id)
    {
        var role = await _db.QueryFirstOrDefaultAsync<RoleEntity>(
            DashboardSql.GetRoleById,
            new { Id = id });

        if (role is null)
            throw new ResourceNotFoundException("角色", id);

        if (role.IsSystem)
            throw new BusinessException(400, "系统角色无法删除");

        // 先删除角色菜单关联
        await _db.ExecuteAsync(DashboardSql.RemoveMenusFromRole, new { RoleId = id });

        var affected = await _db.ExecuteAsync(DashboardSql.DeleteRole, new { Id = id });
        return affected > 0;
    }

    /// <summary>
    /// 分配菜单给角色
    /// </summary>
    public async Task<bool> AssignMenusToRoleAsync(ArgAssignMenusToRole arg)
    {
        // 先删除原有关联
        await _db.ExecuteAsync(DashboardSql.RemoveMenusFromRole, new { arg.RoleId });

        // 添加新关联
        foreach (var menuId in arg.MenuIds)
        {
            await _db.ExecuteAsync(DashboardSql.AssignMenusToRole, new
            {
                arg.RoleId,
                MenuId = menuId,
                CreatedAt = DateTime.UtcNow
            });
        }

        return true;
    }

    /// <summary>
    /// 获取角色的菜单
    /// </summary>
    public async Task<IReadOnlyList<ResultMenuInfo>> GetRoleMenusAsync(Guid roleId)
    {
        var menus = await _db.QueryAsync<MenuEntity>(
            DashboardSql.GetRoleMenus,
            new { RoleId = roleId });

        return BuildMenuTree(menus);
    }

    #endregion

    #region Helper Methods

    private static IReadOnlyList<ResultMenuInfo> BuildMenuTree(IEnumerable<MenuEntity> menus)
    {
        var lookup = menus.ToLookup(m => m.ParentId);

        IReadOnlyList<ResultMenuInfo> BuildChildren(Guid? parentId)
        {
            return lookup[parentId]
                .OrderBy(m => m.SortOrder)
                .Select(m => new ResultMenuInfo
                {
                    Id = m.Id,
                    ParentId = m.ParentId,
                    Name = m.Name,
                    Icon = m.Icon,
                    Path = m.Path,
                    Component = m.Component,
                    Permission = m.Permission,
                    SortOrder = m.SortOrder,
                    IsVisible = m.IsVisible,
                    IsEnabled = m.IsEnabled,
                    Children = BuildChildren(m.Id)
                })
                .ToList();
        }

        return BuildChildren(null);
    }

    private static ResultMenuInfo MapToMenuResult(MenuEntity entity) => new()
    {
        Id = entity.Id,
        ParentId = entity.ParentId,
        Name = entity.Name,
        Icon = entity.Icon,
        Path = entity.Path,
        Component = entity.Component,
        Permission = entity.Permission,
        SortOrder = entity.SortOrder,
        IsVisible = entity.IsVisible,
        IsEnabled = entity.IsEnabled
    };

    #endregion
}
