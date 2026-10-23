using Volo.Abp.AspNetCore;
using Volo.Abp.Autofac;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace ProsperaAssess.Api;

/// <summary>
/// Initial ABP host module. Legacy ASP.NET Core Identity and EF Core repositories
/// continue to serve the current endpoints during a controlled migration.
/// </summary>
[DependsOn(
    typeof(AbpAspNetCoreModule),
    typeof(AbpAutofacModule),
    typeof(AbpEntityFrameworkCoreModule)
)]
public class ProsperaAssessModule : AbpModule
{
}
