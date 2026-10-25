using Volo.Abp.Modularity;
namespace ProsperaAssess;

[DependsOn(typeof(Volo.Abp.Authorization.AbpAuthorizationModule), typeof(ProsperaAssessDomainSharedModule), typeof(Volo.Abp.Application.AbpDddApplicationContractsModule))]
public class ProsperaAssessApplicationContractsModule : AbpModule { }
