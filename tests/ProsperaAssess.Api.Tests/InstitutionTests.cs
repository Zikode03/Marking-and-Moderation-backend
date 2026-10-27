using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;
namespace ProsperaAssess.Api.Tests;

public class InstitutionTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private static async Task<JsonElement> Record(HttpClient client, string kind, string code, Dictionary<string, string> fields)
    {
        var response = await client.PostAsJsonAsync("/api/institution/records/" + kind, new { code, name = code, fields });
        var text = await response.Content.ReadAsStringAsync(); Assert.True(response.StatusCode == HttpStatusCode.OK, text); return JsonDocument.Parse(text).RootElement.Clone();
    }
    [Fact]
    public async Task DedicatedModeratorCanReviewDocumentsButCannotManageStaff()
    {
        using var moderator = fixture.Client("Moderator");
        Assert.Equal(HttpStatusCode.OK, (await moderator.GetAsync("/api/institution/documents")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await moderator.GetAsync("/api/institution/tasks")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await moderator.PostAsJsonAsync("/api/institution/invitations", new { email = "denied@tests.example", role = "HOD" })).StatusCode);
    }
    [Fact]
    public async Task RegistryEnforcesReferencesRevisionsAndInstitutionBoundaries()
    {
        using var admin = fixture.Client("SchoolAdmin"); using var teacher = fixture.Client("Teacher"); using var other = fixture.Client("OtherSchool");
        var suffix = Guid.NewGuid().ToString("N");
        var branch = await Record(admin, "branch", "BR" + suffix, new());
        var learner = await Record(admin, "learner", "C" + suffix, new() { ["branchId"] = branch.GetProperty("id").GetString()! });
        var cohort = await Record(admin, "class", "CL" + suffix, new() { ["grade"] = "10", ["academicYear"] = "2026" });
        await Record(admin, "enrolment", "EN" + suffix, new() { ["learnerId"] = learner.GetProperty("id").GetString()!, ["classId"] = cohort.GetProperty("id").GetString()!, ["academicYear"] = "2026" });
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.GetAsync("/api/institution/records/learner")).StatusCode);
        var outsiders = await other.GetFromJsonAsync<JsonElement>("/api/institution/records/branch"); Assert.Equal(0, outsiders.GetArrayLength());
        var path = "/api/institution/records/branch/" + branch.GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync(path, new { code = branch.GetProperty("code").GetString(), name = "Late edit", fields = new { }, revision = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync(path, new { code = branch.GetProperty("code").GetString(), name = "Archive", fields = new { }, revision = 1, active = false })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/institution/records/enrolment", new { code = "BAD" + suffix, name = "Invalid", fields = new { learnerId = Guid.NewGuid(), classId = cohort.GetProperty("id").GetString(), academicYear = "2026" } })).StatusCode);
    }
    [Fact]
    public async Task BulkImportRollsBackEveryRowWhenAnyRowIsInvalid()
    {
        using var admin = fixture.Client("SchoolAdmin"); var code = "IMPORT" + Guid.NewGuid().ToString("N");
        var response = await admin.PostAsJsonAsync("/api/institution/import/branch", new[] { new { code, name = "Valid", fields = new { } }, new { code = code + "2", name = "", fields = new { } } });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var records = await admin.GetFromJsonAsync<JsonElement>("/api/institution/records/branch"); Assert.DoesNotContain(records.EnumerateArray(), r => r.GetProperty("code").GetString() == code);
    }
    [Fact]
    public async Task StaffAccessCannotBeSelfChangedAndDisablingRevokesExistingBearerAccess()
    {
        using var admin = fixture.Client("SchoolAdmin"); var email = "member-" + Guid.NewGuid().ToString("N") + "@tests.example";
        var created = await admin.PostAsJsonAsync("/api/bootstrap/staff", new { email, password = ApiFixture.Password, role = "HOD" }); Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var roster = await admin.GetFromJsonAsync<JsonElement>("/api/institution/staff"); var id = roster.EnumerateArray().First(r => r.GetProperty("email").GetString() == email).GetProperty("id").GetString();
        using var member = fixture.Client(); var login = await member.PostAsJsonAsync("/login", new { email, password = ApiFixture.Password }); Assert.Equal(HttpStatusCode.OK, login.StatusCode); member.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.OK, (await member.GetAsync("/api/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync("/api/institution/staff/" + id, new { active = false, roles = new[] { "HOD" } })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await member.GetAsync("/api/me")).StatusCode);
        var self = roster.EnumerateArray().First(r => r.GetProperty("email").GetString() == "schooladmin@tests.example").GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/api/institution/staff/" + self, new { active = false, roles = new[] { "Teacher" } })).StatusCode);
    }
    [Fact]
    public async Task InvitationsAreSingleUseAndAssignOnlyTheInvitedInstitution()
    {
        using var admin = fixture.Client("SchoolAdmin"); using var guest = fixture.Client();
        var email = "invite-" + Guid.NewGuid().ToString("N") + "@tests.example";
        var response = await admin.PostAsJsonAsync("/api/institution/invitations", new { email, role = "Teacher", grade = 10, subject = "Mathematics" }); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var path = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("invitationPath").GetString()!; var token = path.Split("token=")[1];
        Assert.Equal(HttpStatusCode.BadRequest, (await guest.PostAsJsonAsync("/api/invitations/accept", new { token = "invalid", password = ApiFixture.Password })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await guest.PostAsJsonAsync("/api/invitations/accept", new { token, password = ApiFixture.Password })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await guest.PostAsJsonAsync("/api/invitations/accept", new { token, password = ApiFixture.Password })).StatusCode);
        var login = await guest.PostAsJsonAsync("/login", new { email, password = ApiFixture.Password }); Assert.Equal(HttpStatusCode.OK, login.StatusCode); guest.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString());
        var profile = await guest.GetFromJsonAsync<JsonElement>("/api/me"); Assert.Equal(fixture.School, profile.GetProperty("schoolId").GetGuid()); Assert.Contains(profile.GetProperty("roles").EnumerateArray(), r => r.GetString() == "Teacher");
    }
    [Fact]
    public async Task EnrolmentAndMemoPoliciesAreEnforcedByTheApi()
    {
        using var admin = fixture.Client("SchoolAdmin"); using var teacher = fixture.Client("Teacher"); using var hod = fixture.Client("HOD");
        var policyFields = new Dictionary<string, string> { ["language"] = "English", ["retentionDays"] = "365", ["requireEnrolment"] = "true", ["requireMemoApproval"] = "true", ["markingPolicy"] = "Review all marks", ["moderationSamplePercent"] = "100" };
        var policy = await Record(admin, "settings", "CURRENT", policyFields);
        try
        {
            using (var missing = Files()) Assert.Equal(HttpStatusCode.BadRequest, (await teacher.PostAsync("/api/ai/batches", missing)).StatusCode);
            var learner = await Record(admin, "learner", "C001", new());
            var cohort = await Record(admin, "class", "POLICY" + Guid.NewGuid().ToString("N"), new() { ["grade"] = "10", ["academicYear"] = DateTime.UtcNow.Year.ToString() });
            await Record(admin, "enrolment", "POLICY" + Guid.NewGuid().ToString("N"), new() { ["learnerId"] = learner.GetProperty("id").GetString()!, ["classId"] = cohort.GetProperty("id").GetString()!, ["academicYear"] = DateTime.UtcNow.Year.ToString() });
            using var valid = Files(); var upload = await teacher.PostAsync("/api/ai/batches", valid); Assert.Equal(HttpStatusCode.Created, upload.StatusCode); var batchId = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            Assert.Equal(HttpStatusCode.Conflict, (await teacher.PostAsync($"/api/ai/batches/{batchId}/start", null)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await hod.PostAsync($"/api/institution/documents/{batchId}/approve", null)).StatusCode);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await teacher.PostAsync($"/api/ai/batches/{batchId}/start", null)).StatusCode);
        }
        finally
        {
            policyFields["requireEnrolment"] = "false"; policyFields["requireMemoApproval"] = "false";
            Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync("/api/institution/records/settings/" + policy.GetProperty("id").GetString(), new { code = "CURRENT", name = "Policies", fields = policyFields, revision = 1 })).StatusCode);
        }
    }
    private static MultipartFormDataContent Files(Guid? previous = null)
    {
        var nonce = Guid.NewGuid().ToString("N"); var files = new MultipartFormDataContent(); files.Add(new StringContent(AppJson.Write(new { title = "Documents " + nonce, previousBatchId = previous, school = new { grade = 10, subject = "Mathematics", term = 1, assessmentType = "Test", schoolType = "Public", pathway = "DBE-NSC" } })), "context");
        foreach (var name in new[] { "paper", "memo", "scripts" }) files.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("%PDF-1.4 " + name + nonce)), name, name == "scripts" ? "C001.pdf" : name + ".pdf"); return files;
    }
    [Fact]
    public async Task DocumentsRetainVersionsAndRequireIndependentApprovalAndQualifiedAssignments()
    {
        using var teacher = fixture.Client("Teacher"); using var admin = fixture.Client("SchoolAdmin"); using var hod = fixture.Client("HOD"); using var other = fixture.Client("OtherSchool");
        using var files = Files(); var upload = await teacher.PostAsync("/api/ai/batches", files); Assert.Equal(HttpStatusCode.Created, upload.StatusCode); var id = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var detail = await teacher.GetFromJsonAsync<JsonElement>($"/api/ai/batches/{id}"); var script = detail.GetProperty("scripts")[0].GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.PostAsync($"/api/institution/documents/{id}/approve", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await hod.PostAsync($"/api/institution/documents/{id}/approve", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await hod.PostAsync($"/api/institution/documents/{id}/approve", null)).StatusCode);
        using var revisionFiles = Files(id); var revision = await teacher.PostAsync("/api/ai/batches", revisionFiles); Assert.Equal(HttpStatusCode.Created, revision.StatusCode);
        var documents = await teacher.GetFromJsonAsync<JsonElement>("/api/institution/documents"); Assert.Contains(documents.EnumerateArray(), r => r.GetProperty("previousBatchId").GetString() == id.ToString());
        Assert.Equal(0, (await other.GetFromJsonAsync<JsonElement>("/api/institution/documents")).GetArrayLength());
        var roster = await hod.GetFromJsonAsync<JsonElement>("/api/institution/staff"); var teacherId = roster.EnumerateArray().First(r => r.GetProperty("email").GetString() == "teacher@tests.example").GetProperty("id").GetString(); var hodId = roster.EnumerateArray().First(r => r.GetProperty("email").GetString() == "hod@tests.example").GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.BadRequest, (await hod.PutAsJsonAsync($"/api/institution/assignments/{script}", new { teacherId, moderatorId = teacherId })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await hod.PutAsJsonAsync($"/api/institution/assignments/{script}", new { teacherId, moderatorId = hodId, dueAt = DateTimeOffset.UtcNow.AddDays(2) })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await hod.PostAsync("/api/institution/samples/25", null)).StatusCode);
        var audit = await admin.GetFromJsonAsync<JsonElement>("/api/institution/audit"); Assert.Contains(audit.GetProperty("events").EnumerateArray(), e => e.GetProperty("action").GetString() == "moderation.sample.recorded");
        Assert.Equal(0, (await teacher.GetFromJsonAsync<JsonElement>("/api/institution/results")).GetArrayLength());
    }
}
