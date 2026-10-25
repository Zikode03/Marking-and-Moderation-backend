using System.Security.Claims;
using System.Text;
using System.Security.Cryptography;
using System.Text.Json;
namespace ProsperaAssess;

public class SubmissionAppService : ProsperaAppService, ISubmissionAppService
{
    [Microsoft.AspNetCore.Authorization.Authorize(ProsperaPermissions.Workspace)]
    public virtual async Task<OperationResult> GetAsync(Guid id)
    {
        var principal = Principal;

        var user = await users.GetUserAsync(principal);
        if (user is null || user.SchoolId == Guid.Empty) return Results.Unauthorized();
        var row = await AsyncExecuter.FirstOrDefaultAsync((await SubmissionsRepository.GetQueryableAsync()), s => s.Id == id && s.SchoolId == user.SchoolId);
        if (row is null) return Results.NotFound();
        var parent = await AsyncExecuter.FirstAsync((await AssessmentsRepository.GetQueryableAsync()), a => a.Id == row.AssessmentId && a.SchoolId == user.SchoolId);
        return !await access.CanAccess(user, principal, parent) ? Results.NotFound() : Results.Ok(row.ToView());

    }
    [Microsoft.AspNetCore.Authorization.Authorize(ProsperaPermissions.Workspace)]
    public virtual async Task<OperationResult> GetListAsync(Guid? assessmentId)
    {
        var principal = Principal;

        var user = await users.GetUserAsync(principal);
        if (user is null || user.SchoolId == Guid.Empty) return Results.Unauthorized();
        var visibleIds = (await access.Visible(user, principal)).Select(a => a.Id).ToList();
        var rows = await AsyncExecuter.ToListAsync((await SubmissionsRepository.GetQueryableAsync()).Where(s => s.SchoolId == user.SchoolId && visibleIds.Contains(s.AssessmentId) &&
            (assessmentId == null || s.AssessmentId == assessmentId)).OrderBy(s => s.StudentReference));
        return Results.Ok(rows.Select(s => s.ToView()));

    }
    [Microsoft.AspNetCore.Authorization.Authorize(ProsperaPermissions.Workspace)]
    [Volo.Abp.Uow.UnitOfWork(true)]
    public virtual async Task<OperationResult> RegisterLegacyAsync(CreateSubmission request)
    {
        var principal = Principal;

        var user = await users.GetUserAsync(principal);
        if (user is null || user.SchoolId == Guid.Empty) return Results.Unauthorized();
        if (!InputRules.Text(request.StudentReference, 100) || !InputRules.Text(request.AnswerText, 200_000))
            return Results.BadRequest(new { error = "Student reference (up to 100 characters) and answer (up to 200000 characters) are required." });
        var parent = await AsyncExecuter.FirstOrDefaultAsync((await AssessmentsRepository.GetQueryableAsync()), a => a.Id == request.AssessmentId && a.SchoolId == user.SchoolId);
        if (parent is null || !await access.CanAccess(user, principal, parent)) return Results.NotFound();
        var submission = new SubmissionRow(GuidGenerator.Create())
        {
            AssessmentId = request.AssessmentId,
            SchoolId = user.SchoolId,
            StudentReference = request.StudentReference!.Trim(),
            AnswerText = request.AnswerText!
        };
        submission.AddAudit(user.Id, "Submitted", "Submission registered");
        await SubmissionsRepository.InsertAsync(submission);
        await UnitOfWorkManager.Current!.SaveChangesAsync();
        return Results.Created($"/api/submissions/{submission.Id}", submission.ToView());

    }
    [Microsoft.AspNetCore.Authorization.Authorize(ProsperaPermissions.Mark)]
    [Volo.Abp.Uow.UnitOfWork(true)]
    public virtual async Task<OperationResult> MarkAsync(Guid id, MarkRequest request)
    {
        var principal = Principal;

        var user = await users.GetUserAsync(principal);
        if (user is null || user.SchoolId == Guid.Empty) return Results.Unauthorized();
        var submission = await AsyncExecuter.FirstOrDefaultAsync((await SubmissionsRepository.GetQueryableAsync()), s => s.Id == id && s.SchoolId == user.SchoolId);
        if (submission is null) return Results.NotFound();
        var parent = await AsyncExecuter.FirstAsync((await AssessmentsRepository.GetQueryableAsync()), a => a.Id == submission.AssessmentId && a.SchoolId == user.SchoolId);
        if (!await access.CanAccess(user, principal, parent)) return Results.NotFound();
        if (request.Revision is { } revision && revision != submission.Revision()) return Conflict();
        if (submission.Status is not ("Submitted" or "Marked"))
            return Results.Conflict(new { error = "Only submitted or marked work can be marked. Moderated and approved results are locked." });
        var assessment = await AsyncExecuter.FirstAsync((await AssessmentsRepository.GetQueryableAsync()), a => a.Id == submission.AssessmentId && a.SchoolId == user.SchoolId);
        var criteria = assessment.Criteria();
        if (request.Marks is null || request.Marks.Count != criteria.Count ||
            criteria.Any(c => !request.Marks.TryGetValue(c.Id, out var mark) || mark < 0 || mark > c.MaxMarks) ||
            request.Feedback?.Length > 10_000)
            return Results.BadRequest(new { error = "Supply every rubric mark within its range and feedback up to 10000 characters." });
        submission.RecordMarks(user.Id, request.Marks, request.Feedback, criteria);
        await SubmissionsRepository.UpdateAsync(submission);
        await UnitOfWorkManager.Current!.SaveChangesAsync();
        return Results.Ok(submission.ToView());

    }
    [Microsoft.AspNetCore.Authorization.Authorize(ProsperaPermissions.Moderate)]
    [Volo.Abp.Uow.UnitOfWork(true)]
    public virtual async Task<OperationResult> ModerateAsync(Guid id, ModerationRequest request)
    {
        var principal = Principal;

        var user = await users.GetUserAsync(principal);
        if (user is null || user.SchoolId == Guid.Empty) return Results.Unauthorized();
        var submission = await AsyncExecuter.FirstOrDefaultAsync((await SubmissionsRepository.GetQueryableAsync()), s => s.Id == id && s.SchoolId == user.SchoolId);
        if (submission is null) return Results.NotFound();
        var parent = await AsyncExecuter.FirstAsync((await AssessmentsRepository.GetQueryableAsync()), a => a.Id == submission.AssessmentId && a.SchoolId == user.SchoolId);
        if (!await access.CanAccess(user, principal, parent)) return Results.NotFound();
        if (request.Revision is { } revision && revision != submission.Revision()) return Conflict();
        if (submission.Status != "Marked") return Results.Conflict(new { error = "Only marked work can be moderated." });
        if (submission.LastMarkerId == user.Id) return Results.Conflict(new { error = "Marker cannot moderate their own marking." });
        if (request.Approve is null || !InputRules.Text(request.Comments, 10_000))
            return Results.BadRequest(new { error = "An explicit approval decision and comments up to 10000 characters are required." });
        submission.Moderate(user.Id, request.Approve.Value, request.Comments!.Trim());
        await SubmissionsRepository.UpdateAsync(submission);
        await UnitOfWorkManager.Current!.SaveChangesAsync();
        return Results.Ok(submission.ToView());

    }
    [Microsoft.AspNetCore.Authorization.Authorize(ProsperaPermissions.Approve)]
    [Volo.Abp.Uow.UnitOfWork(true)]
    public virtual async Task<OperationResult> ApproveAsync(Guid id)
    {
        var principal = Principal;

        var user = await users.GetUserAsync(principal);
        if (user is null || user.SchoolId == Guid.Empty) return Results.Unauthorized();
        var submission = await AsyncExecuter.FirstOrDefaultAsync((await SubmissionsRepository.GetQueryableAsync()), s => s.Id == id && s.SchoolId == user.SchoolId);
        if (submission is null) return Results.NotFound();
        var parent = await AsyncExecuter.FirstAsync((await AssessmentsRepository.GetQueryableAsync()), a => a.Id == submission.AssessmentId && a.SchoolId == user.SchoolId);
        if (!await access.CanAccess(user, principal, parent)) return Results.NotFound();
        if (submission.Status != "Moderated") return Results.Conflict(new { error = "Moderation required before approval." });
        submission.Approve(user.Id);
        await SubmissionsRepository.UpdateAsync(submission);
        await UnitOfWorkManager.Current!.SaveChangesAsync();
        return Results.Ok(submission.ToView());

    }
    [Microsoft.AspNetCore.Authorization.Authorize(ProsperaPermissions.Mark)]
    [Volo.Abp.Uow.UnitOfWork(true)]
    public virtual async Task<OperationResult> GetAiSuggestionAsync(Guid id)
    {
        var principal = Principal;

        var user = await users.GetUserAsync(principal);
        if (user is null || user.SchoolId == Guid.Empty) return Results.Unauthorized();
        var row = await AsyncExecuter.FirstOrDefaultAsync((await SubmissionsRepository.GetQueryableAsync()), s => s.Id == id && s.SchoolId == user.SchoolId);
        if (row is null) return Results.NotFound();
        var parent = await AsyncExecuter.FirstAsync((await AssessmentsRepository.GetQueryableAsync()), a => a.Id == row.AssessmentId && a.SchoolId == user.SchoolId);
        if (!await access.CanAccess(user, principal, parent)) return Results.NotFound();
        return Results.Problem("No validated proprietary AI model is deployed.", statusCode: 503);

    }
    private static OperationResult Conflict() => Results.Conflict(new { error = "This submission changed. Refresh before saving again." });

}