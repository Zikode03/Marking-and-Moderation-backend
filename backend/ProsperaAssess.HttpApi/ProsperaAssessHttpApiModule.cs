using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;
using Volo.Abp.AspNetCore.Mvc;
namespace ProsperaAssess;

[DependsOn(typeof(ProsperaAssessApplicationContractsModule), typeof(AbpAspNetCoreMvcModule))]
public class ProsperaAssessHttpApiModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddControllers(options => options.Filters.Add<PortalExceptionFilter>()).AddApplicationPart(typeof(PortalController).Assembly);
    }
}
