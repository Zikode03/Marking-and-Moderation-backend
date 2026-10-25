using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;
namespace ProsperaAssess;

[ApiController]
public class PortalController(IAssessmentAppService assessment, ISubmissionAppService submission, IAcademicAppService academic, IScriptBatchAppService scriptbatch, ITrainingAppService training, IStaffAppService staff) : AbpController
{
    private static IActionResult Convert(OperationResult result)
    {
        if (result.Content is not null) return new FileContentResult(result.Content, result.ContentType ?? "application/octet-stream") { FileDownloadName = result.FileName };
        if (result.Location is not null) return new CreatedResult(result.Location, result.Data);
        return new ObjectResult(result.Data) { StatusCode = result.StatusCode };
    }
    [HttpGet("/api/assessments/{id:guid}")]
    public async Task<IActionResult> AssessmentGet1Async(Guid id) => Convert(await assessment.GetAsync(id));
    [HttpGet("/api/assessments")]
    public async Task<IActionResult> AssessmentGet2Async() => Convert(await assessment.GetListAsync());
    [HttpGet("/api/schools/grades")]
    public async Task<IActionResult> AssessmentGet3Async() => Convert(await assessment.GetGradesAsync());
    [HttpGet("/api/schools/assessments")]
    public async Task<IActionResult> AssessmentGet4Async() => Convert(await assessment.GetSchoolListAsync());
    [HttpPost("/api/assessments")]
    public async Task<IActionResult> AssessmentPost5Async() => Convert(await assessment.CreateAsync());
    [HttpGet("/api/submissions/{id:guid}")]
    public async Task<IActionResult> SubmissionGet1Async(Guid id) => Convert(await submission.GetAsync(id));
    [HttpGet("/api/submissions")]
    public async Task<IActionResult> SubmissionGet2Async(Guid? assessmentId) => Convert(await submission.GetListAsync(assessmentId));
    [HttpPost("/api/submissions")]
    public async Task<IActionResult> SubmissionPost3Async([FromBody] CreateSubmission request) => Convert(await submission.RegisterLegacyAsync(request));
    [HttpPost("/api/submissions/{id:guid}/mark")]
    public async Task<IActionResult> SubmissionPost4Async(Guid id, [FromBody] MarkRequest request) => Convert(await submission.MarkAsync(id, request));
    [HttpPost("/api/submissions/{id:guid}/moderate")]
    public async Task<IActionResult> SubmissionPost5Async(Guid id, [FromBody] ModerationRequest request) => Convert(await submission.ModerateAsync(id, request));
    [HttpPost("/api/submissions/{id:guid}/approve")]
    public async Task<IActionResult> SubmissionPost6Async(Guid id) => Convert(await submission.ApproveAsync(id));
    [HttpPost("/api/submissions/{id:guid}/ai-suggestion")]
    public async Task<IActionResult> SubmissionPost7Async(Guid id) => Convert(await submission.GetAiSuggestionAsync(id));
    [HttpGet("/api/academics")]
    public async Task<IActionResult> AcademicGet1Async(string organisationType) => Convert(await academic.GetStructureAsync(organisationType));
    [HttpPost("/api/academics/faculties")]
    public async Task<IActionResult> AcademicPost2Async([FromBody] CreateFaculty request) => Convert(await academic.CreateFacultyAsync(request));
    [HttpPut("/api/academics/faculties/{id:guid}")]
    public async Task<IActionResult> AcademicPut3Async(Guid id, [FromBody] CreateFaculty request) => Convert(await academic.UpdateFacultyAsync(id, request));
    [HttpPost("/api/academics/modules")]
    public async Task<IActionResult> AcademicPost4Async([FromBody] CreateModule request) => Convert(await academic.CreateModuleAsync(request));
    [HttpPut("/api/academics/modules/{id:guid}")]
    public async Task<IActionResult> AcademicPut5Async(Guid id, [FromBody] CreateModule request) => Convert(await academic.UpdateModuleAsync(id, request));
    [HttpPost("/api/academics/allocations")]
    public async Task<IActionResult> AcademicPost6Async([FromBody] CreateAllocation request) => Convert(await academic.AllocateStaffAsync(request));
    [HttpDelete("/api/academics/allocations/{id:guid}")]
    public async Task<IActionResult> AcademicDelete7Async(Guid id) => Convert(await academic.RemoveAllocationAsync(id));
    [HttpGet("/api/ai/status")]
    public async Task<IActionResult> ScriptBatchGet1Async(CancellationToken ct) => Convert(await scriptbatch.GetModelStatusAsync(ct));
    [HttpGet("/api/ai/batches")]
    public async Task<IActionResult> ScriptBatchGet2Async() => Convert(await scriptbatch.GetListAsync());
    [HttpGet("/api/ai/batches/{id:guid}/results")]
    public async Task<IActionResult> ScriptBatchGet3Async(Guid id) => Convert(await scriptbatch.ExportApprovedResultsAsync(id));
    [HttpGet("/api/ai/batches/{id:guid}")]
    public async Task<IActionResult> ScriptBatchGet4Async(Guid id) => Convert(await scriptbatch.GetAsync(id));
    [HttpPost("/api/ai/batches/{id:guid}/start")]
    public async Task<IActionResult> ScriptBatchPost5Async(Guid id, CancellationToken ct) => Convert(await scriptbatch.StartMarkingAsync(id, ct));
    [HttpGet("/api/ai/batches/{id:guid}/files/{file}")]
    public async Task<IActionResult> ScriptBatchGet6Async(Guid id, string file) => Convert(await scriptbatch.DownloadFileAsync(id, file));
    [HttpPost("/api/ai/batches/{id:guid}/scripts/{scriptId:guid}/review")]
    public async Task<IActionResult> ScriptBatchReviewAsync(Guid id, Guid scriptId, [FromBody] AiReview review) => Convert(await scriptbatch.ReviewAsync(id, scriptId, review));
    [HttpPost("/api/ai/batches/{id:guid}/scripts/{scriptId:guid}/moderate")]
    public async Task<IActionResult> ScriptBatchPost7Async(Guid id, Guid scriptId, [FromBody] AiReview review) => Convert(await scriptbatch.ModerateAsync(id, scriptId, review));
    [HttpPost("/api/ai/batches/{id:guid}/scripts/{scriptId:guid}/approve")]
    public async Task<IActionResult> ScriptBatchPost8Async(Guid id, Guid scriptId, [FromBody] AiReview review) => Convert(await scriptbatch.ApproveAsync(id, scriptId, review));
    [HttpGet("/api/ai/batches/{id:guid}/training")]
    public async Task<IActionResult> TrainingGet1Async(Guid id) => Convert(await training.GetListAsync(id));
    [HttpPost("/api/ai/batches/{id:guid}/scripts/{scriptId:guid}/training")]
    public async Task<IActionResult> TrainingPost2Async(Guid id, Guid scriptId, [FromBody] TrainingLabel label) => Convert(await training.CreateExampleAsync(id, scriptId, label));
    [HttpPost("/api/ai/batches/{id:guid}/training/{exampleId:guid}/verify")]
    public async Task<IActionResult> TrainingPost3Async(Guid id, Guid exampleId) => Convert(await training.VerifyExampleAsync(id, exampleId));
    [HttpGet("/api/ai/batches/{id:guid}/training/export")]
    public async Task<IActionResult> TrainingGet4Async(Guid id) => Convert(await training.ExportVerifiedExamplesAsync(id));
    [HttpGet("/api/me")]
    public async Task<IActionResult> StaffGet1Async() => Convert(await staff.GetProfileAsync());
    [HttpPost("/api/bootstrap/staff")]
    public async Task<IActionResult> StaffPost2Async([FromBody] CreateStaffRequest request) => Convert(await staff.CreateAsync(request));
    [Microsoft.AspNetCore.Authorization.Authorize(ProsperaPermissions.Upload)]
    [HttpPost("/api/ai/batches")]
    [RequestSizeLimit(60 * 1024 * 1024)]
    public async Task<IActionResult> UploadAsync(CancellationToken ct)
    {
        if (!Request.HasFormContentType) return BadRequest(new { error = "Upload multipart files." });
        var form = await Request.ReadFormAsync(ct); AiUploadContext? context;
        try { context = System.Text.Json.JsonSerializer.Deserialize<AiUploadContext>(form["context"].ToString(), AppJson.Options); } catch (System.Text.Json.JsonException) { return BadRequest(new { error = "Invalid upload context." }); }
        if (context is null) return BadRequest(new { error = "Academic context is required." });
        var files = new List<UploadFileDto>();
        foreach (var file in form.Files) { if (file.Length > 10 * 1024 * 1024) return BadRequest(new { error = "Each file must be under 10 MB." }); using var stream = new MemoryStream(); await file.CopyToAsync(stream, ct); files.Add(new(file.Name, file.FileName, stream.ToArray())); }
        return Convert(await scriptbatch.UploadAsync(new(context, files), ct));
    }
}
