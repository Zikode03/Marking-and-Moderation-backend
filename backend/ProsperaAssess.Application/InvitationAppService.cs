using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Uow;
namespace ProsperaAssess;

public class InvitationAppService : ProsperaAppService, IInvitationAppService
{
    [Authorize(ProsperaPermissions.CreateStaff)]
    [UnitOfWork(true)]
    public virtual async Task<OperationResult> CreateAsync(InviteStaffInput input)
    {
        var user = await users.GetUserAsync(Principal); if (user is null) return Results.Unauthorized();
        if (!Principal.IsInRole("SchoolAdmin")) return Results.Forbid();
        if (!InputRules.Email(input.Email) || input.Role is not ("Teacher" or "Lecturer" or "Professor" or "HOD" or "Moderator" or "Principal" or "AcademicAdmin" or "SchoolAdmin")) return Results.BadRequest(new { error = "Provide a valid staff email and role." });
        if (await users.FindByEmailAsync(input.Email.Trim()) is not null) return Results.Conflict(new { error = "An account already exists for this email." });
        if (input.Role == "Teacher" && (input.Grade is not (>= 8 and <= 12) || !InputRules.Text(input.Subject, 200) || input.ModuleId is not null)) return Results.BadRequest(new { error = "Teachers need a grade and subject allocation." });
        if (input.Role is "Lecturer" or "Professor" && (input.ModuleId is null || !await AsyncExecuter.AnyAsync((await ModulesRepository.GetQueryableAsync()), m => m.Id == input.ModuleId && m.SchoolId == user.SchoolId))) return Results.BadRequest(new { error = "Select a module from this institution." });
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        var row = new InstitutionRecord(GuidGenerator.Create()) { SchoolId = user.SchoolId, Kind = "invitation", Code = hash, Name = input.Email.Trim(), Revision = 1, DataJson = AppJson.Write(new { email = input.Email.Trim(), input.Role, input.Grade, subject = input.Subject?.Trim(), input.ModuleId, expiresAt = DateTimeOffset.UtcNow.AddDays(7) }) };
        await InstitutionRecords.InsertAsync(row);
        await InstitutionEvents.InsertAsync(new InstitutionEvent(GuidGenerator.Create()) { SchoolId = user.SchoolId, RecordId = row.Id, Actor = user.Id, Action = "staff.invited", AfterJson = AppJson.Write(new { email = row.Name, input.Role }), At = DateTimeOffset.UtcNow });
        await UnitOfWorkManager.Current!.SaveChangesAsync(); return Results.Ok(new { invitationPath = "/accept-invitation#token=" + token, expiresInDays = 7 });
    }
    [Authorize(ProsperaPermissions.CreateStaff)]
    [Volo.Abp.Auditing.DisableAuditing]
    public virtual async Task<OperationResult> SendEmailAsync(SendInvitationEmailInput input)
    {
        var user = await users.GetUserAsync(Principal); if (user is null) return Results.Unauthorized();
        if (!Principal.IsInRole("SchoolAdmin")) return Results.Forbid();
        if (input.Token is null || input.Token.Length != 64 || !input.Token.All(Uri.IsHexDigit)) return Results.BadRequest(new { error = "Invalid invitation." });
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input.Token)));
        var row = await AsyncExecuter.FirstOrDefaultAsync((await InstitutionRecords.GetQueryableAsync()), r => r.SchoolId == user.SchoolId && r.Kind == "invitation" && r.Code == hash && r.Active);
        if (row is null) return Results.NotFound();
        var data = JsonSerializer.Deserialize<JsonElement>(row.DataJson);
        if (data.GetProperty("expiresAt").GetDateTimeOffset() <= DateTimeOffset.UtcNow) return Results.BadRequest(new { error = "Create a new invitation; this one has expired." });
        var mail = LazyServiceProvider.LazyGetRequiredService<IInvitationEmailSender>();
        if (!await mail.SendAsync(data.GetProperty("email").GetString()!, "/accept-invitation#token=" + input.Token)) return Results.Json(new { error = "Email delivery failed. Retry or share the activation link privately." }, statusCode: 503);
        return Results.Ok(new { message = "Invitation email sent." });
    }
    [AllowAnonymous]
    [Volo.Abp.Auditing.DisableAuditing]
    [UnitOfWork(true)]
    public virtual async Task<OperationResult> AcceptAsync(AcceptInvitationInput input)
    {
        if (!InputRules.Text(input.Token, 64) || input.Token.Length != 64 || !InputRules.Text(input.Password, 1024)) return Results.BadRequest(new { error = "Provide an invitation token and a valid password." });
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input.Token)));
        var row = await AsyncExecuter.FirstOrDefaultAsync((await InstitutionRecords.GetQueryableAsync()), r => r.Kind == "invitation" && r.Code == hash && r.Active);
        if (row is null) return Results.BadRequest(new { error = "This invitation is invalid, expired or already used." });
        var data = JsonSerializer.Deserialize<JsonElement>(row.DataJson);
        if (data.GetProperty("expiresAt").GetDateTimeOffset() <= DateTimeOffset.UtcNow) return Results.BadRequest(new { error = "This invitation has expired." });
        var email = data.GetProperty("email").GetString()!;
        if (await users.FindByEmailAsync(email) is not null) return Results.Conflict(new { error = "This email already has an account." });
        var account = new StaffAccount { SchoolId = row.SchoolId, Email = email, UserName = email, EmailConfirmed = false };
        var created = await users.CreateAsync(account, input.Password);
        if (!created.Succeeded) return Results.BadRequest(new { error = "Choose a password that meets the account requirements.", errors = created.Errors.Select(e => e.Description) });
        var role = data.GetProperty("role").GetString()!;
        if (!(await users.AddToRoleAsync(account, role)).Succeeded) return Results.BadRequest(new { error = "The role could not be assigned." });
        if (role is "Teacher" or "Lecturer" or "Professor") await StaffAllocationsRepository.InsertAsync(new StaffAllocationRow(GuidGenerator.Create()) { SchoolId = row.SchoolId, UserId = account.Id, Grade = role == "Teacher" ? data.GetProperty("grade").GetInt32() : null, Subject = role == "Teacher" ? data.GetProperty("subject").GetString() : null, ModuleId = role == "Teacher" ? null : data.GetProperty("moduleId").GetGuid() });
        row.Active = false; row.Revision++; await InstitutionRecords.UpdateAsync(row);
        await InstitutionEvents.InsertAsync(new InstitutionEvent(GuidGenerator.Create()) { SchoolId = row.SchoolId, RecordId = row.Id, Actor = account.Id, Action = "staff.invitation.accepted", AfterJson = AppJson.Write(new { email, role }), At = DateTimeOffset.UtcNow });
        await UnitOfWorkManager.Current!.SaveChangesAsync(); return Results.Ok(new { message = "Your account is ready. Sign in with your email and password." });
    }
}
