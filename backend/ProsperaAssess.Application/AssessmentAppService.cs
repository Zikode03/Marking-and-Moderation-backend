using System.Security.Claims;
using System.Text;
using System.Security.Cryptography;
using System.Text.Json;
namespace ProsperaAssess;

public class AssessmentAppService : ProsperaAppService, IAssessmentAppService
{
    [Microsoft.AspNetCore.Authorization.Authorize(ProsperaPermissions.Workspace)]
    public virtual async Task<OperationResult> GetAsync(Guid id)
    {
        var principal = Principal;

        var user = await users.GetUserAsync(principal);
        if (user is null || user.SchoolId == Guid.Empty) return Results.Unauthorized();
        var row = await AsyncExecuter.FirstOrDefaultAsync((await AssessmentsRepository.GetQueryableAsync()), a => a.Id == id && a.SchoolId == user.SchoolId);
        return row is null || !await access.CanAccess(user, principal, row) ? Results.NotFound() : Results.Ok(row.ToView());

    }
    [Microsoft.AspNetCore.Authorization.Authorize(ProsperaPermissions.Workspace)]
    public virtual async Task<OperationResult> GetListAsync()
    {
        var principal = Principal;

        var user = await users.GetUserAsync(principal);
        if (user is null || user.SchoolId == Guid.Empty) return Results.Unauthorized();
        var rows = await access.Visible(user, principal);
        return Results.Ok(rows.Select(a => a.ToView()));

    }
    [Microsoft.AspNetCore.Authorization.Authorize(ProsperaPermissions.Workspace)]
    public virtual async Task<OperationResult> GetGradesAsync()
    {
        var principal = Principal;
        return Results.Ok(Enumerable.Range(8, 5));
    }
    [Microsoft.AspNetCore.Authorization.Authorize(ProsperaPermissions.Workspace)]
    public virtual async Task<OperationResult> GetSchoolListAsync()
    {
        var principal = Principal;

        var user = await users.GetUserAsync(principal);
        if (user is null || user.SchoolId == Guid.Empty) return Results.Unauthorized();
        var rows = (await access.Visible(user, principal)).Where(a => a.SchoolJson != null);
        return Results.Ok(rows.Select(a => a.ToView()));

    }
    [Microsoft.AspNetCore.Authorization.Authorize(ProsperaPermissions.Workspace)]
    [Volo.Abp.Uow.UnitOfWork(true)]
    public virtual async Task<OperationResult> CreateAsync()
    {
        var principal = Principal;
        return Results.Json(new { error = "Assessment creation is disabled. Upload the existing paper, approved memorandum and scripts through /api/ai/batches." }, statusCode: 410);
    }
}