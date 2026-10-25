using Volo.Abp.Application.Services;
namespace ProsperaAssess;

public interface IStaffAppService : IApplicationService
{
    Task<OperationResult> GetProfileAsync();
    Task<OperationResult> CreateAsync(CreateStaffRequest request);
}