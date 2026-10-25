using Volo.Abp;
namespace ProsperaAssess;

public static class WorkflowErrors
{
    public const string Locked = "Prospera:WorkflowLocked";
    public const string Marks = "Prospera:InvalidMarks";
}
public partial class SubmissionRow
{
    public void RecordMarks(string actor, Dictionary<Guid, decimal> marks, string? feedback, IReadOnlyList<Criterion> criteria)
    {
        if (Status is not ("Submitted" or "Marked" or "ReturnedForRemarking")) throw new BusinessException(WorkflowErrors.Locked);
        if (marks.Count != criteria.Count || criteria.Any(c => !marks.TryGetValue(c.Id, out var m) || m < 0 || m > c.MaxMarks)) throw new BusinessException(WorkflowErrors.Marks);
        MarksJson = AppJson.Write(marks); Feedback = feedback; LastMarkerId = actor; Status = "Marked"; AddAudit(actor, "Marked", feedback ?? "");
    }
    public void Moderate(string actor, bool approve, string comments)
    {
        if (Status != "Marked" || LastMarkerId == actor) throw new BusinessException(WorkflowErrors.Locked);
        Status = approve ? "Moderated" : "ReturnedForRemarking"; AddAudit(actor, approve ? "Moderated" : "Returned", comments);
    }
    public void Approve(string actor)
    {
        if (Status != "Moderated") throw new BusinessException(WorkflowErrors.Locked);
        Status = "Approved"; AddAudit(actor, "Approved", "Approved academic result");
    }
}
public partial class AiScript
{
    public void RecordAiResult(LocalMarkResponse response)
    {
        if (Status != "Processing") throw new BusinessException(WorkflowErrors.Locked);
        AiMarkingPolicy.Validate(response.Result, true);
        if (string.IsNullOrWhiteSpace(response.Model) || string.IsNullOrWhiteSpace(response.RunId)) throw new BusinessException(WorkflowErrors.Marks);
        ResultJson = AppJson.Write(response.Result); Model = response.Model; ProviderResponseId = response.RunId;
        Status = response.Result.NeedsAttention ? "NeedsAttention" : "AwaitingModeration"; Error = null;
        Audit("local-ai", "AiMarked", "Question-level marks from local model; not released");
    }
    public void FailMarking(string reason)
    {
        if (Status != "Processing") throw new BusinessException(WorkflowErrors.Locked);
        Status = "Failed"; Error = reason; Audit("local-ai", "Failed", reason);
    }
    public void Queue(string actor)
    {
        if (Status is not ("Uploaded" or "Failed")) throw new BusinessException(WorkflowErrors.Locked);
        Status = "Queued"; Error = null; Audit(actor, "Queued", "Requested local AI marking");
    }
    public void ConfirmTeacherReview(string actor, Dictionary<string, decimal> marks, string reason)
    {
        var result = Result();
        if (Status is not ("AwaitingModeration" or "NeedsAttention" or "ReturnedToTeacher") || result is null) throw new BusinessException(WorkflowErrors.Locked);
        if (string.IsNullOrWhiteSpace(reason) || marks.Count != result.Questions.Count ||
            result.Questions.Any(q => !marks.TryGetValue(q.Number, out var m) || m < 0 || m > q.MaxMarks)) throw new BusinessException(WorkflowErrors.Marks);
        Audit(actor, "TeacherReviewStarted", reason);
        ResultJson = AppJson.Write(result with { NeedsAttention = false, Questions = result.Questions.Select(q => q with { Marks = marks[q.Number], NeedsReview = false }).ToList() });
        Status = "TeacherReviewed";
        Audit(actor, "TeacherReviewed", reason);
    }
    public void ConfirmModeration(string actor, Dictionary<string, decimal> marks, string reason)
    {
        if (LatestTeacherReviewer() == actor) throw new BusinessException(WorkflowErrors.Locked);
        var result = Result();
        if (Status != "TeacherReviewed" || result is null) throw new BusinessException(WorkflowErrors.Locked);
        if (marks.Count != result.Questions.Count || result.Questions.Any(q => !marks.TryGetValue(q.Number, out var m) || m < 0 || m > q.MaxMarks)) throw new BusinessException(WorkflowErrors.Marks);
        Audit(actor, "ModerationStarted", reason);
        ResultJson = AppJson.Write(result with { NeedsAttention = false, Questions = result.Questions.Select(q => q with { Marks = marks[q.Number], NeedsReview = false }).ToList() });
        Status = "Moderated"; Audit(actor, "Moderated", reason);
    }
    private string? LatestTeacherReviewer() =>
        (System.Text.Json.JsonSerializer.Deserialize<List<AiHistory>>(AuditJson, AppJson.Options) ?? [])
        .LastOrDefault(h => h.Action == "TeacherReviewed")?.Actor;
    public void ReturnToTeacher(string actor, string reason)
    {
        if (LatestTeacherReviewer() == actor) throw new BusinessException(WorkflowErrors.Locked);
        if (Status != "TeacherReviewed" || string.IsNullOrWhiteSpace(reason)) throw new BusinessException(WorkflowErrors.Locked);
        Status = "ReturnedToTeacher"; Audit(actor, "ReturnedToTeacher", reason);
    }
    public void Approve(string actor)
    {
        if (Status != "Moderated") throw new BusinessException(WorkflowErrors.Locked);
        Status = "Approved"; Audit(actor, "Approved", "Approved for authorised release");
    }
}
