using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;
namespace ProsperaAssess;

[ApiController]
public class InvitationController(IInvitationAppService service) : AbpController
{
    [HttpPost("/api/institution/invitations")]
    public async Task<IActionResult> Invite([FromBody] InviteStaffInput input) { var r = await service.CreateAsync(input); return new ObjectResult(r.Data) { StatusCode = r.StatusCode }; }
    [Volo.Abp.Auditing.DisableAuditing]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("account")]
    [HttpPost("/api/institution/invitations/email")]
    public async Task<IActionResult> Email([FromBody] SendInvitationEmailInput input) { var r = await service.SendEmailAsync(input); return new ObjectResult(r.Data) { StatusCode = r.StatusCode }; }
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    [Volo.Abp.Auditing.DisableAuditing]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("login")]
    [HttpPost("/api/invitations/accept")]
    public async Task<IActionResult> Accept([FromBody] AcceptInvitationInput input) { var r = await service.AcceptAsync(input); return new ObjectResult(r.Data) { StatusCode = r.StatusCode }; }
}
