using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace ProsperaAssess.Api.Tests;

public class AcademicTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private static async Task<JsonElement> Read(HttpResponseMessage response, HttpStatusCode status = HttpStatusCode.OK)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == status, $"Expected {status}, got {response.StatusCode}: {text}");
        return JsonDocument.Parse(text).RootElement.Clone();
    }
    private Task<JsonElement> SchoolAssessment(int grade, string subject) => fixture.SeedAssessment(grade, subject);
    private Task<JsonElement> ModuleAssessment(Guid moduleId) => fixture.SeedAssessment(moduleId: moduleId);

    [Fact]
    public async Task Teacher_OnlySeesAllocatedGradeAndSubject()
    {
        var assigned = await SchoolAssessment(10, "Mathematics");
        var otherGrade = await SchoolAssessment(11, "Mathematics");
        var otherSubject = await SchoolAssessment(10, "English");
        using var teacher = fixture.Client("Teacher");
        var rows = await teacher.GetFromJsonAsync<JsonElement>("/api/assessments");
        Assert.Contains(rows.EnumerateArray(), row => row.GetProperty("id").GetGuid() == assigned.GetProperty("id").GetGuid());
        Assert.DoesNotContain(rows.EnumerateArray(), row => row.GetProperty("id").GetGuid() == otherGrade.GetProperty("id").GetGuid() || row.GetProperty("id").GetGuid() == otherSubject.GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.NotFound, (await teacher.GetAsync("/api/assessments/" + otherGrade.GetProperty("id").GetString())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await teacher.PostAsJsonAsync("/api/submissions", new { assessmentId = otherSubject.GetProperty("id").GetGuid(), studentReference = "Fictional", answerText = "Test" })).StatusCode);
    }

    [Fact]
    public async Task LecturerAndProfessor_AreRestrictedToTheirFacultyModules()
    {
        var business = await ModuleAssessment(fixture.BusinessModule);
        var technology = await ModuleAssessment(fixture.TechnologyModule);
        using var lecturer = fixture.Client("Lecturer");
        using var professor = fixture.Client("Professor");
        Assert.Equal(HttpStatusCode.OK, (await lecturer.GetAsync("/api/assessments/" + business.GetProperty("id").GetString())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await lecturer.GetAsync("/api/assessments/" + technology.GetProperty("id").GetString())).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await professor.GetAsync("/api/assessments/" + technology.GetProperty("id").GetString())).StatusCode);
        var structure = await lecturer.GetFromJsonAsync<JsonElement>("/api/academics?organisationType=institution");
        Assert.Single(structure.GetProperty("faculties").EnumerateArray());
        Assert.Equal(fixture.BusinessFaculty, structure.GetProperty("faculties")[0].GetProperty("id").GetGuid());
        Assert.Single(structure.GetProperty("modules").EnumerateArray());
        Assert.Empty(structure.GetProperty("staff").EnumerateArray());
    }

    [Theory]
    [InlineData("Lecturer")]
    [InlineData("Professor")]
    public async Task AllocatedInstitutionStaff_CanMarkTheirModule(string role)
    {
        var assessment = await ModuleAssessment(role == "Lecturer" ? fixture.BusinessModule : fixture.TechnologyModule);
        using var staff = fixture.Client(role);
        var submission = await Read(await staff.PostAsJsonAsync("/api/submissions", new { assessmentId = assessment.GetProperty("id").GetGuid(), studentReference = "STUDENT-TEST", answerText = "Fictional answer" }), HttpStatusCode.Created);
        var marks = new Dictionary<string, int> { [assessment.GetProperty("criteria")[0].GetProperty("id").GetString()!] = 8 };
        var marked = await Read(await staff.PostAsJsonAsync("/api/submissions/" + submission.GetProperty("id").GetString() + "/mark", new { marks, feedback = "Test feedback" }));
        Assert.Equal("Marked", marked.GetProperty("status").GetString());
    }

    [Fact]
    public async Task AssessmentCreation_IsDisabled()
    {
        using var admin = fixture.Client("SchoolAdmin");
        Assert.Equal(HttpStatusCode.Gone, (await admin.PostAsJsonAsync("/api/assessments", new { title = "Disabled" })).StatusCode);
    }

    [Fact]
    public async Task AllocationManagement_EnforcesRoleAndTenantBoundaries()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var teacher = await users.FindByEmailAsync("teacher@tests.example");
        var other = await users.FindByEmailAsync("otherschool@tests.example");
        using var admin = fixture.Client("SchoolAdmin");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/academics/allocations", new { userId = teacher!.Id, moduleId = fixture.BusinessModule })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync("/api/academics/allocations", new { userId = other!.Id, grade = 12, subject = "Mathematics" })).StatusCode);
        using var teacherClient = fixture.Client("Teacher");
        Assert.Equal(HttpStatusCode.Forbidden, (await teacherClient.PostAsJsonAsync("/api/academics/faculties", new { name = "Not allowed", code = "DENIED" })).StatusCode);
        var allocation = await Read(await admin.PostAsJsonAsync("/api/academics/allocations", new { userId = teacher.Id, grade = 12, subject = "Mathematics" }));
        Assert.Equal(12, allocation.GetProperty("grade").GetInt32());
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/academics/allocations", new { userId = teacher.Id, grade = 12, subject = "Mathematics" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync("/api/academics/allocations/" + allocation.GetProperty("id").GetString())).StatusCode);
    }

    [Fact]
    public async Task FacultyModuleManagement_ValidatesOwnershipAndDuplicates()
    {
        using var admin = fixture.Client("SchoolAdmin");
        var code = "F" + Guid.NewGuid().ToString("N");
        var faculty = await Read(await admin.PostAsJsonAsync("/api/academics/faculties", new { name = "New faculty", code }));
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/academics/faculties", new { name = "Duplicate", code })).StatusCode);
        var module = await Read(await admin.PostAsJsonAsync("/api/academics/modules", new { facultyId = faculty.GetProperty("id").GetGuid(), name = "New module", code = "M" + Guid.NewGuid().ToString("N") }));
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync("/api/academics/modules/" + module.GetProperty("id").GetString(), new { facultyId = faculty.GetProperty("id").GetGuid(), name = "Updated module", code = module.GetProperty("code").GetString() })).StatusCode);
        using var other = fixture.Client("OtherSchool");
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync("/api/assessments/" + (await ModuleAssessment(module.GetProperty("id").GetGuid())).GetProperty("id").GetString())).StatusCode);
    }

    [Fact]
    public async Task BaselineMigration_PreservesLegacyDataAndCanRunTwice()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var existing = scope.ServiceProvider.GetRequiredService<AssessDb>().Database.GetConnectionString();
        var connection = new SqlConnectionStringBuilder(existing) { InitialCatalog = "ProsperaAssess_Test_Upgrade_" + Guid.NewGuid().ToString("N") };
        var master = new SqlConnectionStringBuilder(connection.ConnectionString) { InitialCatalog = "master" };
        await using var sql = new SqlConnection(master.ConnectionString); await sql.OpenAsync();
        await new SqlCommand("CREATE DATABASE [" + connection.InitialCatalog + "]", sql).ExecuteNonQueryAsync();
        await using var db = new AssessDb(new DbContextOptionsBuilder<AssessDb>().UseSqlServer(connection.ConnectionString).Options);
        try
        {
            using var resource = typeof(AcademicTests).Assembly.GetManifestResourceStream("ProsperaAssess.Api.Tests.LegacySchema.sql")!;
            var legacy = await new System.IO.StreamReader(resource).ReadToEndAsync();
            await db.Database.ExecuteSqlRawAsync(legacy);
            var id = Guid.NewGuid();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.Assessments (Id,SchoolId,Title,Code,CriteriaJson) VALUES ({id},{fixture.School},{"Preserved assessment"},{"LEGACY"},{"[]"})");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.AspNetUsers (Id,SchoolId,UserName,EmailConfirmed,PasswordHash,PhoneNumberConfirmed,TwoFactorEnabled,LockoutEnabled,AccessFailedCount) VALUES ({"preserved-user"},{fixture.School},{"legacy-user"},{true},{"preserved-hash"},{false},{false},{false},{0})");
            await db.Database.ExecuteSqlRawAsync("DROP TABLE dbo.StaffAllocations; DROP TABLE dbo.Modules; DROP TABLE dbo.Faculties; ALTER TABLE dbo.Assessments DROP COLUMN InstitutionJson; ALTER TABLE dbo.AiBatches DROP COLUMN ScriptLanguage; ALTER TABLE dbo.AiBatches DROP COLUMN MemoLanguage;");
            await db.Database.MigrateAsync(); await db.Database.MigrateAsync();
            await using var verify = new SqlConnection(connection.ConnectionString); await verify.OpenAsync();
            Assert.Equal("Preserved assessment", await new SqlCommand("SELECT Title FROM dbo.Assessments WHERE Code='LEGACY'", verify).ExecuteScalarAsync());
            Assert.Equal("preserved-hash", await new SqlCommand("SELECT PasswordHash FROM dbo.AspNetUsers WHERE Id='preserved-user'", verify).ExecuteScalarAsync());
            Assert.NotEmpty(await db.Database.GetAppliedMigrationsAsync());
        }
        finally { Assert.StartsWith("ProsperaAssess_Test_Upgrade_", connection.InitialCatalog); await db.Database.EnsureDeletedAsync(); }
    }

    [Fact]
    public async Task NewTeachingAccounts_RequireTheirInitialAllocation()
    {
        using var admin = fixture.Client("SchoolAdmin");
        var email = Guid.NewGuid().ToString("N") + "@tests.example";
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/bootstrap/staff", new { email, password = ApiFixture.Password, role = "Teacher" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/bootstrap/staff", new { email, password = ApiFixture.Password, role = "Lecturer" })).StatusCode);
        await Read(await admin.PostAsJsonAsync("/api/bootstrap/staff", new { email, password = ApiFixture.Password, role = "Professor", moduleId = fixture.BusinessModule }), HttpStatusCode.Created);
        var structure = await admin.GetFromJsonAsync<JsonElement>("/api/academics?organisationType=institution");
        var member = structure.GetProperty("staff").EnumerateArray().Single(u => u.GetProperty("email").GetString() == email);
        var allocation = structure.GetProperty("allocations").EnumerateArray().Single(a => a.GetProperty("userId").GetString() == member.GetProperty("id").GetString());
        Assert.Equal(fixture.BusinessModule, allocation.GetProperty("moduleId").GetGuid());
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.DeleteAsync("/api/academics/allocations/" + allocation.GetProperty("id").GetString())).StatusCode);
    }
}
