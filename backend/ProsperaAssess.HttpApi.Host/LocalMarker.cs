using System.Net.Http.Json;
using ProsperaAssess;
namespace ProsperaAssess.Api;

public sealed class LocalMarker(HttpClient http, IConfiguration config) : IAiMarkingEngine
{
    private Uri Endpoint(string path) => new(new Uri(config["AI:ServiceUrl"] ?? "http://127.0.0.1:8000/"), path);
    public async Task<ModelHealth> Health(CancellationToken ct)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(3));
            return await http.GetFromJsonAsync<ModelHealth>(Endpoint("health"), timeout.Token) ?? new(false, "Model not ready", null);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException) { return new(false, "Local AI service unavailable", null); }
    }
    public async Task<LocalMarkResponse> Mark(AiBatch batch, AiScript script, CancellationToken ct, string academicContext = "{}")
    {
        using var form = new MultipartFormDataContent();
        foreach (var (name, content, type, filename) in new[] { ("paper", batch.Paper, batch.PaperType, batch.PaperName), ("memo", batch.Memo, batch.MemoType, batch.MemoName), ("script", script.Content, script.ContentType, "candidate" + Path.GetExtension(script.FileName)) })
        {
            var part = new ByteArrayContent(content); part.Headers.ContentType = new(type); form.Add(part, name, filename);
        }
        form.Add(new StringContent(script.Id.ToString()), "request_id");
        form.Add(new StringContent(batch.ScriptLanguage), "script_language");
        form.Add(new StringContent(batch.MemoLanguage), "memo_language");
        form.Add(new StringContent(academicContext), "academic_context");
        using var response = await http.PostAsync(Endpoint("mark-files"), form, ct);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException(response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable ? "Local model is not ready. Uploads are retained; retry after training/deployment." : "Local marking failed. Check the AI service, then retry.");
        var result = await response.Content.ReadFromJsonAsync<LocalMarkResponse>(ct) ?? throw new InvalidOperationException("The model returned no result.");
        Validate(result.Result, requireMemoEvidence: true);
        if (string.IsNullOrWhiteSpace(result.Model) || string.IsNullOrWhiteSpace(result.RunId)) throw new InvalidOperationException("The model did not identify its version and run.");
        return result;
    }
    public static void Validate(AiMarkingResult result, bool requireMemoEvidence = false) => AiMarkingPolicy.Validate(result, requireMemoEvidence);
}
