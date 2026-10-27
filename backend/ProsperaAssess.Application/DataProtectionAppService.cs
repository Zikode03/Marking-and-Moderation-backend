using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Uow;

namespace ProsperaAssess;

[Authorize(ProsperaPermissions.ManageAcademics)]
public class DataProtectionAppService(IStorageControl storage) : ProsperaAppService, IDataProtectionAppService
{
    private async Task<StaffAccount> Current()
    {
        var user = await users.GetUserAsync(Principal) ?? throw new PortalBusinessException(401, null);
        if (!AcademicAccess.Manager(Principal)) throw new PortalBusinessException(403, null);
        return user;
    }
    private async Task<int?> Days(Guid school)
    {
        var settings = await AsyncExecuter.FirstOrDefaultAsync((await InstitutionRecords.GetQueryableAsync()), r => r.SchoolId == school && r.Kind == "settings" && r.Active);
        var values = settings is null ? new Dictionary<string,string>() : JsonSerializer.Deserialize<Dictionary<string,string>>(settings.DataJson, AppJson.Options) ?? [];
        return int.TryParse(values.GetValueOrDefault("retentionDays"), out var days) && days is >= 30 and <= 3650 ? days : null;
    }
    private async Task<AiBatch?> Batch(Guid id, Guid school)
    {
        var assessments = (await AssessmentsRepository.GetQueryableAsync()).Where(a => a.SchoolId == school).Select(a => a.Id);
        return await AsyncExecuter.FirstOrDefaultAsync((await AiBatchesRepository.GetQueryableAsync()).Where(b => b.Id == id && assessments.Contains(b.AssessmentId)));
    }
    private async Task<(bool Eligible, string Token, string Reason, DateTimeOffset LastActivity)> Preview(AiBatch batch, Guid school, int? days)
    {
        var scripts = await AsyncExecuter.ToListAsync((await AiScriptsRepository.GetQueryableAsync()).Where(s => s.BatchId == batch.Id).Select(s => new {s.Id, s.Revision, s.Status, s.UpdatedAt}));
        var scriptIds = scripts.Select(s=>s.Id).ToList();
        var labels = await AsyncExecuter.ToListAsync((await AiTrainingExamplesRepository.GetQueryableAsync()).Where(e=>scriptIds.Contains(e.ScriptId)).Select(e=>new {e.Id,e.CreatedAt,e.VerifiedBy}));
        var decisions = await AsyncExecuter.ToListAsync((await InstitutionRecords.GetQueryableAsync()).Where(r=>r.SchoolId==school && r.Code==batch.Id.ToString() && (r.Kind=="retentionHold" || r.Kind=="trainingAuthorisation")).Select(r=>new {r.Id,r.Revision,r.Active}));
        var last = scripts.Select(s => s.UpdatedAt).Concat(labels.Select(e=>e.CreatedAt)).Append(batch.CreatedAt).Max();
        var held = await AsyncExecuter.AnyAsync((await InstitutionRecords.GetQueryableAsync()), r => r.SchoolId == school && r.Kind == "retentionHold" && r.Code == batch.Id.ToString() && r.Active);
        var reason = days is null ? "Configure an institutional retention period first." : held ? "Protected by a retention hold." : scripts.Any(s => s.Status != "Approved") ? "Only fully approved batches can expire; resolve active or abandoned work separately." : last > DateTimeOffset.UtcNow.AddDays(-days.Value) ? "Still within the retention period." : "Retention period elapsed.";
        var eligible = days is not null && !held && scripts.Count > 0 && scripts.All(s => s.Status == "Approved") && last <= DateTimeOffset.UtcNow.AddDays(-days.Value);
        var token = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(AppJson.Write(new {batch.Id, days, held, last, labels=labels.OrderBy(e=>e.Id), decisions=decisions.OrderBy(r=>r.Id), scripts = scripts.OrderBy(s => s.Id)}))));
        return (eligible, token, reason, last);
    }
    public virtual async Task<OperationResult> GetAsync()
    {
        var user = await Current(); var days = await Days(user.SchoolId);
        var assessments = (await AssessmentsRepository.GetQueryableAsync()).Where(a => a.SchoolId == user.SchoolId).Select(a => a.Id);
        var batches = await AsyncExecuter.ToListAsync((await AiBatchesRepository.GetQueryableAsync()).Where(b => assessments.Contains(b.AssessmentId)).OrderBy(b => b.CreatedAt).Take(200).Select(b => new AiBatch(b.Id) {CreatedAt=b.CreatedAt}));
        var rows = new List<object>();
        foreach (var batch in batches)
        {
            var preview = await Preview(batch, user.SchoolId, days);
            var decisions = await AsyncExecuter.ToListAsync((await InstitutionRecords.GetQueryableAsync()).Where(r => r.SchoolId == user.SchoolId && r.Code == batch.Id.ToString() && (r.Kind == "retentionHold" || r.Kind == "trainingAuthorisation")));
            rows.Add(new {batch.Id, batch.CreatedAt, preview.LastActivity, preview.Eligible, preview.Token, preview.Reason, held = decisions.Any(r => r.Kind == "retentionHold" && r.Active), trainingAuthorised = decisions.Any(r => r.Kind == "trainingAuthorisation" && r.Active)});
        }
        return Results.Ok(new {retentionDays=days, usedBytes=await storage.UsedBytesAsync(user.SchoolId), quotaBytes=storage.QuotaBytes, batches=rows, limit=200});
    }
    [UnitOfWork(true)]
    public virtual Task<OperationResult> HoldAsync(Guid id, PrivacyDecisionInput input) => Decision(id, "retentionHold", input);
    [UnitOfWork(true)]
    public virtual Task<OperationResult> TrainingAsync(Guid id, PrivacyDecisionInput input) => Decision(id, "trainingAuthorisation", input);
    private async Task<OperationResult> Decision(Guid id, string kind, PrivacyDecisionInput input)
    {
        var user = await Current();
        if (!InputRules.Text(input.Reason, 1000)) return Results.BadRequest(new {error="Record the decision reference or reason, without learner names or answers."});
        await storage.LockAsync(user.SchoolId);
        if (await Batch(id, user.SchoolId) is null) return Results.NotFound();
        var row = await AsyncExecuter.FirstOrDefaultAsync((await InstitutionRecords.GetQueryableAsync()), r => r.SchoolId == user.SchoolId && r.Kind == kind && r.Code == id.ToString());
        if (row is null) { row = new InstitutionRecord(GuidGenerator.Create()) {SchoolId=user.SchoolId, Kind=kind, Code=id.ToString(), Name=kind}; await InstitutionRecords.InsertAsync(row); }
        row.Active=input.Enabled; row.Revision++; row.DataJson=AppJson.Write(new {reason=input.Reason.Trim(), actor=user.Id, at=DateTimeOffset.UtcNow});
        await InstitutionEvents.InsertAsync(new InstitutionEvent(GuidGenerator.Create()) {SchoolId=user.SchoolId, RecordId=id, Actor=user.Id, Action=kind+(input.Enabled?".enabled":".disabled"), At=DateTimeOffset.UtcNow});
        await UnitOfWorkManager.Current!.SaveChangesAsync();
        return Results.Ok(new {message="Decision recorded."});
    }
    [UnitOfWork(true)]
    public virtual async Task<OperationResult> PurgeAsync(Guid id, RetentionDeleteInput input)
    {
        var user = await Current();
        if (input.Confirmation != "DELETE EXPIRED BATCH") return Results.BadRequest(new {error="Enter DELETE EXPIRED BATCH to confirm permanent deletion."});
        await storage.LockAsync(user.SchoolId);
        var batch = await Batch(id, user.SchoolId); if (batch is null) return Results.NotFound();
        var preview = await Preview(batch, user.SchoolId, await Days(user.SchoolId));
        if (!preview.Eligible || preview.Token != input.PreviewToken) return Results.Conflict(new {error="The batch or policy changed, or it is not eligible. Refresh the retention preview."});
        var scripts = await AsyncExecuter.ToListAsync((await AiScriptsRepository.GetQueryableAsync()).Where(s => s.BatchId == id));
        var scriptIds = scripts.Select(s => s.Id).ToList();
        var examples = await AsyncExecuter.ToListAsync((await AiTrainingExamplesRepository.GetQueryableAsync()).Where(e => scriptIds.Contains(e.ScriptId)));
        var related = scriptIds.Concat(examples.Select(e => e.Id)).Append(id).Select(x => x.ToString()).ToList();
        var records = await AsyncExecuter.ToListAsync((await InstitutionRecords.GetQueryableAsync()).Where(r => r.SchoolId == user.SchoolId && related.Contains(r.Code)));
        foreach (var record in records) await InstitutionRecords.DeleteAsync(record);
        foreach (var example in examples) await AiTrainingExamplesRepository.DeleteAsync(example);
        foreach (var script in scripts) await AiScriptsRepository.DeleteAsync(script);
        await AiBatchesRepository.DeleteAsync(batch);
        await InstitutionEvents.InsertAsync(new InstitutionEvent(GuidGenerator.Create()) {SchoolId=user.SchoolId, RecordId=id, Actor=user.Id, Action="retention.batchPurged", At=DateTimeOffset.UtcNow});
        await UnitOfWorkManager.Current!.SaveChangesAsync();
        return Results.Ok(new {message="Expired documents, script results, review history and linked training examples were permanently deleted from active storage. Backups expire under the backup policy."});
    }
}
