using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Uow;
namespace ProsperaAssess;

[Authorize(ProsperaPermissions.Workspace)]
public class InstitutionAppService : ProsperaAppService, IInstitutionAppService
{
    private static readonly Dictionary<string, string[]> Schema = new()
    {
        ["institution"] = ["type", "sector", "pathway", "brandColor", "contactEmail"],
        ["branch"] = ["address", "contactEmail"],
        ["subject"] = ["curriculum", "language"],
        ["qualification"] = ["facultyId", "studyLevels"],
        ["module"] = ["moduleId", "qualificationId", "studyLevel", "semester"],
        ["class"] = ["branchId", "grade", "studyLevel", "qualificationId", "academicYear"],
        ["learner"] = ["branchId", "email"],
        ["enrolment"] = ["learnerId", "classId", "moduleId", "academicYear", "semester"],
        ["session"] = ["academicYear", "term", "semester", "startsOn", "endsOn"],
        ["assessment"] = ["assessmentId", "sessionId", "classId"],
        ["settings"] = ["language", "retentionDays", "requireEnrolment", "requireMemoApproval", "markingPolicy", "moderationSamplePercent"],
        ["integration"] = ["provider", "contactEmail", "notes"],
    };
    private async Task<StaffAccount> Current() => await users.GetUserAsync(Principal) ?? throw new PortalBusinessException(401, null);
    private static Dictionary<string, string> Fields(InstitutionRecord row) => JsonSerializer.Deserialize<Dictionary<string, string>>(row.DataJson, AppJson.Options) ?? new();
    private static object View(InstitutionRecord row) => new { row.Id, row.Kind, row.Code, row.Name, fields = Fields(row), row.Active, row.Revision };
    private async Task Log(StaffAccount user, Guid id, string action, string before, string after)
    {
        await InstitutionEvents.InsertAsync(new InstitutionEvent(GuidGenerator.Create()) { SchoolId = user.SchoolId, RecordId = id, Actor = user.Id, Action = action, BeforeJson = before, AfterJson = after, At = DateTimeOffset.UtcNow });
    }
    public virtual async Task<OperationResult> GetAsync(string kind)
    {
        var user = await Current();
        if (!Schema.ContainsKey(kind) && kind != "assignment") return Results.NotFound();
        if ((kind is "learner" or "enrolment" or "assignment") && !AcademicAccess.Leader(Principal)) return Results.Forbid();
        var rows = await AsyncExecuter.ToListAsync((await InstitutionRecords.GetQueryableAsync()).Where(r => r.SchoolId == user.SchoolId && r.Kind == kind).OrderBy(r => r.Code));
        return Results.Ok(rows.Select(View));
    }
    [Authorize(ProsperaPermissions.ManageAcademics)]
    [UnitOfWork(true)]
    public virtual async Task<OperationResult> SaveAsync(string kind, Guid? id, ManagedRecordInput input)
    {
        var user = await Current();
        if (!AcademicAccess.Manager(Principal)) return Results.Forbid();
        if (!Schema.TryGetValue(kind, out var allowed)) return Results.NotFound();
        if (!InputRules.Text(input.Code, 100) || !InputRules.Text(input.Name, 200) || input.Fields is null || input.Fields.Count > allowed.Length || input.Fields.Any(p => !allowed.Contains(p.Key) || p.Value is null || p.Value.Length > 5000)) return Invalid("Provide valid code, name and module fields.");
        if (kind == "settings") await LazyServiceProvider.LazyGetRequiredService<IStorageControl>().LockAsync(user.SchoolId);
        var data = input.Fields.ToDictionary(p => p.Key, p => p.Value.Trim());
        string F(string key) => data.GetValueOrDefault(key, "");
        bool Number(string key, int min, int max, bool optional = false) => optional && F(key) == "" || int.TryParse(F(key), out var n) && n >= min && n <= max;
        var code = kind is "institution" or "settings" ? "CURRENT" : input.Code!.Trim().ToUpperInvariant();
        var row = id is null ? null : await AsyncExecuter.FirstOrDefaultAsync((await InstitutionRecords.GetQueryableAsync()), r => r.Id == id && r.SchoolId == user.SchoolId && r.Kind == kind);
        if (id is not null && row is null) return Results.NotFound();
        if (row is not null && kind == "learner" && row.Code != code) return Invalid("Candidate references cannot change after registration; archive and register a corrected reference.");
        if (row is not null && row.Revision != input.Revision) return Results.Conflict(new { error = "This record changed. Reload before saving." });
        if (await AsyncExecuter.AnyAsync((await InstitutionRecords.GetQueryableAsync()), r => r.SchoolId == user.SchoolId && r.Kind == kind && r.Code == code && r.Id != id)) return Results.Conflict(new { error = "This code already exists, including archived records." });
        if (F("contactEmail") != "" && !InputRules.Email(F("contactEmail")) || F("email") != "" && !InputRules.Email(F("email"))) return Invalid("Enter a valid email address.");
        if (kind == "institution" && (F("type") is not ("school" or "college" or "university") || F("sector") is not ("public" or "private") || !System.Text.RegularExpressions.Regex.IsMatch(F("brandColor"), "^#[0-9a-fA-F]{6}$"))) return Invalid("Choose institution type, sector and a six-digit brand colour.");
        if (kind == "subject" && (F("curriculum") == "" || F("language") == "")) return Invalid("Curriculum and language are required.");
        if (kind == "qualification" && !Number("studyLevels", 1, 12)) return Invalid("Provide between 1 and 12 study levels.");
        if (kind is "class" or "enrolment" or "session" && !Number("academicYear", 2000, 2200)) return Invalid("Provide a valid academic year.");
        if (kind == "class" && ((!Number("grade", 8, 12, true) || !Number("studyLevel", 1, 12, true)) || (F("grade") == "") == (F("studyLevel") == ""))) return Invalid("Choose either Grade 8-12 or a university study level.");
        if (kind == "enrolment" && (F("classId") == "" && F("moduleId") == "" || !Number("semester", 1, 4, true))) return Invalid("Choose a class or module and a valid semester.");
        if (kind == "session" && (!DateOnly.TryParse(F("startsOn"), out var start) || !DateOnly.TryParse(F("endsOn"), out var end) || end < start || !Number("term", 1, 4, true) || !Number("semester", 1, 4, true))) return Invalid("Provide valid session dates, term and semester.");
        if (kind == "settings" && (F("requireMemoApproval") != "" && F("requireMemoApproval") is not ("true" or "false") || !Number("retentionDays", 30, 3650) || !Number("moderationSamplePercent", 1, 100) || F("requireEnrolment") is not ("true" or "false") || F("language") == "" || F("markingPolicy") == "")) return Invalid("Provide language, marking policy, retention of 30-3650 days and a sampling percentage of 1-100.");
        foreach (var (key, targetKind) in new[] { ("branchId", "branch"), ("qualificationId", "qualification"), ("learnerId", "learner"), ("classId", "class"), ("sessionId", "session") })
        {
            var required = key == "qualificationId" && kind == "module" || key == "learnerId" && kind == "enrolment" || key == "sessionId" && kind == "assessment" || key == "qualificationId" && kind == "class" && F("studyLevel") != "";
            if (F(key) == "") { if (required) return Invalid("Select " + targetKind + "."); continue; }
            if (!Guid.TryParse(F(key), out var refId) || !await AsyncExecuter.AnyAsync((await InstitutionRecords.GetQueryableAsync()), r => r.SchoolId == user.SchoolId && r.Id == refId && r.Kind == targetKind && r.Active)) return Invalid("Select an active " + targetKind + " from this institution.");
        }
        if (F("facultyId") != "" && (!Guid.TryParse(F("facultyId"), out var facultyId) || !await AsyncExecuter.AnyAsync((await FacultiesRepository.GetQueryableAsync()), r => r.Id == facultyId && r.SchoolId == user.SchoolId))) return Invalid("Select a faculty from this institution.");
        if (F("moduleId") != "" && (!Guid.TryParse(F("moduleId"), out var moduleId) || !await AsyncExecuter.AnyAsync((await ModulesRepository.GetQueryableAsync()), r => r.Id == moduleId && r.SchoolId == user.SchoolId))) return Invalid("Select a module from this institution.");
        if (kind == "assessment" && (!Guid.TryParse(F("assessmentId"), out var assessmentId) || !await AsyncExecuter.AnyAsync((await AssessmentsRepository.GetQueryableAsync()), r => r.Id == assessmentId && r.SchoolId == user.SchoolId))) return Invalid("Select an assessment from this institution.");
        if (kind == "assessment")
        {
            var registrations = await AsyncExecuter.ToListAsync((await InstitutionRecords.GetQueryableAsync()).Where(r => r.SchoolId == user.SchoolId && r.Kind == "assessment" && r.Id != id && r.Active));
            if (registrations.Any(r => Fields(r).GetValueOrDefault("assessmentId") == F("assessmentId"))) return Invalid("This assessment is already registered to a session. Edit its existing registration.");
        }
        if (kind == "module" && (F("moduleId") == "" || !Number("studyLevel", 1, 12) || !Number("semester", 1, 4))) return Invalid("Provide a module, study level and semester.");
        if ((kind is "class" or "module") && F("qualificationId") != "")
        {
            var qualificationId = Guid.Parse(F("qualificationId"));
            var qualification = await AsyncExecuter.FirstAsync((await InstitutionRecords.GetQueryableAsync()), r => r.Id == qualificationId && r.SchoolId == user.SchoolId);
            if (!int.TryParse(Fields(qualification).GetValueOrDefault("studyLevels"), out var levels) || !int.TryParse(F("studyLevel"), out var level) || level > levels) return Invalid("The study level must belong to the selected qualification.");
        }
        if (kind == "enrolment" && F("classId") != "")
        {
            var classId = Guid.Parse(F("classId"));
            var cohort = await AsyncExecuter.FirstAsync((await InstitutionRecords.GetQueryableAsync()), r => r.Id == classId && r.SchoolId == user.SchoolId);
            if (Fields(cohort).GetValueOrDefault("academicYear") != F("academicYear")) return Invalid("The enrolment year must match the class year.");
        }
        if (kind == "enrolment")
        {
            var linked = await AsyncExecuter.ToListAsync((await InstitutionRecords.GetQueryableAsync()).Where(r => r.SchoolId == user.SchoolId && r.Kind == "enrolment" && r.Id != id && r.Active));
            if (linked.Any(r => { var v = Fields(r); return new[] { "learnerId", "classId", "moduleId", "academicYear", "semester" }.All(k => v.GetValueOrDefault(k, "") == F(k)); })) return Invalid("This enrolment already exists.");
        }
        if (row is not null && !input.Active && row.Active)
        {
            var references = await AsyncExecuter.ToListAsync((await InstitutionRecords.GetQueryableAsync()).Where(r => r.SchoolId == user.SchoolId && r.Id != id && r.Active));
            if (references.Any(r => Fields(r).Values.Contains(row.Id.ToString()))) return Invalid("Archive dependent enrolments or records first.");
        }
        var before = row is null ? "" : AppJson.Write(View(row));
        var created = row is null;
        row ??= new InstitutionRecord(GuidGenerator.Create()) { SchoolId = user.SchoolId, Kind = kind };
        row.Code = code; row.Name = input.Name!.Trim(); row.DataJson = AppJson.Write(data); row.Active = input.Active; row.Revision++;
        if (created) await InstitutionRecords.InsertAsync(row); else await InstitutionRecords.UpdateAsync(row);
        await Log(user, row.Id, kind + (created ? ".created" : ".updated"), before, AppJson.Write(View(row)));
        await UnitOfWorkManager.Current!.SaveChangesAsync();
        return Results.Ok(View(row));
    }
    [Authorize(ProsperaPermissions.ManageAcademics)]
    [UnitOfWork(true)]
    public virtual async Task<OperationResult> ImportAsync(string kind, List<ManagedRecordInput> records)
    {
        if (kind is "settings" or "institution" || records is null || records.Count is < 1 or > 500) return Invalid("Import 1-500 records; institution and settings profiles are edited individually.");
        foreach (var record in records) await SaveAsync(kind, null, record);
        return Results.Ok(new { imported = records.Count });
    }
    [Authorize(ProsperaPermissions.Moderate)]
    [UnitOfWork(true)]
    public virtual async Task<OperationResult> SampleAsync(int percentage)
    {
        var user = await Current(); if (percentage is < 1 or > 100) return Invalid("Choose a percentage from 1 to 100.");
        var parents = (await access.Visible(user, Principal)).Select(a => a.Id).ToList();
        var batches = await AsyncExecuter.ToListAsync((await AiBatchesRepository.GetQueryableAsync()).Where(b => parents.Contains(b.AssessmentId)).Select(b => b.Id));
        var eligible = await AsyncExecuter.ToListAsync((await AiScriptsRepository.GetQueryableAsync()).Where(s => batches.Contains(s.BatchId) && s.Status != "Approved" && s.Status != "Moderated").Select(s => s.Id));
        var sample = eligible.OrderBy(id => id.ToString(), StringComparer.Ordinal).Take((int)Math.Ceiling(eligible.Count * percentage / 100m)).ToList();
        await Log(user, user.SchoolId, "moderation.sample.recorded", "", AppJson.Write(new { percentage, population = eligible.Count, scriptIds = sample }));
        await UnitOfWorkManager.Current!.SaveChangesAsync(); return Results.Ok(new { percentage, population = eligible.Count, scriptIds = sample });
    }
    private static OperationResult Invalid(string message) => Results.BadRequest(new { error = message });

