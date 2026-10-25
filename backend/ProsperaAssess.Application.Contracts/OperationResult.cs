namespace ProsperaAssess;

public record OperationResult(System.Text.Json.JsonElement? Data = null, int StatusCode = 200, string? Location = null, byte[]? Content = null, string? ContentType = null, string? FileName = null);
public sealed class PortalBusinessException(int status, object? details) : Volo.Abp.BusinessException("Prospera:OperationRejected") { public int Status { get; } = status; public object? ErrorData { get; } = details; }
