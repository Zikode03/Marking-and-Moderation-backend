using Volo.Abp.Modularity;
namespace ProsperaAssess;

[DependsOn(typeof(Volo.Abp.PermissionManagement.AbpPermissionManagementDomainModule), typeof(ProsperaAssessDomainModule), typeof(ProsperaAssessApplicationContractsModule), typeof(Volo.Abp.Application.AbpDddApplicationModule))]
public class ProsperaAssessApplicationModule : AbpModule { }
