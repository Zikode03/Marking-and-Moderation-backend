using Volo.Abp.Application.Services;
namespace ProsperaAssess;

public interface IAssessmentAppService : IApplicationService
{
    Task<OperationResult> GetAsync(Guid id);
    Task<OperationResult> GetListAsync();
    Task<OperationResult> GetGradesAsync();
    Task<OperationResult> GetSchoolListAsync();
    Task<OperationResult> CreateAsync();
}