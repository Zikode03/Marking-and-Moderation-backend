using System.Security.Claims;
using System.Text;
using System.Security.Cryptography;
using System.Text.Json;
namespace ProsperaAssess;

public class ScriptBatchAppService : ProsperaAppService, IScriptBatchAppService
{
    private const long MaxUpload = 60 * 1024 * 1024;
    [Microsoft.AspNetCore.Authorization.Authorize(ProsperaPermissions.Workspace)]
    public virtual async Task<OperationResult> GetModelStatusAsync(CancellationToken ct)
    {
        return Results.Ok(await marker.Health(ct));
    }
    [Microsoft.AspNetCore.Authorization.Authorize(ProsperaPermissions.Workspace)]
    public virtual async Task<OperationResult> GetListAsync()
    {
        var p = Principal;

        var user = await users.GetUserAsync(p); if (user is null) return Results.Unauthorized();
        var ids = (await access.Visible(user, p)).Select(a => a.Id).ToList();
        var batches = await AsyncExecuter.ToListAsync((await AiBatchesRepository.GetQueryableAsync()).Where(b => ids.Contains(b.AssessmentId)).OrderByDescending(b => b.CreatedAt)
            .Select(b => new { b.Id, b.AssessmentId, b.CreatedAt, b.PaperName, b.MemoName, b.ScriptLanguage, b.MemoLanguage }));
        var batchIds = batches.Select(b => b.Id).ToList();
        var scripts = await AsyncExecuter.ToListAsync((await AiScriptsRepository.GetQueryableAsync()).Where(s => batchIds.Contains(s.BatchId)).Select(s => new { s.Id, s.BatchId, s.Candidate, s.Status, s.Error }));
        return Results.Ok(batches.Select(b => new { b.Id, b.AssessmentId, b.CreatedAt, b.PaperName, b.MemoName, b.ScriptLanguage, b.MemoLanguage, Scripts = scripts.Where(s => s.BatchId == b.Id) }));

    }
    [Microsoft.AspNetCore.Authorization.Authorize(ProsperaPermissions.Workspace)]
    public virtual async Task<OperationResult> ExportApprovedResultsAsync(Guid id)
    {
        var p = Principal;

        if (await Batch(id, p, users, access) is null) return Results.NotFound();
        var rows = await AsyncExecuter.ToListAsync((await AiScriptsRepository.GetQueryableAsync()).Where(s => s.BatchId == id && s.Status == "Approved").Select(s => new { s.Candidate, s.ResultJson, s.Model }));
        await RecordRead(id, "results.exported");
        static string Cell(string value) => "\"" + ((value.Length > 0 && "=+-@".Contains(value[0]) ? "'" : "") + value).Replace("\"", "\"\"") + "\"";
        var lines = new List<string> { "Candidate,Total,Maximum,Model" };
        lines.AddRange(rows.Select(s => string.Join(',', Cell(s.Candidate), JsonSerializer.Deserialize<AiMarkingResult>(s.ResultJson!, AppJson.Options)!.Questions.Sum(q => q.Marks ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture), JsonSerializer.Deserialize<AiMarkingResult>(s.ResultJson!, AppJson.Options)!.Questions.Sum(q => q.MaxMarks).ToString(System.Globalization.CultureInfo.InvariantCulture), Cell(s.Model ?? ""))));
        return Results.File(System.Text.Encoding.UTF8.GetBytes(string.Join('\n', lines)), "text/csv", "approved-results.csv");

    }
    [Microsoft.AspNetCore.Authorization.Authorize(ProsperaPermissions.Workspace)]
    public virtual async Task<OperationResult> GetAsync(Guid id)
    {
        var p = Principal;

        if (await Batch(id, p, users, access) is not { } batch) return Results.NotFound();
        var scripts = await AsyncExecuter.ToListAsync((await AiScriptsRepository.GetQueryableAsync()).Where(s => s.BatchId == id).Select(s => new ScriptReadModel { Id = s.Id, BatchId = s.BatchId, Candidate = s.Candidate, FileName = s.FileName, Status = s.Status, UpdatedAt = s.UpdatedAt, ResultJson = s.ResultJson, Error = s.Error, Model = s.Model, ProviderResponseId = s.ProviderResponseId, Revision = s.Revision, AuditJson = s.AuditJson }));
        return Results.Ok(new { batch.Id, batch.AssessmentId, batch.PaperName, batch.MemoName, batch.ScriptLanguage, batch.MemoLanguage, Scripts = scripts.Select(s => s.View()) });

    }
    [Microsoft.AspNetCore.Authorization.Authorize(ProsperaPermissions.LabelTraining)]
    [Volo.Abp.Uow.UnitOfWork(true)]
    public virtual async Task<OperationResult> StartMarkingAsync(Guid id, CancellationToken ct)
    {
        var p = Principal;

        if (await Batch(id, p, users, access) is not { } batch) return Results.NotFound();
        var currentUser = await users.GetUserAsync(p); if (currentUser is null) return Results.Unauthorized();
        var setting = await AsyncExecuter.FirstOrDefaultAsync((await InstitutionRecords.GetQueryableAsync()), r => r.SchoolId == currentUser.SchoolId && r.Kind == "settings" && r.Active);
        if (setting is not null && (JsonSerializer.Deserialize<Dictionary<string,string>>(setting.DataJson, AppJson.Options) ?? []).GetValueOrDefault("requireMemoApproval") == "true" &&
            !await AsyncExecuter.AnyAsync((await InstitutionRecords.GetQueryableAsync()), r => r.SchoolId == currentUser.SchoolId && r.Kind == "memoApproval" && r.Code == id.ToString()))
            return Results.Conflict(new { error = "An independent academic reviewer must approve this memorandum version before marking." });
        var health = await marker.Health(ct);
        if (!health.Ready) return Results.Json(new { error = "The local marking model is not ready. Your uploaded files are saved; connect a trained model before starting." }, statusCode: 503);
        if (new[] { batch.ScriptLanguage, batch.MemoLanguage }.Any(code => MarkingLanguages.Codes.Contains(code) && health.Languages?.Contains(code) != true))
            return Results.Json(new { error = "This model version has not been validated for the selected script or memorandum language. Your files remain saved." }, statusCode: 503);
        var scripts = await AsyncExecuter.ToListAsync((await AiScriptsRepository.GetQueryableAsync()).Where(s => s.BatchId == id && (s.Status == "Uploaded" || s.Status == "Failed")));
        foreach (var script in scripts) { script.Queue(p.Identity?.Name ?? "staff"); await AiScriptsRepository.UpdateAsync(script); }
        await UnitOfWorkManager.Current!.SaveChangesAsync(ct); return Results.Ok(new { queued = scripts.Count });

    }
    [Microsoft.AspNetCore.Authorization.Authorize(ProsperaPermissions.Workspace)]
    public virtual async Task<OperationResult> DownloadFileAsync(Guid id, string file)
    {
        var p = Principal;

        if (await Batch(id, p, users, access) is not { } batch) return Results.NotFound();
        await RecordRead(id, "document.downloaded:" + (file is "paper" or "memo" ? file : "script"));
        if (file == "paper") return Results.File(await AsyncExecuter.FirstAsync((await AiBatchesRepository.GetQueryableAsync()).Where(b => b.Id == id).Select(b => b.Paper)), batch.PaperType, batch.PaperName);
        if (file == "memo") return Results.File(await AsyncExecuter.FirstAsync((await AiBatchesRepository.GetQueryableAsync()).Where(b => b.Id == id).Select(b => b.Memo)), batch.MemoType, batch.MemoName);
        if (!Guid.TryParse(file, out var scriptId)) return Results.NotFound();
        var script = await AsyncExecuter.FirstOrDefaultAsync((await AiScriptsRepository.GetQueryableAsync()), s => s.Id == scriptId && s.BatchId == id);
        return script is null ? Results.NotFound() : Results.File(script.Content, script.ContentType, script.FileName);

    }
    [Microsoft.AspNetCore.Authorization.Authorize(ProsperaPermissions.Mark)]
    [Volo.Abp.Uow.UnitOfWork(true)]
    public virtual async Task<OperationResult> ReviewAsync(Guid id, Guid scriptId, AiReview review)
    {
        var p = Principal;
        var user = await users.GetUserAsync(p); if (user is null) return Results.Unauthorized();
        if (await Batch(id, p, users, access) is null) return Results.NotFound();
        var script = await AsyncExecuter.FirstOrDefaultAsync((await AiScriptsRepository.GetQueryableAsync()), s => s.Id == scriptId && s.BatchId == id);
        if (script is null) return Results.NotFound();
        if (script.Revision != review.Revision) return Conflict();
        if (script.Status is not ("AwaitingModeration" or "NeedsAttention" or "ReturnedToTeacher") || script.Result() is not { } result) return Conflict();
        if (!InputRules.Text(review.Reason, 5000) || review.Marks is null || review.Marks.Count != result.Questions.Count)
            return Results.BadRequest(new { error = "The teacher must verify every AI mark and provide review notes." });
        foreach (var q in result.Questions)
            if (!review.Marks.TryGetValue(q.Number, out var mark) || mark < 0 || mark > q.MaxMarks)
                return Results.BadRequest(new { error = "Each confirmed mark must be within the approved memorandum limits." });
        if (review.Feedback is { } feedback && (feedback.Count != result.Questions.Count || result.Questions.Any(q => !feedback.TryGetValue(q.Number, out var text) || text is null || text.Length > 5000)))
            return Results.BadRequest(new { error = "Provide learner feedback for every question, limited to 5000 characters each." });
        if (review.Summary is not null && !InputRules.Text(review.Summary, 10000))
            return Results.BadRequest(new { error = "Overall learner feedback must be non-empty and at most 10000 characters." });
        await Assigned(script.Id, user.Id, "teacherId", user.SchoolId);
        if (review.Evidence is { } evidence && (evidence.Count != result.Questions.Count || result.Questions.Any(q => !evidence.TryGetValue(q.Number,out var text) || text is null || text.Length > 30000)))
            return Results.BadRequest(new { error = "Provide a learner-answer transcription for every question, limited to 30000 characters each." });
        script.ConfirmTeacherReview(user.Id, review.Marks, review.Reason!.Trim(), review.Feedback, review.Summary, review.Evidence);
        await AiScriptsRepository.UpdateAsync(script);
        await UnitOfWorkManager.Current!.SaveChangesAsync();
        return Results.Ok(script.View());
    }
    [Microsoft.AspNetCore.Authorization.Authorize(ProsperaPermissions.Moderate)]
    [Volo.Abp.Uow.UnitOfWork(true)]
    public virtual async Task<OperationResult> ReturnToTeacherAsync(Guid id, Guid scriptId, AiReview review)
    {
        var reviewer = await users.GetUserAsync(Principal); if (reviewer is null) return Results.Unauthorized();
        if (await Batch(id, Principal, users, access) is null) return Results.NotFound();
        var script = await AsyncExecuter.FirstOrDefaultAsync((await AiScriptsRepository.GetQueryableAsync()), s => s.Id == scriptId && s.BatchId == id);
        if (script is null) return Results.NotFound();
        if (script.Revision != review.Revision || script.Status != "TeacherReviewed") return Conflict();
        if (!InputRules.Text(review.Reason, 5000)) return Results.BadRequest(new { error = "Explain the feedback to the teacher." });
        await Assigned(script.Id, reviewer.Id, "moderatorId", reviewer.SchoolId);
        script.ReturnToTeacher(reviewer.Id, review.Reason!.Trim());
        await AiScriptsRepository.UpdateAsync(script);
        await UnitOfWorkManager.Current!.SaveChangesAsync();
        return Results.Ok(script.View());
    }
    [Microsoft.AspNetCore.Authorization.Authorize(ProsperaPermissions.Moderate)]
    [Volo.Abp.Uow.UnitOfWork(true)]
    public virtual async Task<OperationResult> ModerateAsync(Guid id, Guid scriptId, AiReview review)
    {
        var p = Principal;
        var moderator = await users.GetUserAsync(p); if (moderator is null) return Results.Unauthorized();

        if (await Batch(id, p, users, access) is null) return Results.NotFound();
        var script = await AsyncExecuter.FirstOrDefaultAsync((await AiScriptsRepository.GetQueryableAsync()), s => s.Id == scriptId && s.BatchId == id);
        if (script is null) return Results.NotFound();
        if (script.Revision != review.Revision) return Conflict();
        if (script.Status != "TeacherReviewed" || script.Result() is not { } result) return Conflict();
        if (!InputRules.Text(review.Reason, 5000) || review.Marks is null || review.Marks.Count != result.Questions.Count) return Results.BadRequest(new { error = "Confirm every question and give a moderation reason." });
        foreach (var q in result.Questions) if (!review.Marks.TryGetValue(q.Number, out var mark) || mark < 0 || mark > q.MaxMarks) return Results.BadRequest(new { error = "Marks must be within the memorandum question limits." });

        await Assigned(script.Id, moderator.Id, "moderatorId", moderator.SchoolId);
        script.ConfirmModeration(moderator.Id, review.Marks, review.Reason!.Trim());
        await AiScriptsRepository.UpdateAsync(script);
        await UnitOfWorkManager.Current!.SaveChangesAsync(); return Results.Ok(script.View());

    }
    [Microsoft.AspNetCore.Authorization.Authorize(ProsperaPermissions.Approve)]
    [Volo.Abp.Uow.UnitOfWork(true)]
    public virtual async Task<OperationResult> ApproveAsync(Guid id, Guid scriptId, AiReview review)
    {
        var p = Principal;

        if (await Batch(id, p, users, access) is null) return Results.NotFound();
        var script = await AsyncExecuter.FirstOrDefaultAsync((await AiScriptsRepository.GetQueryableAsync()), s => s.Id == scriptId && s.BatchId == id);
        if (script is null) return Results.NotFound();
        if (script.Revision != review.Revision || script.Status != "Moderated") return Conflict();
        script.Approve(p.Identity?.Name ?? "approver");
        await AiScriptsRepository.UpdateAsync(script);
        await UnitOfWorkManager.Current!.SaveChangesAsync(); return Results.Ok(script.View());

    }
    [Microsoft.AspNetCore.Authorization.Authorize(ProsperaPermissions.Upload)]
    public virtual async Task<OperationResult> UploadAsync(UploadBatchDto request, CancellationToken ct)
    {
        var p = Principal;

        var user = await users.GetUserAsync(p); if (user is null) return Results.Unauthorized();
        if (!p.IsInRole("Teacher") && !p.IsInRole("Lecturer") && !p.IsInRole("Professor") && !p.IsInRole("AcademicAdmin") && !p.IsInRole("SchoolAdmin")) return Results.Forbid();
        var context = request.Context; var form = new UploadCollection(request.Files);
        if (context is null || !MarkingLanguages.Valid(context.ScriptLanguage) || !MarkingLanguages.Valid(context.MemoLanguage) || !InputRules.Text(context.Title, 200) || !InputRules.Assessment(new(context.Title, "UPLOAD", [new("Imported", 1)], context.School, context.Institution))) return Results.BadRequest(new { error = "Valid batch title and academic context are required." });
        var assessment = new AssessmentRow(GuidGenerator.Create()) { SchoolId = user.SchoolId, Title = context.Title!.Trim(), Code = "UPLOAD-" + GuidGenerator.Create().ToString("N"), CriteriaJson = "[]", SchoolJson = context.School is null ? null : AppJson.Write(context.School) };
        if (context.Institution is { } input)
        {
            var module = await AsyncExecuter.FirstOrDefaultAsync((await ModulesRepository.GetQueryableAsync()), m => m.Id == input.ModuleId && m.SchoolId == user.SchoolId, ct);
            if (module is null) return Results.NotFound();
            var faculty = await AsyncExecuter.FirstAsync((await FacultiesRepository.GetQueryableAsync()), f => f.Id == module.FacultyId && f.SchoolId == user.SchoolId, ct);
            assessment.InstitutionJson = AppJson.Write(new InstitutionContext(faculty.Id, faculty.Name, module.Id, module.Code, module.Name, input.AcademicYear, input.Semester, input.AssessmentType, 0));
        }
        if (!await access.CanAccess(user, p, assessment)) return Results.Forbid();
        var paper = form.GetFile("paper"); var memo = form.GetFile("memo"); var scripts = form.GetFiles("scripts");
        if (paper is null || memo is null || scripts.Count is < 1 or > 30 || form.Files.Count != scripts.Count + 2) return Results.BadRequest(new { error = "Include one question paper, one approved memorandum, and 1–30 scripts. Each script file must contain one candidate's complete script." });
        if (form.Files.Sum(f => f.Length) > MaxUpload - 1024 * 1024) return Results.BadRequest(new { error = "The batch exceeds the 59 MB file limit." });
        var loaded = new List<(UploadFileDto File, byte[] Data, string Type)>();
        foreach (var file in form.Files)
        {
            if (file.Length is < 1 or > 10 * 1024 * 1024 || Path.GetFileName(file.FileName).Length > 200) return Results.BadRequest(new { error = "Each PDF, PNG or JPEG must be non-empty, under 10 MB, with a filename under 200 characters." });
            var data = file.Content;
            var type = Detect(data, file.FileName); if (type is null) return Results.BadRequest(new { error = "Unsupported or mismatched file. Use PDF, PNG or JPEG." });
            loaded.Add((file, data, type));
        }
        var scriptFiles = loaded.Where(f => f.File.Name == "scripts").ToList();
        var candidates = scriptFiles.Select(f => Path.GetFileNameWithoutExtension(f.File.FileName).Trim()).ToList();
        if (candidates.Any(c => string.IsNullOrWhiteSpace(c) || c.Length > 100) || candidates.Distinct(StringComparer.OrdinalIgnoreCase).Count() != candidates.Count || scriptFiles.Select(f => Convert.ToHexString(SHA256.HashData(f.Data))).Distinct().Count() != scriptFiles.Count) return Results.BadRequest(new { error = "Use a unique candidate/student number as each script filename. Duplicate files are not allowed." });
        var settings = await AsyncExecuter.FirstOrDefaultAsync((await InstitutionRecords.GetQueryableAsync()), r => r.SchoolId == user.SchoolId && r.Kind == "settings" && r.Active);
        var policy = settings is null ? new Dictionary<string, string>() : JsonSerializer.Deserialize<Dictionary<string, string>>(settings.DataJson, AppJson.Options) ?? [];
        if (policy.GetValueOrDefault("requireEnrolment") == "true")
        {
            var learners = await AsyncExecuter.ToListAsync((await InstitutionRecords.GetQueryableAsync()).Where(r => r.SchoolId == user.SchoolId && r.Kind == "learner" && r.Active));
            var enrolled = await AsyncExecuter.ToListAsync((await InstitutionRecords.GetQueryableAsync()).Where(r => r.SchoolId == user.SchoolId && r.Kind == "enrolment" && r.Active));
            var classes = await AsyncExecuter.ToListAsync((await InstitutionRecords.GetQueryableAsync()).Where(r => r.SchoolId == user.SchoolId && r.Kind == "class" && r.Active));
            bool MatchesContext(InstitutionRecord enrolment)
            {
                var values = JsonSerializer.Deserialize<Dictionary<string,string>>(enrolment.DataJson, AppJson.Options) ?? [];
                if (context.Institution is { } university) return values.GetValueOrDefault("moduleId") == university.ModuleId.ToString() && values.GetValueOrDefault("academicYear") == university.AcademicYear.ToString();
                var cohort = classes.FirstOrDefault(r => r.Id.ToString() == values.GetValueOrDefault("classId"));
                var cohortFields = cohort is null ? new Dictionary<string,string>() : JsonSerializer.Deserialize<Dictionary<string,string>>(cohort.DataJson, AppJson.Options) ?? [];
                return context.School is { } school && cohortFields.GetValueOrDefault("grade") == school.Grade.ToString() && values.GetValueOrDefault("academicYear") == (school.AcademicYear ?? DateTime.UtcNow.Year).ToString();
            }
            foreach (var candidate in candidates)
            {
                var learner = learners.FirstOrDefault(l => string.Equals(l.Code, candidate, StringComparison.OrdinalIgnoreCase));
                if (learner is null || !enrolled.Any(e => (JsonSerializer.Deserialize<Dictionary<string, string>>(e.DataJson, AppJson.Options) ?? []).GetValueOrDefault("learnerId") == learner.Id.ToString() && MatchesContext(e)))
                    return Results.BadRequest(new { error = "Every script filename must match an active candidate enrolled for this grade or module and academic year." });
            }
        }
        var paperFile = loaded.Single(f => f.File.Name == "paper"); var memoFile = loaded.Single(f => f.File.Name == "memo");
        var batch = new AiBatch(GuidGenerator.Create()) { AssessmentId = assessment.Id, CreatedAt = DateTimeOffset.UtcNow, UploadedBy = user.Id, PaperName = Path.GetFileName(paper.FileName), PaperType = paperFile.Type, Paper = paperFile.Data, MemoName = Path.GetFileName(memo.FileName), MemoType = memoFile.Type, Memo = memoFile.Data, ScriptLanguage = context.ScriptLanguage, MemoLanguage = context.MemoLanguage };
        if (context.PreviousBatchId is { } previousBatchId)
        {
            if (await Batch(previousBatchId, p, users, access) is null) return Results.NotFound();
            await InstitutionRecords.InsertAsync(new InstitutionRecord(GuidGenerator.Create()) { SchoolId = user.SchoolId, Kind = "documentVersion", Code = batch.Id.ToString(), Name = batch.MemoName, Revision = 1, DataJson = AppJson.Write(new Dictionary<string,string> { ["previousBatchId"] = previousBatchId.ToString() }) });
        }
        // Prevent accidental duplicate uploads within the same academic scope.
        var relatedIds = (await access.Visible(user, p)).Where(a => a.SchoolJson == assessment.SchoolJson && a.InstitutionJson == assessment.InstitutionJson).Select(a => a.Id).ToList();
        var relatedBatches = await AsyncExecuter.ToListAsync((await AiBatchesRepository.GetQueryableAsync()).Where(b => relatedIds.Contains(b.AssessmentId)).Select(b => b.Id));
        var hashes = scriptFiles.Select(f => Convert.ToHexString(SHA256.HashData(f.Data))).ToList();
        if (context.PreviousBatchId is null && await AsyncExecuter.AnyAsync((await AiScriptsRepository.GetQueryableAsync()), s => relatedBatches.Contains(s.BatchId) && hashes.Contains(s.Hash)))
            return Results.Conflict(new { error = "A script in this academic context was already uploaded. Use a document revision when intentionally replacing an assessment version." });
        await AssessmentsRepository.InsertAsync(assessment); await AiBatchesRepository.InsertAsync(batch);
        foreach (var (file, data, type) in scriptFiles)
        {
            var script = new AiScript(GuidGenerator.Create()) { BatchId = batch.Id, Candidate = Path.GetFileNameWithoutExtension(file.FileName).Trim(), FileName = Path.GetFileName(file.FileName), ContentType = type, Content = data, Hash = Convert.ToHexString(SHA256.HashData(data)) };
            script.Audit(user.Email ?? user.Id, "Uploaded", "Original script stored"); await AiScriptsRepository.InsertAsync(script);
        }
        await UnitOfWorkManager.Current!.SaveChangesAsync(ct);
        return Results.Created($"/api/ai/batches/{batch.Id}", new { batch.Id, batch.AssessmentId });

    }
    private async Task RecordRead(Guid batchId, string action)
    {
        var user = await users.GetUserAsync(Principal); if (user is null) throw new PortalBusinessException(401,null);
        await InstitutionEvents.InsertAsync(new InstitutionEvent(GuidGenerator.Create()) { SchoolId = user.SchoolId, RecordId = batchId, Actor = user.Id, Action = action, At = DateTimeOffset.UtcNow });
    }
    private async Task Assigned(Guid scriptId, string actor, string role, Guid schoolId)
    {
        var assignment = await AsyncExecuter.FirstOrDefaultAsync((await InstitutionRecords.GetQueryableAsync()), r => r.SchoolId == schoolId && r.Kind == "assignment" && r.Code == scriptId.ToString() && r.Active);
        if (assignment is not null && (JsonSerializer.Deserialize<Dictionary<string, string>>(assignment.DataJson, AppJson.Options) ?? []).GetValueOrDefault(role) != actor)
            throw new PortalBusinessException(403, new { error = "This script is assigned to another reviewer." });
    }
    private static OperationResult Conflict() => Results.Conflict(new { error = "The script changed or is not ready for this action. Refresh and try again." });
    private async Task<AiBatch?> Batch(Guid id, ClaimsPrincipal p, IStaffIdentityService users, AcademicAccess access)
    {
        var user = await users.GetUserAsync(p); if (user is null) return null;
        var batch = await AsyncExecuter.FirstOrDefaultAsync((await AiBatchesRepository.GetQueryableAsync()).Where(b => b.Id == id).Select(b => new AiBatch(b.Id) { AssessmentId = b.AssessmentId, PaperName = b.PaperName, MemoName = b.MemoName, PaperType = b.PaperType, MemoType = b.MemoType, ScriptLanguage = b.ScriptLanguage, MemoLanguage = b.MemoLanguage }));
        if (batch is null) return null;
        var assessment = await AsyncExecuter.FirstOrDefaultAsync((await AssessmentsRepository.GetQueryableAsync()), a => a.Id == batch.AssessmentId && a.SchoolId == user.SchoolId);
        return assessment is not null && await access.CanAccess(user, p, assessment) ? batch : null;
    }

    public static string? Detect(byte[] data, string name)
    {
        var extension = Path.GetExtension(name).ToLowerInvariant();
        if (extension == ".pdf" && data.AsSpan().StartsWith("%PDF-"u8)) return "application/pdf";
        if (extension == ".png" && data.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return "image/png";
        if (extension is ".jpg" or ".jpeg" && data.Length > 3 && data[0] == 255 && data[1] == 216 && data[2] == 255) return "image/jpeg";
        return null;
    }

}
