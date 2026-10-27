using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;
namespace ProsperaAssess;

[ApiController]
public class InstitutionController(IInstitutionAppService service) : AbpController
{
    private static IActionResult Convert(OperationResult result) => result.Content is not null ? new FileContentResult(result.Content, result.ContentType!) { FileDownloadName = result.FileName } : new ObjectResult(result.Data) { StatusCode = result.StatusCode };
    [HttpGet("/api/institution/records/{kind}")]
    public async Task<IActionResult> Records(string kind) => Convert(await service.GetAsync(kind));
    [HttpPost("/api/institution/records/{kind}")]
    public async Task<IActionResult> Create(string kind, [FromBody] ManagedRecordInput input) => Convert(await service.SaveAsync(kind, null, input));
    [HttpPut("/api/institution/records/{kind}/{id:guid}")]
    public async Task<IActionResult> Update(string kind, Guid id, [FromBody] ManagedRecordInput input) => Convert(await service.SaveAsync(kind, id, input));
    [HttpPost("/api/institution/import/{kind}")]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<IActionResult> Import(string kind, [FromBody] List<ManagedRecordInput> records) => Convert(await service.ImportAsync(kind, records));
    [HttpPost("/api/institution/samples/{percentage:int}")]
    public async Task<IActionResult> Sample(int percentage) => Convert(await service.SampleAsync(percentage));
    [HttpGet("/api/institution/audit")]
    public async Task<IActionResult> Audit(string? search) => Convert(await service.AuditAsync(search));
    [HttpGet("/api/institution/tasks")]
    public async Task<IActionResult> Tasks() => Convert(await service.TasksAsync());
    [HttpGet("/api/institution/results")]
    public async Task<IActionResult> Results(string? candidate) => Convert(await service.ReportAsync(candidate));
    [HttpGet("/api/institution/staff")]
    public async Task<IActionResult> Staff() => Convert(await service.StaffAsync());
    [HttpPut("/api/institution/staff/{id}")]
    public async Task<IActionResult> StaffAccess(string id, [FromBody] StaffStatusInput input) => Convert(await service.SetStaffAsync(id, input));
    [HttpGet("/api/institution/documents")]
    public async Task<IActionResult> Documents() => Convert(await service.DocumentsAsync());
    [HttpPost("/api/institution/documents/{batchId:guid}/approve")]
    public async Task<IActionResult> ApproveMemo(Guid batchId) => Convert(await service.ApproveMemoAsync(batchId));
    [HttpPut("/api/institution/assignments/{scriptId:guid}")]
    public async Task<IActionResult> Assign(Guid scriptId, [FromBody] AssignmentInput input) => Convert(await service.AssignAsync(scriptId, input));
}
