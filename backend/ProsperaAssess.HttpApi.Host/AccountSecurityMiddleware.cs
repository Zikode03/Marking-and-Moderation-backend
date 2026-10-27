using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.Extensions.Options;
using ProsperaAssess;
namespace ProsperaAssess.Api;

public sealed class AccountSecurityMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, SignInManager<AppUser> signIn, IConfiguration configuration, IOptionsMonitor<BearerTokenOptions> options)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            if (configuration.GetValue("Authentication:UseRefreshCookies", false))
            {
                var raw = context.Request.Cookies["__Host-prospera-refresh"];
                var ticket = string.IsNullOrWhiteSpace(raw) ? null : options.Get(IdentityConstants.BearerScheme).RefreshTokenProtector.Unprotect(raw);
                var idClaim = signIn.Options.ClaimsIdentity.UserIdClaimType;
                var stampClaim = signIn.Options.ClaimsIdentity.SecurityStampClaimType;
                if (ticket?.Properties.ExpiresUtc is not { } expiry || expiry <= DateTimeOffset.UtcNow || ticket.Principal.FindFirst(idClaim)?.Value != context.User.FindFirst(idClaim)?.Value || ticket.Principal.FindFirst(stampClaim)?.Value != context.User.FindFirst(stampClaim)?.Value)
                { context.Response.StatusCode = 401; await context.Response.WriteAsJsonAsync(new { error = "This browser session has ended or changed. Sign in again." }); return; }
            }
            var user = await signIn.ValidateSecurityStampAsync(context.User);
            if (user is null || !await signIn.CanSignInAsync(user) || await signIn.UserManager.IsLockedOutAsync(user))
            { context.Response.StatusCode = 401; await context.Response.WriteAsJsonAsync(new { error = "Your session has ended. Sign in again." }); return; }
            if (user.TwoFactorEnabled && !context.User.HasClaim(System.Security.Claims.ClaimTypes.AuthenticationMethod, "mfa"))
            { context.Response.StatusCode = 401; await context.Response.WriteAsJsonAsync(new { error = "Sign in again with your authenticator code." }); return; }
            var requires = configuration.GetValue("Authentication:RequireAdminMfa", false) && (context.User.IsInRole("SchoolAdmin") || context.User.IsInRole("AcademicAdmin"));
            if (requires && !user.TwoFactorEnabled && context.Request.Path != "/api/me" && !context.Request.Path.StartsWithSegments("/api/account"))
            { context.Response.StatusCode = 403; await context.Response.WriteAsJsonAsync(new { error = "Set up multi-factor authentication under Account security before managing the organisation.", mfaEnrollmentRequired = true }); return; }
        }
        await next(context);
    }
}
