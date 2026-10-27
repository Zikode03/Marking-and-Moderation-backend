using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.SqlServer;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement.EntityFrameworkCore;
using Volo.Abp.AuditLogging.EntityFrameworkCore;
namespace ProsperaAssess;

[DependsOn(typeof(ProsperaAssessDomainModule), typeof(AbpEntityFrameworkCoreSqlServerModule), typeof(AbpPermissionManagementEntityFrameworkCoreModule), typeof(AbpAuditLoggingEntityFrameworkCoreModule))]
public class ProsperaAssessEntityFrameworkCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpDbContext<AssessDb>(options => options.AddDefaultRepositories(includeAllEntities: true));
        Configure<AbpDbContextOptions>(options => options.UseSqlServer());
        Configure<Volo.Abp.PermissionManagement.PermissionManagementOptions>(options => { options.ManagementProviders.Add<LegacyRolePermissionManagementProvider>(); options.ManagementProviders.Add<LegacyUserPermissionManagementProvider>(); });
        context.Services.AddIdentityCore<AppUser>(o => { o.User.RequireUniqueEmail = true; o.Password.RequiredLength = 12; }).AddRoles<IdentityRole>().AddEntityFrameworkStores<AssessDb>();
        context.Services.AddTransient<IUserStore<AppUser>, PortalUserStore>();
        context.Services.AddTransient<IStaffIdentityService, StaffIdentityService>();
        context.Services.AddTransient<IAiQueueRepository, AiQueueRepository>();
    }
}
