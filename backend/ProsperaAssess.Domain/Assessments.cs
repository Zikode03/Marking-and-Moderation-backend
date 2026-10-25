using System.Text.Json;
namespace ProsperaAssess;

public class AssessmentRow : Volo.Abp.Domain.Entities.BasicAggregateRoot<Guid>
{
    public AssessmentRow(Guid id) : base(id) { }
    protected AssessmentRow() { }
    public Guid SchoolId { get; set; }
    public string Title { get; set; } = "";
    public string Code { get; set; } = ""; public string CriteriaJson { get; set; } = "[]"; public string? SchoolJson { get; set; }
    public string? InstitutionJson { get; set; }
    public SchoolContext? SchoolContext() => SchoolJson is null ? null : JsonSerializer.Deserialize<SchoolContext>(SchoolJson, AppJson.Options);
    public InstitutionContext? InstitutionContext() => InstitutionJson is null ? null : JsonSerializer.Deserialize<InstitutionContext>(InstitutionJson, AppJson.Options);
    public List<Criterion> Criteria() => JsonSerializer.Deserialize<List<Criterion>>(CriteriaJson, AppJson.Options) ?? [];
}
public partial class SubmissionRow : Volo.Abp.Domain.Entities.BasicAggregateRoot<Guid>
{
    public SubmissionRow(Guid id) : base(id) { }
    protected SubmissionRow() { }
    public Guid SchoolId { get; set; }
    public Guid AssessmentId { get; set; }
    public string StudentReference { get; set; } = ""; public string AnswerText { get; set; } = "";
    public string Status { get; protected set; } = "Submitted"; public string MarksJson { get; protected set; } = "{}";
    public string? LastMarkerId { get; protected set; }
    public string? Feedback { get; protected set; }
    public string AuditJson { get; protected set; } = "[]";
    public int Revision() => (JsonSerializer.Deserialize<List<AuditEntry>>(AuditJson, AppJson.Options) ?? []).Count;
    public void AddAudit(string actor, string action, string notes) { var entries = JsonSerializer.Deserialize<List<AuditEntry>>(AuditJson, AppJson.Options) ?? []; entries.Add(new AuditEntry(DateTimeOffset.UtcNow, actor, action, notes, JsonSerializer.Deserialize<Dictionary<Guid, decimal>>(MarksJson, AppJson.Options) ?? new(), Feedback, Status)); AuditJson = JsonSerializer.Serialize(entries, AppJson.Options); }
}
