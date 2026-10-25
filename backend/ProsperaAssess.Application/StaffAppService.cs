using System.Security.Claims;
using System.Text;
using System.Security.Cryptography;
using System.Text.Json;
namespace ProsperaAssess;

public class StaffAppService : ProsperaAppService, IStaffAppService
{
    [Microsoft.AspNetCore.Authorization.Authorize(ProsperaPermissions.Workspace)]
    public virtual async Task<OperationResult> GetProfileAsync()
    {
        var principal = Principal;

        var user = await users.GetUserAsync(principal);
        return user is null ? Results.Unauthorized() : Results.Ok(new { user.Email, user.SchoolId, roles = await users.GetRolesAsync(user) });

    }
    [Microsoft.AspNetCore.Authorization.Authorize(ProsperaPermissions.CreateStaff)]
    [Volo.Abp.Uow.UnitOfWork(true)]
    public virtual async Task<OperationResult> CreateAsync(CreateStaffRequest request)
    {
        var principal = Principal;

        var admin = await users.GetUserAsync(principal);
        if (admin is null || admin.SchoolId == Guid.Empty) return Results.Unauthorized();
        if (!InputRules.Email(request.Email) || !InputRules.Text(request.Password, 1024) || request.Role is not ("Teacher" or "Lecturer" or "Professor" or "HOD" or "Principal" or "AcademicAdmin"))
            return Results.BadRequest(new { error = "A valid email, password and school role are required." });
        var email = request.Email!.Trim();
        if (await users.FindByEmailAsync(email) is not null) return Results.Conflict(new { error = "Account already exists." });
        if (request.Role == "Teacher" && (request.Grade is not (>= 8 and <= 12) || !InputRules.Text(request.Subject, 200) || request.ModuleId is not null))
            return Results.BadRequest(new { error = "A teacher account requires an initial Grade 8–12 and subject." });
        if (request.Role is "Lecturer" or "Professor")
        {
            if (request.ModuleId is null || request.Grade is not null || !string.IsNullOrWhiteSpace(request.Subject))
                return Results.BadRequest(new { error = "A lecturer or professor requires an initial faculty module." });
            if (!await AsyncExecuter.AnyAsync((await ModulesRepository.GetQueryableAsync()), m => m.Id == request.ModuleId && m.SchoolId == admin.SchoolId)) return Results.NotFound();
        }

        var user = new StaffAccount { UserName = email, Email = email, EmailConfirmed = true, SchoolId = admin.SchoolId };
        var created = await users.CreateAsync(user, request.Password!);
        if (!created.Succeeded) return Results.BadRequest(new { error = "Account creation failed.", errors = created.Errors.Select(e => e.Description) });
        var assigned = await users.AddToRoleAsync(user, request.Role);
        if (!assigned.Succeeded) return Results.BadRequest(new { error = "Role assignment failed.", errors = assigned.Errors.Select(e => e.Description) });
        if (request.Role is "Teacher" or "Lecturer" or "Professor")
        {
            await StaffAllocationsRepository.InsertAsync(new StaffAllocationRow(GuidGenerator.Create())
            {
                SchoolId = admin.SchoolId,
                UserId = user.Id,
                Grade = request.Role == "Teacher" ? request.Grade : null,
                Subject = request.Role == "Teacher" ? request.Subject!.Trim() : null,
                ModuleId = request.Role == "Teacher" ? null : request.ModuleId
            });
            await UnitOfWorkManager.Current!.SaveChangesAsync();
        }

        return Results.Created("/api/me", new { user.Email, user.SchoolId, request.Role });

    }
}