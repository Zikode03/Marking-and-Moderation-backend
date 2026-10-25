using Volo.Abp.Modularity;
using Volo.Abp.Localization;
using Volo.Abp.VirtualFileSystem;
namespace ProsperaAssess;

[DependsOn(typeof(Volo.Abp.Domain.AbpDddDomainSharedModule), typeof(AbpLocalizationModule), typeof(AbpVirtualFileSystemModule))]
public class ProsperaAssessDomainSharedModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpVirtualFileSystemOptions>(o => o.FileSets.AddEmbedded<ProsperaAssessDomainSharedModule>("ProsperaAssess"));
        Configure<AbpLocalizationOptions>(o => o.Resources.Add<ProsperaAssessResource>("en").AddVirtualJson("/Localization/Prospera"));
    }
}
