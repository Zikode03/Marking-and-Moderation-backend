using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Data;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;
using Xunit;

namespace ProsperaAssess.Api.Tests;

public class AbpArchitectureTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    [Fact]
    public void Layers_DoNotDependOnTransportOrEntityFramework()
    {
        var domain = typeof(AssessmentRow).Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();
        var application = typeof(AcademicAppService).Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();
        var contracts = typeof(IAcademicAppService).Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();
        var http = typeof(PortalController).Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();
        Assert.DoesNotContain(domain, a => a.Contains("EntityFrameworkCore") || a.Contains("ProsperaAssess.Application") || a.Contains("ProsperaAssess.HttpApi"));
        Assert.DoesNotContain(application, a => a.Contains("EntityFrameworkCore") || a.Contains("ProsperaAssess.HttpApi"));
        Assert.DoesNotContain(contracts, a => a == "ProsperaAssess.Domain" || a == "ProsperaAssess.Application" || a.Contains("EntityFrameworkCore"));
        Assert.DoesNotContain(http, a => a == "ProsperaAssess.Application" || a.Contains("EntityFrameworkCore"));
        Assert.True(typeof(Volo.Abp.Domain.Entities.IAggregateRoot<Guid>).IsAssignableFrom(typeof(AiScript)));
        using var scope = fixture.Factory.Services.CreateScope();
        Assert.Equal("Upload scripts and start AI marking", scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Localization.IStringLocalizer<ProsperaAssessResource>>()["Permission:Upload"].Value);
    }

    private async Task<ClaimsPrincipal> Principal(IServiceProvider services, string role)
    {
        var users = services.GetRequiredService<UserManager<AppUser>>();
        var user = (await users.FindByEmailAsync(role.ToLowerInvariant() + "@tests.example"))!;
        return new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, user.Id), new Claim(ClaimTypes.Name, user.Email!), new Claim(ClaimTypes.Role, role) }, "test-direct-service"));
    }

    [Fact]
    public async Task ApplicationServices_EnforcePermissionsWithoutHttpControllers()
    {
        using var scope = fixture.Factory.Services.CreateScope(); var services = scope.ServiceProvider;
        using var current = services.GetRequiredService<ICurrentPrincipalAccessor>().Change(await Principal(services, "Teacher"));
        var service = services.GetRequiredService<IAcademicAppService>();
        await Assert.ThrowsAsync<AbpAuthorizationException>(() => service.CreateFacultyAsync(new("Denied", "DENIED")));
        Assert.NotNull((await service.GetStructureAsync("school")).Data);
    }

    [Fact]
    public async Task RevokedPermission_IsNotRestoredByStartupSeeding()
    {
        using var scope = fixture.Factory.Services.CreateScope(); var services = scope.ServiceProvider;
        var manager = services.GetRequiredService<IPermissionManager>(); var units = services.GetRequiredService<IUnitOfWorkManager>();
        async Task Grant(bool value) { using var unit = units.Begin(requiresNew: true, isTransactional: true); await manager.SetAsync(ProsperaPermissions.Upload, "R", "Teacher", value); await unit.CompleteAsync(); }
        await Grant(false);
        try
        {
            await services.GetRequiredService<IDataSeeder>().SeedAsync();
            using var current = services.GetRequiredService<ICurrentPrincipalAccessor>().Change(await Principal(services, "Teacher"));
            Assert.False(await services.GetRequiredService<IPermissionChecker>().IsGrantedAsync(ProsperaPermissions.Upload));
            await Assert.ThrowsAsync<AbpAuthorizationException>(() => services.GetRequiredService<IScriptBatchAppService>().UploadAsync(new(new("Blocked", new("Mathematics", 10, 1, "Test", "Public", "DBE-NSC"), null), []), CancellationToken.None));
        }
        finally { await Grant(true); }
    }

    [Fact]
    public void Aggregate_ProtectsApprovedMarksAndRequiresIndependentModeration()
    {
        var criterion = new Criterion(Guid.NewGuid(), "Method", 10);
        var submission = new SubmissionRow(Guid.NewGuid());
        submission.RecordMarks("marker", new() { [criterion.Id] = 7 }, "Checked", [criterion]);
        Assert.Throws<Volo.Abp.BusinessException>(() => submission.Moderate("marker", true, "Self review"));
        submission.Moderate("moderator", true, "Reviewed"); submission.Approve("approver");
        Assert.Throws<Volo.Abp.BusinessException>(() => submission.RecordMarks("marker", new() { [criterion.Id] = 1 }, "Late edit", [criterion]));
        Assert.Equal("Approved", submission.Status);
    }

    [Fact]
    public async Task UnitOfWork_RollsBackIdentityAndBusinessWritesTogether()
    {
        using var scope = fixture.Factory.Services.CreateScope(); var services = scope.ServiceProvider;
        var directory = services.GetRequiredService<IStaffIdentityService>();
        var email = "rollback-" + Guid.NewGuid().ToString("N") + "@tests.example";
        var user = new StaffAccount { Email = email, UserName = email, EmailConfirmed = true, SchoolId = fixture.School };
        using (var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
        {
            Assert.True((await directory.CreateAsync(user, ApiFixture.Password)).Succeeded);
            Assert.True((await directory.AddToRoleAsync(user, "Teacher")).Succeeded);
            await services.GetRequiredService<Volo.Abp.Domain.Repositories.IRepository<StaffAllocationRow, Guid>>().InsertAsync(new StaffAllocationRow(Guid.NewGuid()) { SchoolId = fixture.School, UserId = user.Id, Grade = 10, Subject = "Mathematics" }, autoSave: true);
            await unit.RollbackAsync();
        }
        Assert.Null(await directory.FindByEmailAsync(email));
    }

    [Fact]
    public async Task ApplicationAuditing_RecordsActionWithoutPasswordParameters()
    {
        using var scope = fixture.Factory.Services.CreateScope(); var services = scope.ServiceProvider;
        using var principal = services.GetRequiredService<ICurrentPrincipalAccessor>().Change(await Principal(services, "SchoolAdmin"));
        using var audit = services.GetRequiredService<Volo.Abp.Auditing.IAuditingManager>().BeginScope();
        await services.GetRequiredService<IStaffAppService>().CreateAsync(new("audit-" + Guid.NewGuid().ToString("N") + "@tests.example", ApiFixture.Password, "Teacher", 10, "Mathematics"));
        Assert.Contains(services.GetRequiredService<Volo.Abp.Auditing.IAuditingManager>().Current!.Log.Actions, action => action.ServiceName?.Contains("StaffAppService") == true);
        Assert.All(services.GetRequiredService<Volo.Abp.Auditing.IAuditingManager>().Current!.Log.Actions, action => Assert.DoesNotContain(ApiFixture.Password, action.Parameters ?? ""));
        await audit.SaveAsync();
    }
}
