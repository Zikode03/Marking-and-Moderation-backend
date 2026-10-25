namespace ProsperaAssess;

public record ModelHealth(bool Ready, string Status, string? Model, List<string>? Languages = null);
public record LocalMarkResponse(string Model, string RunId, AiMarkingResult Result);
public interface IAiMarkingEngine { Task<ModelHealth> Health(CancellationToken ct); Task<LocalMarkResponse> Mark(AiBatch batch, AiScript script, CancellationToken ct, string academicContext = "{}"); }
public interface IAiQueueRepository { Task<AiScript?> ClaimNextAsync(CancellationToken ct); Task RecoverInterruptedAsync(DateTimeOffset cutoff, CancellationToken ct); }
