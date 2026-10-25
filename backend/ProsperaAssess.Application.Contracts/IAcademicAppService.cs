using Volo.Abp.Application.Services;
namespace ProsperaAssess;

public interface IAcademicAppService : IApplicationService
{
    Task<OperationResult> GetStructureAsync(string organisationType);
    Task<OperationResult> CreateFacultyAsync(CreateFaculty request);
    Task<OperationResult> UpdateFacultyAsync(Guid id, CreateFaculty request);
    Task<OperationResult> CreateModuleAsync(CreateModule request);
    Task<OperationResult> UpdateModuleAsync(Guid id, CreateModule request);
    Task<OperationResult> AllocateStaffAsync(CreateAllocation request);
    Task<OperationResult> RemoveAllocationAsync(Guid id);
}