using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Volo.Abp.Auditing;
using ProsperaAssess;
namespace ProsperaAssess.Api;

public record RecoveryEmailInput([property: Required, EmailAddress, StringLength(180)] string Email);
public record ResetPasswordInput([property: Required, EmailAddress, StringLength(180)] string Email, [property: Required, StringLength(4096)] string Code, [property: Required, StringLength(1024, MinimumLength = 12)] string NewPassword);
public record PasswordChangeInput([property: Required, StringLength(1024)] string CurrentPassword, [property: Required, StringLength(1024, MinimumLength = 12)] string NewPassword);
public record PasswordProof([property: Required, StringLength(1024)] string Password);
public record MfaProof([property: Required, StringLength(20)] string Code);
public record MfaDisableInput([property: Required, StringLength(1024)] string Password, [property: Required, StringLength(100)] string Code, bool RecoveryCode = false);
public record SessionTokens(string TokenType, string AccessToken, long ExpiresIn, string RefreshToken, string Subject);

public record AccountSecurityStatus(string Email, bool MfaEnabled, bool MfaRequired, int RecoveryCodesRemaining, bool EnrollmentRequired);
public record MfaSetupResponse(string SharedKey, string AuthenticatorUri, SessionTokens Session);
public record MfaCodesResponse(IEnumerable<string>? RecoveryCodes, SessionTokens Session);
public record AccountMessage(string Message);

