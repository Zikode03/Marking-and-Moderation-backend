using System.Security.Claims;
using System.Text;
using System.Security.Cryptography;
using System.Text.Json;
namespace ProsperaAssess;

public class TrainingAppService : ProsperaAppService, ITrainingAppService
{
    [Microsoft.AspNetCore.Authorization.Authorize(ProsperaPermissions.Workspace)]
    public virtual async Task<OperationResult> GetListAsync(Guid id)
    {
        var p = Principal;
        var scriptQuery = await AiScriptsRepository.GetQueryableAsync();

        if (await Allowed(id, p, users, access) is null) return Results.NotFound();
        return Results.Ok(await AsyncExecuter.ToListAsync((await AiTrainingExamplesRepository.GetQueryableAsync()).Where(e => scriptQuery.Any(s => s.Id == e.ScriptId && s.BatchId == id))));

    }
    [Microsoft.AspNetCore.Authorization.Authorize(ProsperaPermissions.LabelTraining)]
    [Volo.Abp.Uow.UnitOfWork(true)]
    public virtual async Task<OperationResult> CreateExampleAsync(Guid id, Guid scriptId, TrainingLabel label)
    {
        var p = Principal;
        var scriptQuery = await AiScriptsRepository.GetQueryableAsync();

        if (await Allowed(id, p, users, access) is not { } user || !await AsyncExecuter.AnyAsync((await AiScriptsRepository.GetQueryableAsync()), s => s.Id == scriptId && s.BatchId == id)) return Results.NotFound();
        if (!InputRules.Text(label.QuestionNumber, 100) || !InputRules.Text(label.Question, 20000) || !InputRules.Text(label.Answer, 30000) || !InputRules.Text(label.Rubric, 20000) || label.MaxMarks <= 0 || label.MaxMarks > 1000 || label.Marks < 0 || label.Marks > label.MaxMarks)
            return Results.BadRequest(new { error = "Provide a question number, question, transcribed answer, approved marking guide, maximum and verified expert mark within range." });
        if (label.SourceMarked && (!label.LearnerWritingOnly || !InputRules.Text(label.PreparationNotes, 5000) || !InputRules.Text(label.DatasetScope, 300)))
            return Results.BadRequest(new { error = "For marked scripts confirm learner-only transcription, document annotation handling and declare the academic dataset scope." });
        var example = new AiTrainingExample(GuidGenerator.Create()) { ScriptId = scriptId, QuestionNumber = label.QuestionNumber!.Trim(), Question = label.Question!.Trim(), Answer = label.Answer!.Trim(), Rubric = label.Rubric!.Trim(), MaxMarks = label.MaxMarks, Marks = label.Marks, LabelledBy = user.Id, CreatedAt = DateTimeOffset.UtcNow };
        var sourceScript = await AsyncExecuter.FirstAsync((await AiScriptsRepository.GetQueryableAsync()), s => s.Id == scriptId && s.BatchId == id);
        var learnerReference = string.IsNullOrWhiteSpace(label.LearnerGroup) ? sourceScript.Candidate.Trim().ToUpperInvariant() : label.LearnerGroup.Trim();
        if (label.SourceMarked)
            await InstitutionRecords.InsertAsync(new InstitutionRecord(GuidGenerator.Create()) { SchoolId = user.SchoolId, Kind = "trainingPreparation", Code = example.Id.ToString(), Name = label.QuestionNumber!, Revision = 1, DataJson = AppJson.Write(new { sourceMarked = true, learnerWritingOnly = true, preparationNotes = label.PreparationNotes!.Trim(), datasetScope = label.DatasetScope!.Trim(), learnerGroup = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(user.SchoolId.ToString() + learnerReference))) }) });
        await AiTrainingExamplesRepository.InsertAsync(example); await UnitOfWorkManager.Current!.SaveChangesAsync(); return Results.Created($"/api/ai/batches/{id}/training", example);

    }
    [Microsoft.AspNetCore.Authorization.Authorize(ProsperaPermissions.Moderate)]
    [Volo.Abp.Uow.UnitOfWork(true)]
    public virtual async Task<OperationResult> VerifyExampleAsync(Guid id, Guid exampleId)
    {
        var p = Principal;
        var scriptQuery = await AiScriptsRepository.GetQueryableAsync();

        if (await Allowed(id, p, users, access) is not { } user) return Results.NotFound();
        var example = await AsyncExecuter.FirstOrDefaultAsync((await AiTrainingExamplesRepository.GetQueryableAsync()), e => e.Id == exampleId && scriptQuery.Any(s => s.Id == e.ScriptId && s.BatchId == id));
        if (example is null) return Results.NotFound();
        if (example.VerifiedBy is not null) return Results.Conflict(new { error = "This example is already verified." });
        if (example.LabelledBy == user.Id) return Results.Conflict(new { error = "A second academic reviewer must verify this training example." });
        example.VerifiedBy = user.Id; await AiTrainingExamplesRepository.UpdateAsync(example); await UnitOfWorkManager.Current!.SaveChangesAsync(); return Results.Ok(example);

    }
    [Microsoft.AspNetCore.Authorization.Authorize(ProsperaPermissions.ManageAcademics)]
    public virtual async Task<OperationResult> ExportVerifiedExamplesAsync(Guid id)
    {
        var p = Principal;
        var scriptQuery = await AiScriptsRepository.GetQueryableAsync();

        if (await Allowed(id, p, users, access) is not { } owner) return Results.NotFound();
        if (!await AsyncExecuter.AnyAsync((await InstitutionRecords.GetQueryableAsync()), r => r.SchoolId == owner.SchoolId && r.Kind == "trainingAuthorisation" && r.Code == id.ToString() && r.Active)) return Results.Json(new {error="An institution administrator must authorise training export under Learner data protection first."}, statusCode:403);
        var examples = await AsyncExecuter.ToListAsync((await AiTrainingExamplesRepository.GetQueryableAsync()).Where(e => e.VerifiedBy != null && scriptQuery.Any(s => s.Id == e.ScriptId && s.BatchId == id)));
        var batch = await AsyncExecuter.FirstAsync((await AiBatchesRepository.GetQueryableAsync()), b => b.Id == id);
        var parent = await AsyncExecuter.FirstAsync((await AssessmentsRepository.GetQueryableAsync()), a => a.Id == batch.AssessmentId);
        var hashRows = await AsyncExecuter.ToListAsync(scriptQuery.Where(s => s.BatchId == id).Select(s => new { s.Id, s.Hash }));
        var hashes = hashRows.ToDictionary(s => s.Id, s => s.Hash);
        var assessmentGroup = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Convert.ToHexString(SHA256.HashData(batch.Paper)) + Convert.ToHexString(SHA256.HashData(batch.Memo)))));

        var preparations = await AsyncExecuter.ToListAsync((await InstitutionRecords.GetQueryableAsync()).Where(r => r.SchoolId == parent.SchoolId && r.Kind == "trainingPreparation"));
        var lines = examples.Select(e => { var preparation = preparations.FirstOrDefault(r => r.Code == e.Id.ToString()); return AppJson.Write(new { schemaVersion = 2, batch.ScriptLanguage, batch.MemoLanguage, assessmentGroup, scriptGroup = hashes[e.ScriptId], school = parent.SchoolContext(), institution = parent.InstitutionContext(), preparation = preparation is null ? (JsonElement?)null : JsonSerializer.Deserialize<JsonElement>(preparation.DataJson), e.QuestionNumber, e.Question, e.Answer, e.Rubric, e.MaxMarks, e.Marks }); });
        return Results.File(Encoding.UTF8.GetBytes(string.Join('\n', lines)), "application/x-ndjson", "verified-training-examples.jsonl");

    }
    private async Task<StaffAccount?> Allowed(Guid id, ClaimsPrincipal p, IStaffIdentityService users, AcademicAccess access)
    {
        var user = await users.GetUserAsync(p); if (user is null) return null;
        var parentId = await AsyncExecuter.FirstOrDefaultAsync((await AiBatchesRepository.GetQueryableAsync()).Where(b => b.Id == id).Select(b => (Guid?)b.AssessmentId));
        var parent = await AsyncExecuter.FirstOrDefaultAsync((await AssessmentsRepository.GetQueryableAsync()), a => a.Id == parentId && a.SchoolId == user.SchoolId);
        return parent is not null && await access.CanAccess(user, p, parent) ? user : null;
    }

}
