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
        Configure<AbpAuditingOptions>(o => { o.ApplicationName = "ProsperaAssess"; foreach (var type in new[] { typeof(AiReview), typeof(ManagedRecordInput), typeof(PrivacyDecisionInput), typeof(RetentionDeleteInput), typeof(SendInvitationEmailInput), typeof(RecoveryEmailInput), typeof(ResetPasswordInput), typeof(PasswordChangeInput), typeof(PasswordProof), typeof(MfaProof), typeof(MfaDisableInput) }) o.IgnoredTypes.Add(type); o.IgnoredTypes.Add(typeof(AcceptInvitationInput)); o.IgnoredTypes.Add(typeof(LoginRequest)); o.IgnoredTypes.Add(typeof(RefreshRequest)); o.IgnoredTypes.Add(typeof(UploadBatchDto)); o.IgnoredTypes.Add(typeof(CreateStaffRequest)); o.IgnoredTypes.Add(typeof(TrainingLabel)); });
    }
}
