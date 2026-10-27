using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ProsperaAssess.Api;

using ProsperaAssess;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        // No public registration endpoint.
        app.MapPost("/login", async (LoginRequest request, SignInManager<AppUser> signIn, HttpContext context, IOptionsMonitor<BearerTokenOptions> options) =>
        {
            if (!AccountSecurityEndpoints.TrustedOrigin(context)) return Results.StatusCode(403);
            if (!InputRules.Email(request.Email) || !InputRules.Text(request.Password, 1024)) return Results.Unauthorized();
            signIn.AuthenticationScheme = IdentityConstants.BearerScheme;
            var user = await signIn.UserManager.FindByEmailAsync(request.Email!.Trim());
            if (user is null || !(await signIn.CheckPasswordSignInAsync(user, request.Password!, true)).Succeeded) return Results.Unauthorized();
            var method = "pwd";
            if (await signIn.UserManager.GetTwoFactorEnabledAsync(user))
            {
                if (string.IsNullOrWhiteSpace(request.TwoFactorCode) && string.IsNullOrWhiteSpace(request.TwoFactorRecoveryCode)) return Results.Json(new { error = "Enter your authenticator or recovery code.", requiresTwoFactor = true }, statusCode: 401);
                var valid = !string.IsNullOrWhiteSpace(request.TwoFactorRecoveryCode)
                    ? request.TwoFactorRecoveryCode.Length <= 100 && (await signIn.UserManager.RedeemTwoFactorRecoveryCodeAsync(user, request.TwoFactorRecoveryCode.Trim())).Succeeded
                    : request.TwoFactorCode!.Length <= 20 && await signIn.UserManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, request.TwoFactorCode.Replace(" ", "").Replace("-", ""));
                if (!valid) { await signIn.UserManager.AccessFailedAsync(user); return Results.Unauthorized(); }
                await signIn.UserManager.ResetAccessFailedCountAsync(user); method = "mfa";
            }
            return Results.Ok(await AccountSecurityEndpoints.Tokens(user, signIn, options, method == "mfa", context));
        }).RequireRateLimiting("login").WithTags("Authentication").Produces<SessionTokens>(200).Produces(401).Produces(403).Produces(429);

        app.MapPost("/refresh", async (RefreshRequest request, SignInManager<AppUser> signIn,
            IOptionsMonitor<BearerTokenOptions> options, TimeProvider clock, HttpContext context) =>
        {
            if (!AccountSecurityEndpoints.TrustedOrigin(context)) return Results.StatusCode(403);
            var refreshToken = AccountSecurityEndpoints.CookieMode(context) ? context.Request.Cookies["__Host-prospera-refresh"] : request.RefreshToken;
            if (!InputRules.Text(refreshToken, 16384)) return Results.Unauthorized();
            var ticket = options.Get(IdentityConstants.BearerScheme).RefreshTokenProtector.Unprotect(refreshToken!);
            if (ticket?.Properties.ExpiresUtc is not { } expiry || expiry <= clock.GetUtcNow()) return Results.Unauthorized();
            var user = await signIn.ValidateSecurityStampAsync(ticket.Principal);
            if (user is null || !await signIn.CanSignInAsync(user) || await signIn.UserManager.IsLockedOutAsync(user)) return Results.Unauthorized();
            return Results.Ok(await AccountSecurityEndpoints.Tokens(user, signIn, options, ticket.Principal.HasClaim(ClaimTypes.AuthenticationMethod, "mfa"), context, ticket.Properties.ExpiresUtc));
        }).RequireRateLimiting("login").WithTags("Authentication").Produces<SessionTokens>(200).Produces(401).Produces(403).Produces(429);

    }
}
