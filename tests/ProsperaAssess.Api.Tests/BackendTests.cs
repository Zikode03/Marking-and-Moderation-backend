using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using Xunit;

namespace ProsperaAssess.Api.Tests;

public class BackendTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private static async Task<JsonElement> Body(HttpResponseMessage response, HttpStatusCode expected)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected, $"Expected {expected}, got {response.StatusCode}: {text}");
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    private Task<JsonElement> Assessment() => fixture.SeedAssessment();

    private async Task<(JsonElement Assessment, JsonElement Submission)> Submission()
    {
        var assessment = await Assessment();
        using var teacher = fixture.Client("Teacher");
        var submission = await Body(await teacher.PostAsJsonAsync("/api/submissions", new
        {
            assessmentId = assessment.GetProperty("id").GetGuid(),
            studentReference = "TEST-001",
            answerText = "Fictional test answer"
        }), HttpStatusCode.Created);
        return (assessment, submission);
    }

    private static object Marks(JsonElement assessment, int score = 7) => new
    {
        marker = "Spoofed name must be ignored",
        marks = new Dictionary<string, int> { [assessment.GetProperty("criteria")[0].GetProperty("id").GetString()!] = score },
        feedback = "Reviewed feedback"
    };

    [Fact]
    public async Task CompleteWorkflow_PreservesEvidenceAndLocksResults()
    {
        var (assessment, submission) = await Submission();
        var path = "/api/submissions/" + submission.GetProperty("id").GetString();
        using var teacher = fixture.Client("Teacher");
        using var hod = fixture.Client("HOD");
        using var principal = fixture.Client("Principal");
        Assert.Equal(HttpStatusCode.Conflict, (await principal.PostAsJsonAsync(path + "/approve", new { })).StatusCode);
        var marked = await Body(await teacher.PostAsJsonAsync(path + "/mark", Marks(assessment)), HttpStatusCode.OK);
        Assert.Equal("Marked", marked.GetProperty("status").GetString());
        Assert.Equal(7, marked.GetProperty("total").GetDecimal());
        var audit = marked.GetProperty("audit")[1];
        Assert.NotEqual("Spoofed name must be ignored", audit.GetProperty("actor").GetString());
        Assert.Equal(7, audit.GetProperty("marks").EnumerateObject().Single().Value.GetInt32());
        var moderated = await Body(await hod.PostAsJsonAsync(path + "/moderate", new { approve = true, comments = "Evidence reviewed" }), HttpStatusCode.OK);
        Assert.Equal("Moderated", moderated.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await teacher.PostAsJsonAsync(path + "/mark", Marks(assessment))).StatusCode);
        var approved = await Body(await principal.PostAsJsonAsync(path + "/approve", new { }), HttpStatusCode.OK);
        Assert.Equal("Approved", approved.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await teacher.PostAsJsonAsync(path + "/mark", Marks(assessment))).StatusCode);
    }

    [Fact]
    public async Task ReturnForRemarking_RequiresFreshModeration()
    {
        var (assessment, submission) = await Submission();
        var path = "/api/submissions/" + submission.GetProperty("id").GetString();
        using var teacher = fixture.Client("Teacher");
        using var hod = fixture.Client("HOD");
        await Body(await teacher.PostAsJsonAsync(path + "/mark", Marks(assessment)), HttpStatusCode.OK);
        var returned = await Body(await hod.PostAsJsonAsync(path + "/moderate", new { approve = false, comments = "Please review" }), HttpStatusCode.OK);
        Assert.Equal("Submitted", returned.GetProperty("status").GetString());
        await Body(await teacher.PostAsJsonAsync(path + "/mark", Marks(assessment, 8)), HttpStatusCode.OK);
    }

    [Fact]
    public async Task AcademicAdmin_CannotModerateOwnMarking()
    {
        var (assessment, submission) = await Submission();
        var path = "/api/submissions/" + submission.GetProperty("id").GetString();
        using var academic = fixture.Client("AcademicAdmin");
        await Body(await academic.PostAsJsonAsync(path + "/mark", Marks(assessment)), HttpStatusCode.OK);
        Assert.Equal(HttpStatusCode.Conflict, (await academic.PostAsJsonAsync(path + "/moderate", new { approve = true, comments = "Self review" })).StatusCode);
    }

    [Theory]
    [InlineData("{\"title\":\"Test\",\"code\":\"T\",\"criteria\":[null]}")]
    [InlineData("{\"title\":\"Test\",\"code\":\"T\",\"criteria\":null}")]
    [InlineData("{\"title\":null,\"code\":\"T\",\"criteria\":[]}")]
    [InlineData("{\"title\":\"Test\",\"code\":\"T\",\"criteria\":[{\"name\":\"X\",\"maxMarks\":0}]}")]
    [InlineData("{bad-json")]
    [InlineData("null")]
    public async Task InvalidAssessments_Return400(string json)
    {
        using var client = fixture.Client("SchoolAdmin");
        Assert.Equal(HttpStatusCode.Gone, (await client.PostAsync("/api/assessments", new StringContent(json, Encoding.UTF8, "application/json"))).StatusCode);
    }

    [Fact]
    public async Task Authorization_EnforcesRolesAndNoPublicRegistration()
    {
        using var anonymous = fixture.Client();
        using var teacher = fixture.Client("Teacher");
        using var noRole = fixture.Client("NoRole");
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/assessments")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await noRole.GetAsync("/api/assessments")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.PostAsJsonAsync("/api/bootstrap/staff", new { email = "fake@tests.example", password = ApiFixture.Password, role = "Principal" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.PostAsJsonAsync("/register", new { })).StatusCode);
    }

    [Fact]
    public async Task OtherSchool_CannotReadOrModifyRecords()
    {
        var (assessment, submission) = await Submission();
        using var other = fixture.Client("OtherSchool");
        var path = "/api/submissions/" + submission.GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync(path + "/mark", Marks(assessment))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync(path + "/ai-suggestion", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync("/api/submissions", new { assessmentId = assessment.GetProperty("id").GetGuid(), studentReference = "X", answerText = "X" })).StatusCode);
        var rows = await other.GetFromJsonAsync<JsonElement>("/api/submissions");
        Assert.Empty(rows.EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync("/api/assessments/" + assessment.GetProperty("id").GetString())).StatusCode);
    }

    [Fact]
    public async Task RefreshTokens_RenewAndRejectInvalidTokens()
    {
        using var client = fixture.Client();
        var login = await Body(await client.PostAsJsonAsync("/login", new { email = "teacher@tests.example", password = ApiFixture.Password }), HttpStatusCode.OK);
        var refreshed = await Body(await client.PostAsJsonAsync("/refresh", new { refreshToken = login.GetProperty("refreshToken").GetString() }), HttpStatusCode.OK);
        Assert.False(string.IsNullOrWhiteSpace(refreshed.GetProperty("accessToken").GetString()));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/refresh", new { refreshToken = "invalid" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/login", new { email = (string?)null, password = (string?)null })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/login", new { email = "twofactor@tests.example", password = ApiFixture.Password })).StatusCode);
    }

    [Fact]
    public async Task InvalidMarksAndMissingModerationDecision_Return400()
    {
        var (assessment, submission) = await Submission();
        var path = "/api/submissions/" + submission.GetProperty("id").GetString();
        using var teacher = fixture.Client("Teacher");
        using var hod = fixture.Client("HOD");
        Assert.Equal(HttpStatusCode.BadRequest, (await teacher.PostAsJsonAsync(path + "/mark", Marks(assessment, 11))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await teacher.PostAsJsonAsync(path + "/mark", new { marks = (object?)null })).StatusCode);
        await Body(await teacher.PostAsJsonAsync(path + "/mark", Marks(assessment)), HttpStatusCode.OK);
        Assert.Equal(HttpStatusCode.BadRequest, (await hod.PostAsJsonAsync(path + "/moderate", new { comments = "Missing decision" })).StatusCode);
    }

    [Fact]
    public async Task StaleRevision_Returns409()
    {
        var (assessment, submission) = await Submission();
        var path = "/api/submissions/" + submission.GetProperty("id").GetString();
        using var teacher = fixture.Client("Teacher");
        await Body(await teacher.PostAsJsonAsync(path + "/mark", Marks(assessment)), HttpStatusCode.OK);
        using var hod = fixture.Client("HOD");
        Assert.Equal(HttpStatusCode.Conflict, (await hod.PostAsJsonAsync(path + "/moderate", new { approve = true, comments = "Stale screen", revision = submission.GetProperty("revision").GetInt32() })).StatusCode);
    }

    [Fact]
    public async Task ConcurrentSaves_DoNotOverwriteAuditHistory()
    {
        var (_, submission) = await Submission();
        var id = submission.GetProperty("id").GetGuid();
        using var firstScope = fixture.Factory.Services.CreateScope();
        using var secondScope = fixture.Factory.Services.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<AssessDb>();
        var second = secondScope.ServiceProvider.GetRequiredService<AssessDb>();
        var firstRow = await first.Submissions.SingleAsync(s => s.Id == id);
        var secondRow = await second.Submissions.SingleAsync(s => s.Id == id);
        firstRow.AddAudit("test-1", "Test", "First save");
        secondRow.AddAudit("test-2", "Test", "Conflicting save");
        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<Volo.Abp.Data.AbpDbConcurrencyException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task ProvisionStaff_InheritsSchoolAndRejectsDuplicates()
    {
        using var admin = fixture.Client("SchoolAdmin");
        var email = Guid.NewGuid().ToString("N") + "@tests.example";
        var payload = new { email, password = ApiFixture.Password, role = "Teacher", schoolId = Guid.NewGuid(), grade = 10, subject = "Mathematics" };
        var created = await Body(await admin.PostAsJsonAsync("/api/bootstrap/staff", payload), HttpStatusCode.Created);
        Assert.Equal(fixture.School, created.GetProperty("schoolId").GetGuid());
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/bootstrap/staff", payload)).StatusCode);
    }

    [Fact]
    public async Task ExpiredOrRevokedRefreshToken_IsRejected()
    {
        using var client = fixture.Client();
        using var scope = fixture.Factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<BearerTokenOptions>>().Get(IdentityConstants.BearerScheme);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity("test")),
            new AuthenticationProperties { ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(-1) }, IdentityConstants.BearerScheme);
        var expired = options.RefreshTokenProtector.Protect(ticket);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/refresh", new { refreshToken = expired })).StatusCode);

        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var email = Guid.NewGuid().ToString("N") + "@tests.example";
        var user = new AppUser { Email = email, UserName = email, EmailConfirmed = true, SchoolId = fixture.School };
        Assert.True((await users.CreateAsync(user, ApiFixture.Password)).Succeeded);
        var login = await Body(await client.PostAsJsonAsync("/login", new { email, password = ApiFixture.Password }), HttpStatusCode.OK);
        Assert.True((await users.UpdateSecurityStampAsync(user)).Succeeded);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/refresh", new { refreshToken = login.GetProperty("refreshToken").GetString() })).StatusCode);
    }

    [Fact]
    public async Task CreatedResources_CanBeReadAtTheirLocations()
    {
        var (assessment, submission) = await Submission();
        using var client = fixture.Client("Teacher");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/assessments/" + assessment.GetProperty("id").GetString())).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/submissions/" + submission.GetProperty("id").GetString())).StatusCode);
        var health = await client.GetFromJsonAsync<JsonElement>("/api/health");
        Assert.Equal("sql-server", health.GetProperty("persistence").GetString());
    }
}
