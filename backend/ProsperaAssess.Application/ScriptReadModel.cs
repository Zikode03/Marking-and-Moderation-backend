using System.Text.Json;
namespace ProsperaAssess;

internal sealed class ScriptReadModel
{
    public Guid Id { get; init; }
    public Guid BatchId { get; init; }
    public string Candidate { get; init; } = ""; public string FileName { get; init; } = ""; public string Status { get; init; } = "";
    public DateTimeOffset UpdatedAt { get; init; }
    public string? ResultJson { get; init; }
    public string? Error { get; init; }
    public string? Model { get; init; }
    public string? ProviderResponseId { get; init; }
    public int Revision { get; init; }
    public string AuditJson { get; init; } = "[]";
    public ScriptDto View()
    {
        var result = ResultJson is null ? null : JsonSerializer.Deserialize<AiMarkingResult>(ResultJson, AppJson.Options);
        return new(Id, BatchId, Candidate, FileName, Status, UpdatedAt, result, result is not null && !result.NeedsAttention ? result.Questions.Sum(q => q.Marks) : null, Error, Model, ProviderResponseId, Revision, JsonSerializer.Deserialize<List<AiHistory>>(AuditJson, AppJson.Options) ?? []);
    }
}
