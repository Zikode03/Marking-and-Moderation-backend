using System.ComponentModel.DataAnnotations;
namespace ProsperaAssess;

public class AiBatch : Volo.Abp.Domain.Entities.BasicAggregateRoot<Guid>
{
    public AiBatch(Guid id) : base(id) { }
    protected AiBatch() { }
    public Guid AssessmentId { get; set; }
    public string UploadedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string PaperName { get; set; } = "";
    public string PaperType { get; set; } = "";
    public byte[] Paper { get; set; } = [];
    public string MemoName { get; set; } = "";
    public string MemoType { get; set; } = "";
    public byte[] Memo { get; set; } = [];
    public string ScriptLanguage { get; set; } = "auto";
    public string MemoLanguage { get; set; } = "auto";
}

public partial class AiScript : Volo.Abp.Domain.Entities.BasicAggregateRoot<Guid>
{
    public AiScript(Guid id) : base(id) { }
    protected AiScript() { }
    public Guid BatchId { get; set; }
    public string Candidate { get; set; } = "";
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public byte[] Content { get; set; } = [];
    public string Hash { get; set; } = "";
    public string Status { get; protected set; } = "Uploaded";
    public DateTimeOffset UpdatedAt { get; protected set; }
    public string? ResultJson { get; protected set; }
    public string? Error { get; protected set; }
    public string? Model { get; protected set; }
    public string? ProviderResponseId { get; protected set; }
    public string AuditJson { get; protected set; } = "[]";
    [ConcurrencyCheck] public int Revision { get; protected set; }
    public AiMarkingResult? Result() => ResultJson is null ? null : System.Text.Json.JsonSerializer.Deserialize<AiMarkingResult>(ResultJson, AppJson.Options);
    public void Audit(string actor, string action, string reason)
    {
        var history = System.Text.Json.JsonSerializer.Deserialize<List<AiHistory>>(AuditJson, AppJson.Options) ?? [];
        history.Add(new(DateTimeOffset.UtcNow, actor, action, reason, Result()));
        AuditJson = AppJson.Write(history); Revision++; UpdatedAt = DateTimeOffset.UtcNow;
    }
}
