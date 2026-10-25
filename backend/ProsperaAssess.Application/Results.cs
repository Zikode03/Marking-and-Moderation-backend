namespace ProsperaAssess;

internal static class Results
{
    public static OperationResult Ok(object? value = null) => new(System.Text.Json.JsonSerializer.SerializeToElement(value, AppJson.Options));
    public static OperationResult Created(string location, object value) => new(System.Text.Json.JsonSerializer.SerializeToElement(value, AppJson.Options), 201, location);
    public static OperationResult File(byte[] content, string type, string name) => new(Content: content, ContentType: type, FileName: name);
    public static OperationResult Json(object value, int statusCode = 200) => statusCode >= 400 ? throw new PortalBusinessException(statusCode, value) : new(System.Text.Json.JsonSerializer.SerializeToElement(value, AppJson.Options), statusCode);
    public static OperationResult BadRequest(object? value = null) => throw new PortalBusinessException(400, value);
    public static OperationResult NotFound() => throw new PortalBusinessException(404, null);
    public static OperationResult Unauthorized() => throw new PortalBusinessException(401, null);
    public static OperationResult Forbid() => throw new PortalBusinessException(403, null);
    public static OperationResult Conflict(object? value = null) => throw new PortalBusinessException(409, value);
    public static OperationResult Problem(string message, int statusCode) => throw new PortalBusinessException(statusCode, new { detail = message });
}
