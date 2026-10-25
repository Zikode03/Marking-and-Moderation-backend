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
        app.MapPost("/login", async (LoginRequest request, SignInManager<AppUser> signIn) =>
        {
            if (!InputRules.Email(request.Email) || !InputRules.Text(request.Password, 1024)) return Results.Unauthorized();
            signIn.AuthenticationScheme = IdentityConstants.BearerScheme;
            var result = await signIn.PasswordSignInAsync(request.Email!.Trim(), request.Password!, false, true);
            return result.Succeeded ? Results.Empty : Results.Unauthorized();
        }).RequireRateLimiting("login");

        app.MapPost("/refresh", async (RefreshRequest request, SignInManager<AppUser> signIn,
            IOptionsMonitor<BearerTokenOptions> options, TimeProvider clock) =>
        {
            if (!InputRules.Text(request.RefreshToken, 16384)) return Results.Unauthorized();
            var ticket = options.Get(IdentityConstants.BearerScheme).RefreshTokenProtector.Unprotect(request.RefreshToken!);
            if (ticket?.Properties.ExpiresUtc is not { } expiry || expiry <= clock.GetUtcNow()) return Results.Unauthorized();
            var user = await signIn.ValidateSecurityStampAsync(ticket.Principal);
            if (user is null || !await signIn.CanSignInAsync(user) || await signIn.UserManager.IsLockedOutAsync(user)) return Results.Unauthorized();
            return Results.SignIn(await signIn.CreateUserPrincipalAsync(user), authenticationScheme: IdentityConstants.BearerScheme);
        }).RequireRateLimiting("login");

    }
}
