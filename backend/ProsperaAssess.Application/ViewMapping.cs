using System.Text.Json;
namespace ProsperaAssess;

public static class ViewMapping
{
    public static AssessmentDto ToView(this AssessmentRow row) => new(row.Id, row.Title, row.Code, row.Criteria().Sum(c => c.MaxMarks), row.Criteria(), row.SchoolContext(), row.InstitutionContext());
    public static SubmissionDto ToView(this SubmissionRow row)
    {
        var marks = JsonSerializer.Deserialize<Dictionary<Guid, decimal>>(row.MarksJson, AppJson.Options) ?? [];
        return new(row.Id, row.AssessmentId, row.StudentReference, row.AnswerText, row.Status, marks, row.Feedback, marks.Values.Sum(), JsonSerializer.Deserialize<List<AuditEntry>>(row.AuditJson, AppJson.Options) ?? [], row.Revision());
    }
    public static ScriptDto View(this AiScript row)
    {
        var result = row.Result();
        return new(row.Id, row.BatchId, row.Candidate, row.FileName, row.Status, row.UpdatedAt, result, result is not null && !result.NeedsAttention ? result.Questions.Sum(q => q.Marks) : null, row.Error, row.Model, row.ProviderResponseId, row.Revision, JsonSerializer.Deserialize<List<AiHistory>>(row.AuditJson, AppJson.Options) ?? []);
    }
}
