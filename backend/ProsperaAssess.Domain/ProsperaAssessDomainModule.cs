using Volo.Abp.Modularity;
namespace ProsperaAssess;

[DependsOn(typeof(ProsperaAssessDomainSharedModule), typeof(Volo.Abp.Domain.AbpDddDomainModule))]
public class ProsperaAssessDomainModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context) => Configure<Volo.Abp.Timing.AbpClockOptions>(options => options.Kind = DateTimeKind.Utc);
}
