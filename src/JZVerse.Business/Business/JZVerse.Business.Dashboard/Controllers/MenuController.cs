using JZVerse.Business.Dashboard.Arguments;
using JZVerse.Business.Dashboard.Results;
using JZVerse.Business.Dashboard.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JZVerse.Business.Dashboard.Controllers;

/// <summary>
/// 菜单权限控制器
/// </summary>
[ApiController]
[Route("api/v1/dashboard/menu")]
[Authorize(Roles = "admin")]
public sealed class MenuController : ControllerBase
{
    private readonly MenuService _menuService;

    public MenuController(MenuService menuService)
    {
        _menuService = menuService;
    }

    #region Menu

    /// <summary>
    /// 保存菜单
    /// </summary>
    [HttpPost]
    public async Task<Guid> SaveMenuAsync([FromBody] ArgSaveMenu arg)
    {
        return await _menuService.SaveMenuAsync(arg);
    }

    /// <summary>
    /// 获取菜单详情
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ResultMenuInfo?> GetMenuByIdAsync(Guid id)
    {
        return await _menuService.GetMenuByIdAsync(id);
    }

    /// <summary>
    /// 获取菜单树
    /// </summary>
    [HttpGet("tree")]
    public async Task<IReadOnlyList<ResultMenuInfo>> GetMenuTreeAsync()
    {
        return await _menuService.GetMenuTreeAsync();
    }

    /// <summary>
    /// 删除菜单
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<bool> DeleteMenuAsync(Guid id)
    {
        return await _menuService.DeleteMenuAsync(id);
    }

    #endregion

    #region Role

    /// <summary>
    /// 保存角色
    /// </summary>
    [HttpPost("role")]
    public async Task<Guid> SaveRoleAsync([FromBody] ArgSaveRole arg)
    {
        return await _menuService.SaveRoleAsync(arg);
    }

    /// <summary>
    /// 获取角色详情
    /// </summary>
    [HttpGet("role/{id:guid}")]
    public async Task<ResultRoleInfo?> GetRoleByIdAsync(Guid id)
    {
        return await _menuService.GetRoleByIdAsync(id);
    }

    /// <summary>
    /// 获取所有角色
    /// </summary>
    [HttpGet("role")]
    public async Task<IReadOnlyList<ResultRoleInfo>> GetAllRolesAsync()
    {
        return await _menuService.GetAllRolesAsync();
    }

    /// <summary>
    /// 删除角色
    /// </summary>
    [HttpDelete("role/{id:guid}")]
    public async Task<bool> DeleteRoleAsync(Guid id)
    {
        return await _menuService.DeleteRoleAsync(id);
    }

    /// <summary>
    /// 分配菜单给角色
    /// </summary>
    [HttpPost("role/assign-menus")]
    public async Task<bool> AssignMenusToRoleAsync([FromBody] ArgAssignMenusToRole arg)
    {
        return await _menuService.AssignMenusToRoleAsync(arg);
    }

    /// <summary>
    /// 获取角色的菜单
    /// </summary>
    [HttpGet("role/{roleId:guid}/menus")]
    public async Task<IReadOnlyList<ResultMenuInfo>> GetRoleMenusAsync(Guid roleId)
    {
        return await _menuService.GetRoleMenusAsync(roleId);
    }

    #endregion
}
