using Volo.Abp.Modularity;
using Volo.Abp.Autofac;
using Volo.Abp.Auditing;
using Microsoft.Extensions.DependencyInjection;
using ProsperaAssess;
namespace ProsperaAssess.Api;

[DependsOn(typeof(ProsperaAssessApplicationModule), typeof(ProsperaAssessEntityFrameworkCoreModule), typeof(ProsperaAssessHttpApiModule), typeof(AbpAutofacModule), typeof(Volo.Abp.BackgroundWorkers.AbpBackgroundWorkersModule))]
public class ProsperaAssessHttpApiHostModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpAuditingOptions>(o => { o.ApplicationName = "ProsperaAssess"; o.IgnoredTypes.Add(typeof(LoginRequest)); o.IgnoredTypes.Add(typeof(RefreshRequest)); o.IgnoredTypes.Add(typeof(UploadBatchDto)); o.IgnoredTypes.Add(typeof(CreateStaffRequest)); o.IgnoredTypes.Add(typeof(TrainingLabel)); });
    }
}