    [Authorize(ProsperaPermissions.ManageAcademics)]
    public virtual async Task<OperationResult> AuditAsync(string? search)
    {
        var user = await Current();
        var query = (await InstitutionEvents.GetQueryableAsync()).Where(e => e.SchoolId == user.SchoolId);
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim(); query = query.Where(e => e.Action.Contains(term) || e.Actor.Contains(term)); }
        var events = await AsyncExecuter.ToListAsync(query.OrderByDescending(e => e.At).Take(500));
        var ids = (await access.Visible(user, Principal)).Select(a => a.Id).ToList();
        var batches = await AsyncExecuter.ToListAsync((await AiBatchesRepository.GetQueryableAsync()).Where(b => ids.Contains(b.AssessmentId)).Select(b => b.Id));
        var scripts = await AsyncExecuter.ToListAsync((await AiScriptsRepository.GetQueryableAsync()).Where(s => batches.Contains(s.BatchId)).Select(s => new { s.Id, s.Candidate, s.AuditJson }));
        var workflow = scripts.SelectMany(s => (JsonSerializer.Deserialize<List<AiHistory>>(s.AuditJson, AppJson.Options) ?? []).Select(h => new { recordId = s.Id, s.Candidate, h.Actor, h.Action, h.At, h.Reason })).Where(h => string.IsNullOrWhiteSpace(search) || (h.Action + h.Actor + h.Candidate).Contains(search, StringComparison.OrdinalIgnoreCase)).OrderByDescending(h => h.At).Take(500);
        return Results.Ok(new { events, workflow });
    }
    public virtual async Task<OperationResult> TasksAsync()
    {
        var user = await Current(); var parentIds = (await access.Visible(user, Principal)).Select(a => a.Id).ToList();
        var batches = await AsyncExecuter.ToListAsync((await AiBatchesRepository.GetQueryableAsync()).Where(b => parentIds.Contains(b.AssessmentId)).Select(b => b.Id));
        var scripts = await AsyncExecuter.ToListAsync((await AiScriptsRepository.GetQueryableAsync()).Where(s => batches.Contains(s.BatchId) && s.Status != "Approved").Select(s => new { s.Id, s.BatchId, s.Candidate, s.Status, s.UpdatedAt }));
        var assignments = await AsyncExecuter.ToListAsync((await InstitutionRecords.GetQueryableAsync()).Where(r => r.SchoolId == user.SchoolId && r.Kind == "assignment" && r.Active));
        return Results.Ok(scripts.Select(s => { var assignment = assignments.FirstOrDefault(a => a.Code == s.Id.ToString()); var f = assignment is null ? new Dictionary<string, string>() : Fields(assignment); var review = s.Status is "AwaitingModeration" or "NeedsAttention" or "ReturnedToTeacher"; var owner = f.GetValueOrDefault(review ? "teacherId" : "moderatorId"); return new { scriptId = s.Id, s.BatchId, s.Candidate, s.Status, s.UpdatedAt, ownerId = owner, dueAt = f.GetValueOrDefault("dueAt"), stage = review ? "review" : s.Status == "TeacherReviewed" ? "moderation" : s.Status == "Moderated" ? "approval" : "marking" }; }).Where(t => AcademicAccess.Manager(Principal) || t.ownerId == user.Id || t.ownerId is null && (t.stage == "review" && Principal.IsInRole("Teacher") || t.stage == "review" && (Principal.IsInRole("Lecturer") || Principal.IsInRole("Professor")) || t.stage == "moderation" && (Principal.IsInRole("HOD") || Principal.IsInRole("Moderator")) || t.stage == "approval" && Principal.IsInRole("Principal"))));
    }
    [UnitOfWork(true)]
    public virtual async Task<OperationResult> ReportAsync(string? candidate)
    {
        var user = await Current(); var parents = await access.Visible(user, Principal); var parentIds = parents.Select(a => a.Id).ToList();
        var batches = await AsyncExecuter.ToListAsync((await AiBatchesRepository.GetQueryableAsync()).Where(b => parentIds.Contains(b.AssessmentId)));
        var batchIds = batches.Select(b => b.Id).ToList();
        var scripts = await AsyncExecuter.ToListAsync((await AiScriptsRepository.GetQueryableAsync()).Where(s => batchIds.Contains(s.BatchId) && s.Status == "Approved" && (candidate == null || s.Candidate == candidate)));
        await Log(user, user.SchoolId, "results.viewed", "", candidate ?? "approved results");
        var contextRecords = await AsyncExecuter.ToListAsync((await InstitutionRecords.GetQueryableAsync()).Where(r => r.SchoolId == user.SchoolId && (r.Kind == "assessment" || r.Kind == "session" || r.Kind == "class")));
        return Results.Ok(scripts.Select(s => { var b = batches.First(b => b.Id == s.BatchId); var a = parents.First(a => a.Id == b.AssessmentId); var result = s.Result()!; var registration = contextRecords.FirstOrDefault(r => r.Kind == "assessment" && Fields(r).GetValueOrDefault("assessmentId") == a.Id.ToString()); var registrationFields = registration is null ? new Dictionary<string, string>() : Fields(registration); var session = contextRecords.FirstOrDefault(r => r.Id.ToString() == registrationFields.GetValueOrDefault("sessionId")); var cohort = contextRecords.FirstOrDefault(r => r.Id.ToString() == registrationFields.GetValueOrDefault("classId")); return new { session = session?.Name, className = cohort?.Name, academicYear = session is null ? null : Fields(session).GetValueOrDefault("academicYear"), s.Id, s.Candidate, assessmentId = a.Id, assessment = a.Title, school = a.SchoolContext(), institution = a.InstitutionContext(), approvedAt = s.UpdatedAt, total = result.Questions.Sum(q => q.Marks ?? 0), maximum = result.Questions.Sum(q => q.MaxMarks), feedback = result.Summary, questions = result.Questions, s.Model }; }));
    }
    [Authorize(ProsperaPermissions.Workspace)]
    public virtual async Task<OperationResult> StaffAsync()
    {
        var user = await Current(); if (!AcademicAccess.Leader(Principal)) return Results.Forbid(); var rows = new List<object>();
        foreach (var member in await users.GetUsersAsync(user.SchoolId)) rows.Add(new { member.Id, member.Email, member.Active, roles = await users.GetRolesAsync(member) });
        return Results.Ok(rows);
    }
    [Authorize(ProsperaPermissions.CreateStaff)]
    [UnitOfWork(true)]
    public virtual async Task<OperationResult> SetStaffAsync(string id, StaffStatusInput input)
    {
        var admin = await Current(); if (!Principal.IsInRole("SchoolAdmin")) return Results.Forbid();
        var member = await users.FindByIdAsync(id); if (member is null || member.SchoolId != admin.SchoolId) return Results.NotFound();
        if (id == admin.Id) return Invalid("Another administrator must change your access.");
        var roles = new[] { "Teacher", "Lecturer", "Professor", "HOD", "Moderator", "Principal", "SchoolAdmin", "AcademicAdmin" };
        if (input.Roles is null || input.Roles.Length == 0 || input.Roles.Any(r => !roles.Contains(r))) return Invalid("Select recognised staff roles.");
        var before = AppJson.Write(new { member.Active, roles = await users.GetRolesAsync(member) });
        var result = await users.SetStatusAsync(member, input.Active, input.Roles.Distinct().ToArray());
        if (!result.Succeeded) return Invalid("Could not update account access.");
        await Log(admin, admin.SchoolId, "staff.access.updated", before, AppJson.Write(new { member.Id, input.Active, input.Roles }));
        return Results.Ok(new { member.Id, input.Active, input.Roles });
    }
    public virtual async Task<OperationResult> DocumentsAsync()
    {
        var user = await Current(); var parentIds = (await access.Visible(user, Principal)).Select(a => a.Id).ToList();
        var batches = await AsyncExecuter.ToListAsync((await AiBatchesRepository.GetQueryableAsync()).Where(b => parentIds.Contains(b.AssessmentId)));
        var records = await AsyncExecuter.ToListAsync((await InstitutionRecords.GetQueryableAsync()).Where(r => r.SchoolId == user.SchoolId && (r.Kind == "memoApproval" || r.Kind == "documentVersion")));
        return Results.Ok(batches.Select(b => { var approval = records.FirstOrDefault(r => r.Kind == "memoApproval" && r.Code == b.Id.ToString()); var version = records.FirstOrDefault(r => r.Kind == "documentVersion" && r.Code == b.Id.ToString()); return new { b.Id, b.AssessmentId, b.PaperName, b.MemoName, b.UploadedBy, b.CreatedAt, paperHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(b.Paper)), memoHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(b.Memo)), approvedBy = approval is null ? null : Fields(approval).GetValueOrDefault("approvedBy"), previousBatchId = version is null ? null : Fields(version).GetValueOrDefault("previousBatchId") }; }));
    }
    [Authorize(ProsperaPermissions.Moderate)]
    [UnitOfWork(true)]
    public virtual async Task<OperationResult> ApproveMemoAsync(Guid batchId)
    {
        var user = await Current();
        var batch = await AsyncExecuter.FirstOrDefaultAsync((await AiBatchesRepository.GetQueryableAsync()), b => b.Id == batchId); if (batch is null) return Results.NotFound();
        var assessment = await AsyncExecuter.FirstOrDefaultAsync((await AssessmentsRepository.GetQueryableAsync()), a => a.Id == batch.AssessmentId && a.SchoolId == user.SchoolId);
        if (assessment is null || !await access.CanAccess(user, Principal, assessment)) return Results.NotFound();
        if (batch.UploadedBy == user.Id) return Invalid("A different academic reviewer must approve the memorandum.");
        if (await AsyncExecuter.AnyAsync((await InstitutionRecords.GetQueryableAsync()), r => r.SchoolId == user.SchoolId && r.Kind == "memoApproval" && r.Code == batchId.ToString())) return Results.Conflict(new { error = "This document version is already approved." });
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(batch.Memo));
        var row = new InstitutionRecord(GuidGenerator.Create()) { SchoolId = user.SchoolId, Kind = "memoApproval", Code = batchId.ToString(), Name = batch.MemoName, Revision = 1, DataJson = AppJson.Write(new Dictionary<string, string> { ["approvedBy"] = user.Id, ["memoHash"] = hash, ["approvedAt"] = DateTimeOffset.UtcNow.ToString("O") }) };
        await InstitutionRecords.InsertAsync(row); await Log(user, batchId, "memorandum.approved", "", row.DataJson); await UnitOfWorkManager.Current!.SaveChangesAsync(); return Results.Ok(View(row));
    }
    [Authorize(ProsperaPermissions.Moderate)]
    [UnitOfWork(true)]
    public virtual async Task<OperationResult> AssignAsync(Guid scriptId, AssignmentInput input)
    {
        var user = await Current();
        var script = await AsyncExecuter.FirstOrDefaultAsync((await AiScriptsRepository.GetQueryableAsync()), s => s.Id == scriptId); if (script is null) return Results.NotFound();
        var batch = await AsyncExecuter.FirstAsync((await AiBatchesRepository.GetQueryableAsync()), b => b.Id == script.BatchId);
        var assessment = await AsyncExecuter.FirstOrDefaultAsync((await AssessmentsRepository.GetQueryableAsync()), a => a.Id == batch.AssessmentId && a.SchoolId == user.SchoolId);
        if (assessment is null || !await access.CanAccess(user, Principal, assessment)) return Results.NotFound();
        if (script.Status is "Approved" or "Moderated") return Invalid("Approved or moderated scripts cannot be reassigned.");
        var teacher = await users.FindByIdAsync(input.TeacherId); var moderator = await users.FindByIdAsync(input.ModeratorId);
        if (teacher is null || moderator is null || !teacher.Active || !moderator.Active || teacher.SchoolId != user.SchoolId || moderator.SchoolId != user.SchoolId || teacher.Id == moderator.Id) return Invalid("Choose different active reviewers from this institution.");
        if (!(await users.GetRolesAsync(teacher)).Any(r => r is "Teacher" or "Lecturer" or "Professor" or "AcademicAdmin") || !(await users.GetRolesAsync(moderator)).Any(r => r is "HOD" or "Moderator" or "AcademicAdmin")) return Invalid("Choose teaching staff and a qualified moderator.");
        // Assigned teachers must already have academic access; assignment cannot grant it.
        var teacherRoles = await users.GetRolesAsync(teacher);
        var teacherPrincipal = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(teacherRoles.Select(r => new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, r))));
        if (!await access.CanAccess(teacher, teacherPrincipal, assessment)) return Invalid("The teacher must have the assessment's teaching allocation.");
        var row = await AsyncExecuter.FirstOrDefaultAsync((await InstitutionRecords.GetQueryableAsync()), r => r.SchoolId == user.SchoolId && r.Kind == "assignment" && r.Code == scriptId.ToString());
        if (row is not null && row.Revision != input.Revision) return Results.Conflict(new { error = "Assignment changed. Reload first." });
        var before = row is null ? "" : AppJson.Write(View(row)); var created = row is null;
        row ??= new InstitutionRecord(GuidGenerator.Create()) { SchoolId = user.SchoolId, Kind = "assignment", Code = scriptId.ToString(), Name = script.Candidate };
        row.DataJson = AppJson.Write(new Dictionary<string, string> { ["teacherId"] = teacher.Id, ["moderatorId"] = moderator.Id, ["dueAt"] = input.DueAt?.ToString("O") ?? "" }); row.Revision++;
        if (created) await InstitutionRecords.InsertAsync(row); else await InstitutionRecords.UpdateAsync(row);
        await Log(user, row.Id, "moderation.assigned", before, AppJson.Write(View(row)));
        await UnitOfWorkManager.Current!.SaveChangesAsync(); return Results.Ok(View(row));
    }
}
