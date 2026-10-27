using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Auditing;
namespace ProsperaAssess;

[ApiController, Authorize(ProsperaPermissions.ManageAcademics)]
[Route("api/data-protection")]
public class DataProtectionController(IDataProtectionAppService service) : AbpController
{
    private static IActionResult Convert(OperationResult result) => new ObjectResult(result.Data) {StatusCode=result.StatusCode};
    [HttpGet] public async Task<IActionResult> GetAsync() => Convert(await service.GetAsync());
    [HttpPost("batches/{id:guid}/hold")] public async Task<IActionResult> HoldAsync(Guid id, PrivacyDecisionInput input) => Convert(await service.HoldAsync(id,input));
    [HttpPost("batches/{id:guid}/training-authorisation")] public async Task<IActionResult> TrainingAsync(Guid id, PrivacyDecisionInput input) => Convert(await service.TrainingAsync(id,input));
    [HttpPost("batches/{id:guid}/purge")] public async Task<IActionResult> PurgeAsync(Guid id, RetentionDeleteInput input) => Convert(await service.PurgeAsync(id,input));
}
