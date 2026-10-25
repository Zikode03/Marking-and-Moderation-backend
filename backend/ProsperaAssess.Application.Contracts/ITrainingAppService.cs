using Volo.Abp.Application.Services;
namespace ProsperaAssess;

public interface ITrainingAppService : IApplicationService
{
    Task<OperationResult> GetListAsync(Guid id);
    Task<OperationResult> CreateExampleAsync(Guid id, Guid scriptId, TrainingLabel label);
    Task<OperationResult> VerifyExampleAsync(Guid id, Guid exampleId);
    Task<OperationResult> ExportVerifiedExamplesAsync(Guid id);
}