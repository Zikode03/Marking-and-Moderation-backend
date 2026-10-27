using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace ProsperaAssess.Api.Tests;

public class AiTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private static MultipartFormDataContent Files(int grade = 10, bool duplicate = false, string scriptLanguage = "auto", string memoLanguage = "auto")
    {
        var nonce = Guid.NewGuid().ToString("N");
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(AppJson.Write(new { title = "Fictional AI workflow test", scriptLanguage, memoLanguage, school = new { grade, subject = "Mathematics", term = 1, assessmentType = "Test", schoolType = "Public", pathway = "DBE-NSC" } })), "context");
        foreach (var (name, text, filename) in new[] { ("paper", "Question", "paper.pdf"), ("memo", "Guide", "memo.pdf"), ("scripts", "Fictional answer", "C001.pdf") })
            form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("%PDF-1.4 " + text + nonce)), name, filename);
        if (duplicate) form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("%PDF-1.4 Fictional answer" + nonce)), "scripts", "C002.pdf");
        return form;
    }
    private async Task<Guid> Upload(HttpClient client)
    {
        using var files = Files(); using var response = await client.PostAsync("/api/ai/batches", files);
        var text = await response.Content.ReadAsStringAsync(); Assert.True(response.StatusCode == HttpStatusCode.Created, text);
        return JsonDocument.Parse(text).RootElement.GetProperty("id").GetGuid();
    }
    [Fact]
    public async Task Upload_PersistsOriginals_AndWaitsForModel()
    {
        using var teacher = fixture.Client("Teacher"); var id = await Upload(teacher);
        using var response = await teacher.GetAsync($"/api/ai/batches/{id}/files/paper");
        Assert.StartsWith("%PDF-", await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await teacher.PostAsync($"/api/ai/batches/{id}/start", null)).StatusCode);
        var detail = await teacher.GetFromJsonAsync<JsonElement>($"/api/ai/batches/{id}");
        Assert.Equal("Uploaded", detail.GetProperty("scripts")[0].GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("scripts")[0].GetProperty("total").ValueKind);
        using var other = fixture.Client("OtherSchool"); using var lecturer = fixture.Client("Lecturer");
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/ai/batches/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await lecturer.GetAsync($"/api/ai/batches/{id}/files/paper")).StatusCode);
    }
    [Fact]
    public async Task Upload_RejectsUnallocatedGrades_InvalidSignatures_AndDuplicates()
    {
        using var teacher = fixture.Client("Teacher");
        using var wrongGrade = Files(11); Assert.Equal(HttpStatusCode.Forbidden, (await teacher.PostAsync("/api/ai/batches", wrongGrade)).StatusCode);
        using var duplicate = Files(duplicate: true); Assert.Equal(HttpStatusCode.BadRequest, (await teacher.PostAsync("/api/ai/batches", duplicate)).StatusCode);
        using var invalidLanguage = Files(scriptLanguage: "unsupported"); Assert.Equal(HttpStatusCode.BadRequest, (await teacher.PostAsync("/api/ai/batches", invalidLanguage)).StatusCode);
        Assert.Null(AiEndpoints.Detect(Encoding.UTF8.GetBytes("executable"), "script.pdf"));
        using var hod = fixture.Client("HOD"); using var files = Files(); Assert.Equal(HttpStatusCode.Forbidden, (await hod.PostAsync("/api/ai/batches", files)).StatusCode);
    }
    [Fact]
    public async Task TrainingExamples_RequireIndependentVerification_AndExportOnlyVerified()
    {
        using var teacher = fixture.Client("Teacher"); using var admin = fixture.Client("SchoolAdmin"); using var hod = fixture.Client("HOD");
        var batch = await Upload(teacher); var detail = await teacher.GetFromJsonAsync<JsonElement>($"/api/ai/batches/{batch}"); var script = detail.GetProperty("scripts")[0].GetProperty("id").GetGuid();
        var label = new { questionNumber = "1", question = "What is 2+2?", answer = "4", rubric = "4 earns 2 marks", maxMarks = 2, marks = 2 };
        var created = await teacher.PostAsJsonAsync($"/api/ai/batches/{batch}/scripts/{script}/training", label); Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var example = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal("", await admin.GetStringAsync($"/api/ai/batches/{batch}/training/export"));
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.PostAsync($"/api/ai/batches/{batch}/training/{example}/verify", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await hod.PostAsync($"/api/ai/batches/{batch}/training/{example}/verify", null)).StatusCode);
        var export = await admin.GetStringAsync($"/api/ai/batches/{batch}/training/export"); Assert.Contains("assessmentGroup", export); Assert.DoesNotContain("C001", export); Assert.DoesNotContain("labelledBy", export);
        var invalidMarked = new { questionNumber = "2", question = "Explain the method", answer = "Learner writing", rubric = "Credit method", maxMarks = 5, marks = 3, sourceMarked = true, learnerWritingOnly = false, preparationNotes = "Teacher marks excluded", datasetScope = "CAPS/Grade10/Mathematics/en" };
        Assert.Equal(HttpStatusCode.BadRequest, (await teacher.PostAsJsonAsync($"/api/ai/batches/{batch}/scripts/{script}/training", invalidMarked)).StatusCode);
        var marked = await teacher.PostAsJsonAsync($"/api/ai/batches/{batch}/scripts/{script}/training", new { questionNumber = "2", question = "Explain the method", answer = "Learner writing", rubric = "Credit method", maxMarks = 5, marks = 3, sourceMarked = true, learnerWritingOnly = true, preparationNotes = "Teacher marks excluded", datasetScope = "CAPS/Grade10/Mathematics/en" });
        Assert.Equal(HttpStatusCode.Created, marked.StatusCode);
        var markedId = (await marked.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await hod.PostAsync($"/api/ai/batches/{batch}/training/{markedId}/verify", null)).StatusCode);
        var markedExport = await admin.GetStringAsync($"/api/ai/batches/{batch}/training/export"); Assert.Contains("learnerGroup", markedExport); Assert.Contains("Teacher marks excluded", markedExport); Assert.DoesNotContain("C001", markedExport);
        using var other = fixture.Client("OtherSchool"); Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/ai/batches/{batch}/training")).StatusCode);
    }
    [Fact]
    public async Task Queue_MarksWithLocalContract_ThenRequiresModerationAndApproval()
    {
        // Test-only model transport: no real scripts or model performance claims.
        using var factory = fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<LocalMarker>();
            services.AddScoped(provider => new LocalMarker(new HttpClient(new TestModelTransport()), provider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()));
        }));
        using var client = factory.CreateClient(); client.DefaultRequestHeaders.Authorization = fixture.Client("Teacher").DefaultRequestHeaders.Authorization;
        using var languageFiles = Files(scriptLanguage: "zu", memoLanguage: "en");
        var languageUpload = await client.PostAsync("/api/ai/batches", languageFiles);
        Assert.Equal(HttpStatusCode.Created, languageUpload.StatusCode);
        var languageBatch = (await languageUpload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var languageDetail = await client.GetFromJsonAsync<JsonElement>($"/api/ai/batches/{languageBatch}");
        Assert.Equal("zu", languageDetail.GetProperty("scriptLanguage").GetString());
        Assert.Equal("en", languageDetail.GetProperty("memoLanguage").GetString());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsync($"/api/ai/batches/{languageBatch}/start", null)).StatusCode);
        var batch = await Upload(client); Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/ai/batches/{batch}/start", null)).StatusCode);
        JsonElement script = default;
        for (var i = 0; i < 30; i++)
        {
            var detail = await client.GetFromJsonAsync<JsonElement>($"/api/ai/batches/{batch}"); script = detail.GetProperty("scripts")[0];
            if (script.GetProperty("status").GetString() == "AwaitingModeration") break;
            await Task.Delay(300);
        }
        Assert.Equal("AwaitingModeration", script.GetProperty("status").GetString()); Assert.Equal(7, script.GetProperty("total").GetDecimal());
        var scriptId = script.GetProperty("id").GetGuid(); var revision = script.GetProperty("revision").GetInt32();
        using var admin = fixture.Client("SchoolAdmin"); using var hod = fixture.Client("HOD");
        var path = $"/api/ai/batches/{batch}/scripts/{scriptId}";
        Assert.DoesNotContain("C001", await admin.GetStringAsync($"/api/ai/batches/{batch}/results"));
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(path + "/approve", new { revision })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(path + "/moderate", new { revision, reason = "Checked", marks = new { Q1 = 7 } })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await hod.PostAsJsonAsync(path + "/moderate", new { revision = -1, reason = "Checked", marks = new { Q1 = 7 } })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await hod.PostAsJsonAsync(path + "/moderate", new { revision, reason = "Cannot skip teacher", marks = new { Q1 = 7 } })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await hod.PostAsJsonAsync(path + "/review", new { revision, reason = "Wrong role", marks = new { Q1 = 7 } })).StatusCode);
        var reviewed = await client.PostAsJsonAsync(path + "/review", new { revision, reason = "Verified learner feedback", marks = new Dictionary<string, decimal> { ["Q1"] = 8 }, feedback = new Dictionary<string, string> { ["Q1"] = "Show each calculation." }, summary = "You earned credit for your method. Practise showing every calculation." });
        Assert.Equal(HttpStatusCode.OK, reviewed.StatusCode);
        var reviewedResult = await reviewed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("TeacherReviewed", reviewedResult.GetProperty("status").GetString());
        Assert.Equal("You earned credit for your method. Practise showing every calculation.", reviewedResult.GetProperty("result").GetProperty("summary").GetString());
        Assert.Equal("Show each calculation.", reviewedResult.GetProperty("result").GetProperty("questions")[0].GetProperty("feedback").GetString());
        revision = reviewedResult.GetProperty("revision").GetInt32();
        var returned = await hod.PostAsJsonAsync(path + "/return", new { revision, reason = "Check the explanation" });
        Assert.Equal(HttpStatusCode.OK, returned.StatusCode);
        var returnedResult = await returned.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ReturnedToTeacher", returnedResult.GetProperty("status").GetString());
        revision = returnedResult.GetProperty("revision").GetInt32();
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(path + "/approve", new { revision })).StatusCode);
        var rereviewed = await client.PostAsJsonAsync(path + "/review", new { revision, reason = "Rechecked explanation", marks = new Dictionary<string, decimal> { ["Q1"] = 8 } });
        Assert.Equal(HttpStatusCode.OK, rereviewed.StatusCode);
        revision = (await rereviewed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("revision").GetInt32();
        var moderated = await hod.PostAsJsonAsync(path + "/moderate", new { revision, reason = "Reviewed against source memo", marks = new Dictionary<string, decimal> { ["Q1"] = 8 } }); Assert.Equal(HttpStatusCode.OK, moderated.StatusCode);
        var result = await moderated.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync(path + "/approve", new { revision = result.GetProperty("revision").GetInt32() })).StatusCode);
        Assert.Contains("C001", await admin.GetStringAsync($"/api/ai/batches/{batch}/results"));
        Assert.Equal(HttpStatusCode.Conflict, (await hod.PostAsJsonAsync(path + "/moderate", new { revision, reason = "Late edit", marks = new { Q1 = 1 } })).StatusCode);
    }
    [Fact]
    public void ModelOutput_RejectsUnboundedOrUnflaggedMarks()
    {
        Assert.Throws<InvalidOperationException>(() => LocalMarker.Validate(new(false, "Bad", [new("1", 5, 6, "answer", "feedback", false)])));
        Assert.Throws<InvalidOperationException>(() => LocalMarker.Validate(new(false, "Bad", [new("1", 5, null, "", "Unreadable", false)])));
        LocalMarker.Validate(new(true, "Needs review", [new("1", 5, null, "", "Unreadable", true)]));
        Assert.Throws<InvalidOperationException>(() => LocalMarker.Validate(new(false, "Mixed", [new("1", 5, 2, "answer", "feedback", false, "Memo", "mixed", 0.99m)]), true));
        Assert.Throws<InvalidOperationException>(() => LocalMarker.Validate(new(false, "Uncertain", [new("1", 5, 2, "answer", "feedback", false, "Memo point", "zu", 0.2m)]), true));
        Assert.Throws<InvalidOperationException>(() => LocalMarker.Validate(new(true, "Bad location", [new("1", 5, 2, "answer", "feedback", true, "Memo point", "zu", 0.9m, [new(1, 0.9m, 0.2m, 0.5m, 0.1m, "alternative_answer", "Check")])]), true));
        LocalMarker.Validate(new(true, "Review", [new("1", 5, null, "answer", "feedback", true, "Memo point", "zu", 0.2m, [new(1, 0.1m, 0.2m, 0.2m, 0.1m, "uncertain_handwriting", "Check reading")])]), true);
    }
    private sealed class TestModelTransport : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(request.Method == HttpMethod.Get ? (object)new ModelHealth(true, "test only", "fictional-test-model", ["en"]) : new LocalMarkResponse("fictional-test-model", "test-run", new(false, "Fictional test result", [new("Q1", 10, 7, "Fictional answer", "Test rubric match", false, "Uploaded memo: credit method", "en", 0.99m)])))
        });
    }
}
