using Microsoft.AspNetCore.Identity;
using Volo.Abp;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
namespace ProsperaAssess;

// ABP permission management providers for the installed ASP.NET Identity account store.
public class LegacyRolePermissionManagementProvider(IPermissionGrantRepository grants, IGuidGenerator guids, ICurrentTenant tenant, RoleManager<IdentityRole> roles)
    : PermissionManagementProvider(grants, guids, tenant)
{
    public override string Name => "R";
    public override async Task SetAsync(string permissionName, string providerKey, bool isGranted)
    {
        if (!await roles.RoleExistsAsync(providerKey)) throw new BusinessException("Prospera:RoleNotFound");
        await base.SetAsync(permissionName, providerKey, isGranted);
    }
}
public class LegacyUserPermissionManagementProvider(IPermissionGrantRepository grants, IGuidGenerator guids, ICurrentTenant tenant, UserManager<AppUser> users)
    : PermissionManagementProvider(grants, guids, tenant)
{
    public override string Name => "U";
    public override async Task SetAsync(string permissionName, string providerKey, bool isGranted)
    {
        if (await users.FindByIdAsync(providerKey) is null) throw new BusinessException("Prospera:UserNotFound");
        await base.SetAsync(permissionName, providerKey, isGranted);
    }
}
