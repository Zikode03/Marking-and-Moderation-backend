using Volo.Abp.Application.Services;
namespace ProsperaAssess;

public interface ISubmissionAppService : IApplicationService
{
    Task<OperationResult> GetAsync(Guid id);
    Task<OperationResult> GetListAsync(Guid? assessmentId);
    Task<OperationResult> RegisterLegacyAsync(CreateSubmission request);
    Task<OperationResult> MarkAsync(Guid id, MarkRequest request);
    Task<OperationResult> ModerateAsync(Guid id, ModerationRequest request);
    Task<OperationResult> ApproveAsync(Guid id);
    Task<OperationResult> GetAiSuggestionAsync(Guid id);
}