using System.Security.Claims;
using System.Text;
using System.Security.Cryptography;
using System.Text.Json;
namespace ProsperaAssess;

public class AcademicAppService : ProsperaAppService, IAcademicAppService
{
    [Microsoft.AspNetCore.Authorization.Authorize(ProsperaPermissions.Workspace)]
    public virtual async Task<OperationResult> GetStructureAsync(string organisationType)
    {
        var principal = Principal;

        if (organisationType is not ("school" or "institution")) return Results.BadRequest(new { error = "Choose school or institution." });
        var user = await users.GetUserAsync(principal);
        if (user is null || user.SchoolId == Guid.Empty) return Results.Unauthorized();
        var leader = AcademicAccess.Leader(principal);
        var allocations = await AsyncExecuter.ToListAsync((await StaffAllocationsRepository.GetQueryableAsync()).Where(a => a.SchoolId == user.SchoolId && (leader || a.UserId == user.Id)));
        allocations = allocations.Where(a => organisationType == "school" ? a.ModuleId == null : a.ModuleId != null).ToList();
        var moduleIds = allocations.Where(a => a.ModuleId != null).Select(a => a.ModuleId!.Value).ToList();
        var modules = organisationType == "institution" ? await AsyncExecuter.ToListAsync((await ModulesRepository.GetQueryableAsync()).Where(m => m.SchoolId == user.SchoolId && (leader || moduleIds.Contains(m.Id))).OrderBy(m => m.Code)) : [];
        var facultyIds = modules.Select(m => m.FacultyId).ToList();
        var faculties = organisationType == "institution" ? await AsyncExecuter.ToListAsync((await FacultiesRepository.GetQueryableAsync()).Where(f => f.SchoolId == user.SchoolId && (leader || facultyIds.Contains(f.Id))).OrderBy(f => f.Name)) : [];
        var roster = new List<object>();
        if (AcademicAccess.Manager(principal))
        {
            foreach (var member in await users.GetUsersAsync(user.SchoolId))
            {
                var roles = await users.GetRolesAsync(member);
                if (organisationType == "school" ? roles.Contains("Teacher") : roles.Any(r => r is "Lecturer" or "Professor"))
                    roster.Add(new { member.Id, member.Email, roles });
            }
        }
        return Results.Ok(new { organisationType, grades = Enumerable.Range(8, 5), faculties, modules, allocations, staff = roster });

    }
    [Microsoft.AspNetCore.Authorization.Authorize(ProsperaPermissions.ManageAcademics)]
    [Volo.Abp.Uow.UnitOfWork(true)]
    public virtual async Task<OperationResult> CreateFacultyAsync(CreateFaculty request)
    {
        var principal = Principal;

        var user = await users.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();
        if (!InputRules.Text(request.Name, 200) || !InputRules.Text(request.Code, 50)) return Invalid("Faculty name and code are required.");
        var code = request.Code!.Trim().ToUpperInvariant();
        if (await AsyncExecuter.AnyAsync((await FacultiesRepository.GetQueryableAsync()), f => f.SchoolId == user.SchoolId && f.Code == code)) return Duplicate();
        var faculty = new FacultyRow(GuidGenerator.Create()) { SchoolId = user.SchoolId, Name = request.Name!.Trim(), Code = code };
        await FacultiesRepository.InsertAsync(faculty);
        await UnitOfWorkManager.Current!.SaveChangesAsync();
        return Results.Ok(faculty);

    }
    [Microsoft.AspNetCore.Authorization.Authorize(ProsperaPermissions.ManageAcademics)]
    [Volo.Abp.Uow.UnitOfWork(true)]
    public virtual async Task<OperationResult> UpdateFacultyAsync(Guid id, CreateFaculty request)
    {
        var principal = Principal;

        var user = await users.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();
        var faculty = await AsyncExecuter.FirstOrDefaultAsync((await FacultiesRepository.GetQueryableAsync()), f => f.Id == id && f.SchoolId == user.SchoolId);
        if (faculty is null) return Results.NotFound();
        if (!InputRules.Text(request.Name, 200) || !InputRules.Text(request.Code, 50)) return Invalid("Faculty name and code are required.");
        var code = request.Code!.Trim().ToUpperInvariant();
        if (await AsyncExecuter.AnyAsync((await FacultiesRepository.GetQueryableAsync()), f => f.SchoolId == user.SchoolId && f.Id != id && f.Code == code)) return Duplicate();
        faculty.Name = request.Name!.Trim(); faculty.Code = code;
        await FacultiesRepository.UpdateAsync(faculty);
        await UnitOfWorkManager.Current!.SaveChangesAsync();
        return Results.Ok(faculty);

    }
    [Microsoft.AspNetCore.Authorization.Authorize(ProsperaPermissions.ManageAcademics)]
    [Volo.Abp.Uow.UnitOfWork(true)]
    public virtual async Task<OperationResult> CreateModuleAsync(CreateModule request)
    {
        var principal = Principal;

        var user = await users.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();
        if (!InputRules.Text(request.Name, 200) || !InputRules.Text(request.Code, 50)) return Invalid("Module name, code and faculty are required.");
        if (!await AsyncExecuter.AnyAsync((await FacultiesRepository.GetQueryableAsync()), f => f.Id == request.FacultyId && f.SchoolId == user.SchoolId)) return Results.NotFound();
        var code = request.Code!.Trim().ToUpperInvariant();
        if (await AsyncExecuter.AnyAsync((await ModulesRepository.GetQueryableAsync()), m => m.SchoolId == user.SchoolId && m.Code == code)) return Duplicate();
        var module = new ModuleRow(GuidGenerator.Create()) { SchoolId = user.SchoolId, FacultyId = request.FacultyId, Name = request.Name!.Trim(), Code = code };
        await ModulesRepository.InsertAsync(module);
        await UnitOfWorkManager.Current!.SaveChangesAsync();
        return Results.Ok(module);

    }
    [Microsoft.AspNetCore.Authorization.Authorize(ProsperaPermissions.ManageAcademics)]
    [Volo.Abp.Uow.UnitOfWork(true)]
    public virtual async Task<OperationResult> UpdateModuleAsync(Guid id, CreateModule request)
    {
        var principal = Principal;

        var user = await users.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();
        var module = await AsyncExecuter.FirstOrDefaultAsync((await ModulesRepository.GetQueryableAsync()), m => m.Id == id && m.SchoolId == user.SchoolId);
        if (module is null || !await AsyncExecuter.AnyAsync((await FacultiesRepository.GetQueryableAsync()), f => f.Id == request.FacultyId && f.SchoolId == user.SchoolId)) return Results.NotFound();
        if (!InputRules.Text(request.Name, 200) || !InputRules.Text(request.Code, 50)) return Invalid("Module name and code are required.");
        var code = request.Code!.Trim().ToUpperInvariant();
        if (await AsyncExecuter.AnyAsync((await ModulesRepository.GetQueryableAsync()), m => m.SchoolId == user.SchoolId && m.Id != id && m.Code == code)) return Duplicate();
        if (module.FacultyId != request.FacultyId) return Invalid("A module cannot move faculties after creation; create it under the correct faculty.");
        module.Name = request.Name!.Trim(); module.Code = code;
        await ModulesRepository.UpdateAsync(module);
        await UnitOfWorkManager.Current!.SaveChangesAsync();
        return Results.Ok(module);

    }
    [Microsoft.AspNetCore.Authorization.Authorize(ProsperaPermissions.ManageAcademics)]
    [Volo.Abp.Uow.UnitOfWork(true)]
    public virtual async Task<OperationResult> AllocateStaffAsync(CreateAllocation request)
    {
        var principal = Principal;

        var admin = await users.GetUserAsync(principal);
        if (admin is null) return Results.Unauthorized();
        var member = request.UserId is null ? null : await users.FindByIdAsync(request.UserId);
        if (member is null || member.SchoolId != admin.SchoolId) return Results.NotFound();
        var moduleAllocation = request.ModuleId is not null;
        if (moduleAllocation)
        {
            if (request.Grade != null || !string.IsNullOrWhiteSpace(request.Subject)) return Invalid("Module allocations cannot include a school grade or subject.");
            if (!await users.IsInRoleAsync(member, "Lecturer") && !await users.IsInRoleAsync(member, "Professor")) return Invalid("Only lecturers or professors can be allocated institution modules.");
            if (!await AsyncExecuter.AnyAsync((await ModulesRepository.GetQueryableAsync()), m => m.Id == request.ModuleId && m.SchoolId == admin.SchoolId)) return Results.NotFound();
        }
        else if (request.Grade is not (>= 8 and <= 12) || !InputRules.Text(request.Subject, 200) || !await users.IsInRoleAsync(member, "Teacher"))
            return Invalid("School allocations require a teacher, a Grade 8–12 and a subject.");
        var subject = moduleAllocation ? null : request.Subject!.Trim();
        if (await AsyncExecuter.AnyAsync((await StaffAllocationsRepository.GetQueryableAsync()), a => a.SchoolId == admin.SchoolId && a.UserId == member.Id && a.ModuleId == request.ModuleId && a.Grade == request.Grade && a.Subject == subject)) return Duplicate();
        var allocation = new StaffAllocationRow(GuidGenerator.Create()) { SchoolId = admin.SchoolId, UserId = member.Id, Grade = request.Grade, Subject = subject, ModuleId = request.ModuleId };
        await StaffAllocationsRepository.InsertAsync(allocation);
        await UnitOfWorkManager.Current!.SaveChangesAsync();
        return Results.Ok(allocation);

    }
    [Microsoft.AspNetCore.Authorization.Authorize(ProsperaPermissions.ManageAcademics)]
    [Volo.Abp.Uow.UnitOfWork(true, System.Data.IsolationLevel.Serializable)]
    public virtual async Task<OperationResult> RemoveAllocationAsync(Guid id)
    {
        var principal = Principal;

        var admin = await users.GetUserAsync(principal);
        if (admin is null) return Results.Unauthorized();

        var allocation = await AsyncExecuter.FirstOrDefaultAsync((await StaffAllocationsRepository.GetQueryableAsync()), a => a.Id == id && a.SchoolId == admin.SchoolId);
        if (allocation is null) return Results.NotFound();
        if (await AsyncExecuter.CountAsync((await StaffAllocationsRepository.GetQueryableAsync()), a => a.SchoolId == admin.SchoolId && a.UserId == allocation.UserId) <= 1)
            return Invalid("Add a replacement allocation before removing this staff member's last teaching responsibility.");
        await StaffAllocationsRepository.DeleteAsync(allocation);
        await UnitOfWorkManager.Current!.SaveChangesAsync();

        return Results.Ok(new { removed = true });

    }
    private static OperationResult Invalid(string error) => Results.BadRequest(new { error });
    private static OperationResult Duplicate() => Results.Conflict(new { error = "This code or allocation already exists." });

}