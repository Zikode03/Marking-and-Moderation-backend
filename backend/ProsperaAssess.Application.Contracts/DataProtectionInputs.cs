using Volo.Abp.Application.Services;
namespace ProsperaAssess;

public record RetentionDeleteInput(string PreviewToken, string Confirmation);
public record PrivacyDecisionInput(bool Enabled, string Reason);
public interface IDataProtectionAppService : IApplicationService
{
    Task<OperationResult> GetAsync();
    Task<OperationResult> HoldAsync(Guid id, PrivacyDecisionInput input);
    Task<OperationResult> TrainingAsync(Guid id, PrivacyDecisionInput input);
    Task<OperationResult> PurgeAsync(Guid id, RetentionDeleteInput input);
}
