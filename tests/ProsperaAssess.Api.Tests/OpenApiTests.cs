using System.Text.Json;
using Xunit;
namespace ProsperaAssess.Api.Tests;

public class OpenApiTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task GeneratedDocumentContainsAccountFlowsAndValidLocalReferences()
    {
        using var client = fixture.Client();
        var response = await client.GetAsync("/openapi/v1.json");
        var json = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, json);
        var root = JsonDocument.Parse(json).RootElement;
        Assert.StartsWith("3.", root.GetProperty("openapi").GetString());
        var paths = root.GetProperty("paths");
        foreach (var path in new[] { "/api/data-protection", "/api/operations/metrics", "/api/data-protection/batches/{id}/purge", "/login", "/refresh", "/api/account/password/forgot", "/api/account/password/reset", "/api/account/password/change", "/api/account/mfa/setup", "/api/account/mfa/enable", "/api/account/mfa/disable", "/api/account/mfa/replace", "/api/account/mfa/recovery-codes", "/api/account/sessions/revoke", "/api/sales/enquiries", "/api/academics" }) Assert.True(paths.TryGetProperty(path, out _), path);
        Assert.False(paths.TryGetProperty("/register", out _));
        Assert.Equal("bearer", root.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer").GetProperty("scheme").GetString());
        Assert.NotEmpty(paths.GetProperty("/api/account/security").GetProperty("get").GetProperty("security").EnumerateArray());
        var ids = new HashSet<string>();
        foreach (var path in paths.EnumerateObject()) foreach (var operation in path.Value.EnumerateObject())
            if (operation.Value.ValueKind == JsonValueKind.Object && operation.Value.TryGetProperty("operationId", out var id)) Assert.True(ids.Add(id.GetString()!), "Duplicate operation ID");
        void CheckReferences(JsonElement node)
        {
            if (node.ValueKind == JsonValueKind.Array) { foreach (var child in node.EnumerateArray()) CheckReferences(child); }
            if (node.ValueKind != JsonValueKind.Object) return;
            foreach (var property in node.EnumerateObject())
            {
                if (property.Name == "$ref" && property.Value.GetString() is { } reference && reference.StartsWith("#/"))
                {
                    var target = root;
                    foreach (var segment in reference[2..].Split('/')) Assert.True(target.TryGetProperty(segment.Replace("~1", "/").Replace("~0", "~"), out target), "Unresolved schema reference: " + reference);
                }
                else CheckReferences(property.Value);
            }
        }
        CheckReferences(root);
        if (Environment.GetEnvironmentVariable("PROSPERA_OPENAPI_OUTPUT") is { Length: > 0 } file) { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!); await File.WriteAllTextAsync(file, json); }
    }
}
