using System.Security.Claims;
using Volo.Abp.Application.Services;
using Volo.Abp.Security.Claims;
namespace ProsperaAssess;

public abstract class ProsperaAppService : ApplicationService
{
    protected ClaimsPrincipal Principal => LazyServiceProvider.LazyGetRequiredService<ICurrentPrincipalAccessor>().Principal!;
    protected IStaffIdentityService users => LazyServiceProvider.LazyGetRequiredService<IStaffIdentityService>();
    protected AcademicAccess access => LazyServiceProvider.LazyGetRequiredService<AcademicAccess>();
    protected IAiMarkingEngine marker => LazyServiceProvider.LazyGetRequiredService<IAiMarkingEngine>();
    protected Volo.Abp.Domain.Repositories.IRepository<AssessmentRow, Guid> AssessmentsRepository => LazyServiceProvider.LazyGetRequiredService<Volo.Abp.Domain.Repositories.IRepository<AssessmentRow, Guid>>();
    protected Volo.Abp.Domain.Repositories.IRepository<SubmissionRow, Guid> SubmissionsRepository => LazyServiceProvider.LazyGetRequiredService<Volo.Abp.Domain.Repositories.IRepository<SubmissionRow, Guid>>();
    protected Volo.Abp.Domain.Repositories.IRepository<FacultyRow, Guid> FacultiesRepository => LazyServiceProvider.LazyGetRequiredService<Volo.Abp.Domain.Repositories.IRepository<FacultyRow, Guid>>();
    protected Volo.Abp.Domain.Repositories.IRepository<ModuleRow, Guid> ModulesRepository => LazyServiceProvider.LazyGetRequiredService<Volo.Abp.Domain.Repositories.IRepository<ModuleRow, Guid>>();
    protected Volo.Abp.Domain.Repositories.IRepository<StaffAllocationRow, Guid> StaffAllocationsRepository => LazyServiceProvider.LazyGetRequiredService<Volo.Abp.Domain.Repositories.IRepository<StaffAllocationRow, Guid>>();
    protected Volo.Abp.Domain.Repositories.IRepository<AiBatch, Guid> AiBatchesRepository => LazyServiceProvider.LazyGetRequiredService<Volo.Abp.Domain.Repositories.IRepository<AiBatch, Guid>>();
    protected Volo.Abp.Domain.Repositories.IRepository<AiScript, Guid> AiScriptsRepository => LazyServiceProvider.LazyGetRequiredService<Volo.Abp.Domain.Repositories.IRepository<AiScript, Guid>>();
    protected Volo.Abp.Domain.Repositories.IRepository<AiTrainingExample, Guid> AiTrainingExamplesRepository => LazyServiceProvider.LazyGetRequiredService<Volo.Abp.Domain.Repositories.IRepository<AiTrainingExample, Guid>>();
}