public static class AccountSecurityEndpoints
{
    private static bool Valid(object input) => Validator.TryValidateObject(input, new ValidationContext(input), [], true);
    private static void RequireUpdate(IdentityResult result) { if (!result.Succeeded) throw new Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException("Account settings changed. Reload before retrying."); }
    private static IResult Invalid() => Results.BadRequest(new { error = "Check the supplied information and try again. Passwords must contain at least 12 characters." });
    private static async Task<AppUser?> Current(HttpContext context, SignInManager<AppUser> signIn) => await signIn.ValidateSecurityStampAsync(context.User);
    public static async Task<SessionTokens> Tokens(AppUser user, SignInManager<AppUser> signIn, IOptionsMonitor<BearerTokenOptions> options, bool mfa, HttpContext? context = null, DateTimeOffset? sessionExpiry = null)
    {
        var principal = await signIn.CreateUserPrincipalAsync(user);
        ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim(ClaimTypes.AuthenticationMethod, mfa ? "mfa" : "pwd"));
        var settings = options.Get(IdentityConstants.BearerScheme);
        var now = DateTimeOffset.UtcNow;
        var maximumExpiry = now + settings.RefreshTokenExpiration;
        var refreshExpiry = sessionExpiry is { } requested && requested < maximumExpiry ? requested : maximumExpiry;
        var accessExpiry = now + settings.BearerTokenExpiration < refreshExpiry ? now + settings.BearerTokenExpiration : refreshExpiry;
        var access = new AuthenticationTicket(principal, new AuthenticationProperties { IssuedUtc = now, ExpiresUtc = accessExpiry }, IdentityConstants.BearerScheme);
        var refresh = new AuthenticationTicket(principal, new AuthenticationProperties { IssuedUtc = now, ExpiresUtc = refreshExpiry }, IdentityConstants.BearerScheme);
        var refreshToken = settings.RefreshTokenProtector.Protect(refresh);
        if (context is not null && CookieMode(context))
        {
            context.Response.Cookies.Append("__Host-prospera-refresh", refreshToken, new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict, Path = "/", Expires = refreshExpiry, IsEssential = true });
            refreshToken = "cookie";
        }
        return new SessionTokens("Bearer", settings.BearerTokenProtector.Protect(access), (long)(accessExpiry - now).TotalSeconds, refreshToken, user.Id);
    }
    public static bool CookieMode(HttpContext context) => context.RequestServices.GetRequiredService<IConfiguration>().GetValue("Authentication:UseRefreshCookies", false);
    public static bool TrustedOrigin(HttpContext context)
    {
        if (!CookieMode(context)) return true;
        var expected = context.RequestServices.GetRequiredService<IConfiguration>()["App:PublicUrl"];
        return Uri.TryCreate(expected, UriKind.Absolute, out var origin) && Uri.TryCreate(context.Request.Headers.Origin, UriKind.Absolute, out var supplied) && Uri.Compare(origin, supplied, UriComponents.SchemeAndServer, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) == 0;
    }
    public static void MapAccountSecurityEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/account").WithTags("Account security").WithMetadata(new DisableAuditingAttribute(), new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(16384)).RequireRateLimiting("account");
        group.MapPost("/password/forgot", async (RecoveryEmailInput input, UserManager<AppUser> users, IAccountEmailSender mail, IConfiguration configuration) =>
        {
            if (!Valid(input)) return Invalid();
            var user = await users.FindByEmailAsync(input.Email.Trim());
            if (user is not null && !await users.IsLockedOutAsync(user))
            {
                var token = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(await users.GeneratePasswordResetTokenAsync(user)));
                var origin = configuration["App:PublicUrl"];
                if (Uri.TryCreate(origin, UriKind.Absolute, out _))
                {
                    var link = origin.TrimEnd('/') + "/reset-password#email=" + Uri.EscapeDataString(user.Email!) + "&code=" + token;
                    await mail.SendAsync(user.Email!, "Reset your Prospera Assess password", "A password reset was requested for your account. This link expires in one hour:\n\n" + link + "\n\nIf you did not request it, ignore this email.");
                }
            }
            return Results.Ok(new { message = "If the account can receive recovery email, a reset link will be sent. Check your inbox and spam folder." });
        }).AllowAnonymous().Produces<AccountMessage>(200).Produces(400).Produces(403).Produces(429);
        group.MapPost("/password/reset", async (ResetPasswordInput input, UserManager<AppUser> users) =>
        {
            if (!Valid(input)) return Invalid();
            var user = await users.FindByEmailAsync(input.Email.Trim());
            if (user is null || await users.IsLockedOutAsync(user)) return Invalid();
            string token;
            try { token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(input.Code)); } catch (FormatException) { return Invalid(); }
            var result = await users.ResetPasswordAsync(user, token, input.NewPassword);
            return result.Succeeded ? Results.Ok(new { message = "Password updated. Sign in again on your devices." }) : Invalid();
        }).AllowAnonymous().Produces<AccountMessage>(200).Produces(400).Produces(403).Produces(429);
        group.MapPost("/logout", (HttpContext context) =>
        {
            if (!TrustedOrigin(context)) return Results.StatusCode(403);
            context.Response.Cookies.Delete("__Host-prospera-refresh", new CookieOptions { Secure = true, HttpOnly = true, SameSite = SameSiteMode.Strict, Path = "/" });
            return Results.Ok(new { message = "Signed out." });
        }).AllowAnonymous().Produces<AccountMessage>(200).Produces(400).Produces(403).Produces(429);
        group.MapGet("/security", async (HttpContext context, SignInManager<AppUser> signIn, IConfiguration configuration) =>
        {
            var user = await Current(context, signIn); if (user is null) return Results.Unauthorized();
            var admin = context.User.IsInRole("SchoolAdmin") || context.User.IsInRole("AcademicAdmin");
            return Results.Ok(new AccountSecurityStatus(user.Email ?? "", user.TwoFactorEnabled, admin && configuration.GetValue("Authentication:RequireAdminMfa", false), await signIn.UserManager.CountRecoveryCodesAsync(user), admin && configuration.GetValue("Authentication:RequireAdminMfa", false) && !user.TwoFactorEnabled));
        }).RequireAuthorization().Produces<AccountSecurityStatus>(200).Produces(400).Produces(401).Produces(403).Produces(429);
        group.MapPost("/password/change", async (PasswordChangeInput input, HttpContext context, SignInManager<AppUser> signIn) =>
        {
            if (!Valid(input)) return Invalid(); var user = await Current(context, signIn); if (user is null) return Results.Unauthorized();
            var result = await signIn.UserManager.ChangePasswordAsync(user, input.CurrentPassword, input.NewPassword);
            return result.Succeeded ? Results.Ok(new { message = "Password changed. Sign in again." }) : Invalid();
        }).RequireAuthorization().Produces<AccountMessage>(200).Produces(400).Produces(401).Produces(403).Produces(429);
        group.MapPost("/sessions/revoke", async (PasswordProof input, HttpContext context, SignInManager<AppUser> signIn) =>
        {
            if (!Valid(input)) return Invalid(); var user = await Current(context, signIn); if (user is null) return Results.Unauthorized();
            if (!(await signIn.CheckPasswordSignInAsync(user, input.Password, true)).Succeeded) return Results.Unauthorized();
            RequireUpdate(await signIn.UserManager.UpdateSecurityStampAsync(user));
            return Results.Ok(new { message = "All existing sessions have been revoked. Sign in again." });
        }).RequireAuthorization().Produces<AccountMessage>(200).Produces(400).Produces(401).Produces(403).Produces(429);
        group.MapPost("/mfa/setup", async (PasswordProof input, HttpContext context, SignInManager<AppUser> signIn, IOptionsMonitor<BearerTokenOptions> options) =>
        {
            if (!Valid(input)) return Invalid(); var user = await Current(context, signIn); if (user is null) return Results.Unauthorized();
            if (!(await signIn.CheckPasswordSignInAsync(user, input.Password, true)).Succeeded) return Results.Unauthorized();
            if (user.TwoFactorEnabled) return Results.Conflict(new { error = "MFA is already enabled." });
            RequireUpdate(await signIn.UserManager.ResetAuthenticatorKeyAsync(user));
            var sharedKey = await signIn.UserManager.GetAuthenticatorKeyAsync(user);
            var uri = "otpauth://totp/Prospera%20Assess:" + Uri.EscapeDataString(user.Email!) + "?secret=" + sharedKey + "&issuer=Prospera%20Assess&digits=6";
            return Results.Ok(new MfaSetupResponse(sharedKey!, uri, await Tokens(user, signIn, options, false, context)));
        }).RequireAuthorization().Produces<MfaSetupResponse>(200).Produces(400).Produces(401).Produces(403).Produces(429);
        group.MapPost("/mfa/enable", async (MfaProof input, HttpContext context, SignInManager<AppUser> signIn, IOptionsMonitor<BearerTokenOptions> options) =>
        {
            if (!Valid(input)) return Invalid(); var user = await Current(context, signIn); if (user is null) return Results.Unauthorized();
            if (user.TwoFactorEnabled) return Results.Conflict();
            if (!await signIn.UserManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, input.Code.Replace(" ", "").Replace("-", ""))) { await signIn.UserManager.AccessFailedAsync(user); return Results.BadRequest(new { error = "The authenticator code is invalid." }); }
            RequireUpdate(await signIn.UserManager.SetTwoFactorEnabledAsync(user, true));
            var recoveryCodes = await signIn.UserManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10) ?? throw new Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException("Recovery codes could not be saved.");
            RequireUpdate(await signIn.UserManager.UpdateSecurityStampAsync(user));
            return Results.Ok(new MfaCodesResponse(recoveryCodes, await Tokens(user, signIn, options, true, context)));
        }).RequireAuthorization().Produces<MfaCodesResponse>(200).Produces(400).Produces(401).Produces(403).Produces(429);
        group.MapPost("/mfa/replace", async (MfaDisableInput input, HttpContext context, SignInManager<AppUser> signIn, IOptionsMonitor<BearerTokenOptions> options) =>
        {
            if (!Valid(input)) return Invalid(); var user = await Current(context, signIn); if (user is null) return Results.Unauthorized();
            if (!user.TwoFactorEnabled) return Results.Conflict(new { error = "Use authenticator setup first." });
            if (!(await signIn.CheckPasswordSignInAsync(user, input.Password, true)).Succeeded) return Results.Unauthorized();
            var valid = input.RecoveryCode ? (await signIn.UserManager.RedeemTwoFactorRecoveryCodeAsync(user, input.Code)).Succeeded : await signIn.UserManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, input.Code.Replace(" ", "").Replace("-", ""));
            if (!valid) { await signIn.UserManager.AccessFailedAsync(user); return Results.BadRequest(new { error = "The code is invalid." }); }
            RequireUpdate(await signIn.UserManager.SetTwoFactorEnabledAsync(user, false));
            RequireUpdate(await signIn.UserManager.ResetAuthenticatorKeyAsync(user));
            await signIn.UserManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 0);
            RequireUpdate(await signIn.UserManager.UpdateSecurityStampAsync(user));
            var sharedKey = await signIn.UserManager.GetAuthenticatorKeyAsync(user);
            var uri = "otpauth://totp/Prospera%20Assess:" + Uri.EscapeDataString(user.Email!) + "?secret=" + sharedKey + "&issuer=Prospera%20Assess&digits=6";
            return Results.Ok(new MfaSetupResponse(sharedKey!, uri, await Tokens(user, signIn, options, false, context)));
        }).RequireAuthorization().Produces<MfaSetupResponse>(200).Produces(400).Produces(401).Produces(403).Produces(429);
        group.MapPost("/mfa/disable", async (MfaDisableInput input, HttpContext context, SignInManager<AppUser> signIn, IConfiguration configuration) =>
        {
            if (!Valid(input)) return Invalid(); var user = await Current(context, signIn); if (user is null) return Results.Unauthorized();
            if (configuration.GetValue("Authentication:RequireAdminMfa", false) && (context.User.IsInRole("SchoolAdmin") || context.User.IsInRole("AcademicAdmin"))) return Results.BadRequest(new { error = "Administrators must keep MFA enabled." });
            if (!(await signIn.CheckPasswordSignInAsync(user, input.Password, true)).Succeeded) return Results.Unauthorized();
            var valid = input.RecoveryCode ? (await signIn.UserManager.RedeemTwoFactorRecoveryCodeAsync(user, input.Code)).Succeeded : await signIn.UserManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, input.Code.Replace(" ", "").Replace("-", ""));
            if (!valid) { await signIn.UserManager.AccessFailedAsync(user); return Results.BadRequest(new { error = "The code is invalid." }); }
            RequireUpdate(await signIn.UserManager.SetTwoFactorEnabledAsync(user, false));
            RequireUpdate(await signIn.UserManager.ResetAuthenticatorKeyAsync(user));
            await signIn.UserManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 0);
            RequireUpdate(await signIn.UserManager.UpdateSecurityStampAsync(user));
            return Results.Ok(new { message = "MFA disabled. Sign in again." });
        }).RequireAuthorization().Produces<AccountMessage>(200).Produces(400).Produces(401).Produces(403).Produces(429);
        group.MapPost("/mfa/recovery-codes", async (MfaDisableInput input, HttpContext context, SignInManager<AppUser> signIn, IOptionsMonitor<BearerTokenOptions> options) =>
        {
            if (!Valid(input)) return Invalid(); var user = await Current(context, signIn); if (user is null) return Results.Unauthorized();
            if (!user.TwoFactorEnabled || !(await signIn.CheckPasswordSignInAsync(user, input.Password, true)).Succeeded || !await signIn.UserManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, input.Code.Replace(" ", "").Replace("-", ""))) { await signIn.UserManager.AccessFailedAsync(user); return Results.BadRequest(new { error = "Re-enter your password and current authenticator code." }); }
            var recoveryCodes = await signIn.UserManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10) ?? throw new Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException("Recovery codes could not be saved.");
            RequireUpdate(await signIn.UserManager.UpdateSecurityStampAsync(user));
            return Results.Ok(new MfaCodesResponse(recoveryCodes, await Tokens(user, signIn, options, true, context)));
        }).RequireAuthorization().Produces<MfaCodesResponse>(200).Produces(400).Produces(401).Produces(403).Produces(429);
    }
}
