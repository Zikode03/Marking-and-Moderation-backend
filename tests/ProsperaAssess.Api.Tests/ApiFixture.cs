using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ProsperaAssess.Api.Tests;

public sealed class ApiFixture : IAsyncLifetime
{
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;
    public Guid School { get; } = Guid.NewGuid();
    public Guid BusinessFaculty { get; } = Guid.NewGuid();
    public Guid TechnologyFaculty { get; } = Guid.NewGuid();
    public Guid BusinessModule { get; } = Guid.NewGuid();
    public Guid TechnologyModule { get; } = Guid.NewGuid();
    public const string Password = "Tests!Only9-NotARealAccount";
    private readonly Dictionary<string, string> tokens = new();
    private readonly Dictionary<string, string?> previousEnvironment = new();
    private string connection = "";

    public async Task InitializeAsync()
    {
        var supplied = Environment.GetEnvironmentVariable("PROSPERA_TEST_SQL")
            ?? throw new InvalidOperationException("Set PROSPERA_TEST_SQL to a local SQL Server connection string. Tests create and drop only their own uniquely named ProsperaAssess_Test_* database.");
        var sql = new SqlConnectionStringBuilder(supplied) { InitialCatalog = "ProsperaAssess_Test_" + Guid.NewGuid().ToString("N") };
        connection = sql.ConnectionString;
        SetEnvironment("ConnectionStrings__Default", connection);
        SetEnvironment("Bootstrap__Email", "");
        SetEnvironment("Bootstrap__Password", "");
        SetEnvironment("Bootstrap__SchoolId", "");
        SetEnvironment("Database__Initialize", "true");
        Factory = new WebApplicationFactory<Program>();
        using var client = Factory.CreateClient();
        using var scope = Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var db = scope.ServiceProvider.GetRequiredService<AssessDb>();
        db.Faculties.AddRange(new FacultyRow(BusinessFaculty) { SchoolId = School, Code = "BUS", Name = "Business" }, new FacultyRow(TechnologyFaculty) { SchoolId = School, Code = "TECH", Name = "Technology" });
        db.Modules.AddRange(new ModuleRow(BusinessModule) { SchoolId = School, FacultyId = BusinessFaculty, Code = "BMAN101", Name = "Business Management" }, new ModuleRow(TechnologyModule) { SchoolId = School, FacultyId = TechnologyFaculty, Code = "ICT101", Name = "Computing" });
        await db.SaveChangesAsync();
        foreach (var role in new[] { "SchoolAdmin", "Teacher", "Lecturer", "Professor", "HOD", "Moderator", "Principal", "AcademicAdmin", "OtherSchool", "NoRole", "TwoFactor" })
        {
            var email = role.ToLowerInvariant() + "@tests.example";
            var user = new AppUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                SchoolId = role == "OtherSchool" ? Guid.NewGuid() : School
            };
            Assert.True((await users.CreateAsync(user, Password)).Succeeded);
            if (role != "NoRole")
                Assert.True((await users.AddToRoleAsync(user, role is "OtherSchool" or "TwoFactor" ? "Teacher" : role)).Succeeded);
            if (role == "TwoFactor")
            {
                Assert.True((await users.ResetAuthenticatorKeyAsync(user)).Succeeded);
                Assert.True((await users.SetTwoFactorEnabledAsync(user, true)).Succeeded);
                continue;
            }
            if (role == "Teacher") db.StaffAllocations.Add(new StaffAllocationRow(Guid.NewGuid()) { SchoolId = School, UserId = user.Id, Grade = 10, Subject = "Mathematics" });
            if (role is "Lecturer" or "Professor") db.StaffAllocations.Add(new StaffAllocationRow(Guid.NewGuid()) { SchoolId = School, UserId = user.Id, ModuleId = role == "Lecturer" ? BusinessModule : TechnologyModule });
            await db.SaveChangesAsync();
            using var login = await client.PostAsJsonAsync("/login", new { email, password = Password });
            login.EnsureSuccessStatusCode();
            tokens[role] = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
        }
    }

    private void SetEnvironment(string name, string? value)
    {
        previousEnvironment[name] = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
    }

    public HttpClient Client(string? role = null)
    {
        var client = Factory.CreateClient();
        if (role is not null) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens[role]);
        return client;
    }

    public async Task<JsonElement> SeedAssessment(int grade = 10, string subject = "Mathematics", Guid? moduleId = null)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AssessDb>();
        var row = new AssessmentRow(Guid.NewGuid()) { SchoolId = School, Title = "Historical test fixture", Code = Guid.NewGuid().ToString("N"), CriteriaJson = AppJson.Write(new[] { new Criterion(Guid.NewGuid(), "Knowledge", 10) }), SchoolJson = moduleId is null ? AppJson.Write(new SchoolContext(subject, grade, 1, "Test", "Public", "DBE-NSC")) : null };
        if (moduleId is not null)
        {
            var module = await db.Modules.FirstAsync(m => m.Id == moduleId); var faculty = await db.Faculties.FirstAsync(f => f.Id == module.FacultyId);
            row.InstitutionJson = AppJson.Write(new InstitutionContext(faculty.Id, faculty.Name, module.Id, module.Code, module.Name, 2026, 1, "Test", 0));
        }
        db.Assessments.Add(row); await db.SaveChangesAsync();
        return JsonSerializer.SerializeToElement(row.ToView(), AppJson.Options);
    }

    public async Task DisposeAsync()
    {
        try
        {
            if (Factory is not null)
            {
                // Never delete a database supplied by the caller.
                if (!new SqlConnectionStringBuilder(connection).InitialCatalog.StartsWith("ProsperaAssess_Test_", StringComparison.Ordinal))
                    throw new InvalidOperationException("Refusing to delete a non-test database.");
                // Stop background queue polling before dropping its disposable database.
                await Factory.DisposeAsync();
                await using var cleanupDb = new AssessDb(new DbContextOptionsBuilder<AssessDb>().UseSqlServer(connection).Options);
                await cleanupDb.Database.EnsureDeletedAsync();
            }
        }
        finally
        {
            foreach (var (name, value) in previousEnvironment) Environment.SetEnvironmentVariable(name, value);
        }
    }
}
