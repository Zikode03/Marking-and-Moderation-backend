using Volo.Abp.Application.Services;
namespace ProsperaAssess;

public interface IScriptBatchAppService : IApplicationService
{
    Task<OperationResult> GetModelStatusAsync(CancellationToken ct);
    Task<OperationResult> GetListAsync();
    Task<OperationResult> ExportApprovedResultsAsync(Guid id);
    Task<OperationResult> GetAsync(Guid id);
    Task<OperationResult> StartMarkingAsync(Guid id, CancellationToken ct);
    Task<OperationResult> DownloadFileAsync(Guid id, string file);
    Task<OperationResult> ModerateAsync(Guid id, Guid scriptId, AiReview review);
    Task<OperationResult> ApproveAsync(Guid id, Guid scriptId, AiReview review);
    Task<OperationResult> UploadAsync(UploadBatchDto request, CancellationToken ct);
}