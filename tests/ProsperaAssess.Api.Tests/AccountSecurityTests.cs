using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProsperaAssess.Api;
using Xunit;
namespace ProsperaAssess.Api.Tests;

public class AccountSecurityTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private sealed class Mail : IAccountEmailSender
    {
        public List<string> Bodies { get; } = [];
        public Task<bool> SendAsync(string recipient, string subject, string body) { Bodies.Add(body); return Task.FromResult(true); }
    }
    private async Task<(string Email, JsonElement Tokens)> NewUser(HttpClient client, string role = "Teacher")
    {
        var email = Guid.NewGuid().ToString("N") + "@account-tests.example";
        using var scope = fixture.Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = new AppUser { Email = email, UserName = email, SchoolId = fixture.School, EmailConfirmed = true };
        Assert.True((await users.CreateAsync(user, ApiFixture.Password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
        var login = await client.PostAsJsonAsync("/login", new { email, password = ApiFixture.Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (email, await login.Content.ReadFromJsonAsync<JsonElement>());
    }
    private static void Authorize(HttpClient client, JsonElement tokens) => client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.GetProperty("accessToken").GetString());
    private static string Totp(string key)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bytes = new List<byte>(); var buffer = 0; var bits = 0;
        foreach (var ch in key) { buffer = (buffer << 5) | alphabet.IndexOf(ch); bits += 5; if (bits >= 8) { bits -= 8; bytes.Add((byte)(buffer >> bits)); } }
        var counter = BitConverter.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30); if (BitConverter.IsLittleEndian) Array.Reverse(counter);
        var hash = HMACSHA1.HashData(bytes.ToArray(), counter); var offset = hash[^1] & 15;
        var number = ((hash[offset] & 127) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (number % 1000000).ToString("D6");
    }
    [Fact]
    public async Task RecoveryIsGenericAndResetInvalidatesOldTokensAndCannotBeReused()
    {
        var mail = new Mail();
        using var factory = fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<IAccountEmailSender>(mail)));
        using var client = factory.CreateClient(); var (email, tokens) = await NewUser(client);
        var known = await client.PostAsJsonAsync("/api/account/password/forgot", new { email });
        var unknown = await client.PostAsJsonAsync("/api/account/password/forgot", new { email = "unknown@account-tests.example" });
        Assert.Equal(await known.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());
        Assert.Single(mail.Bodies);
        var link = mail.Bodies[0].Split('\n').First(line => line.StartsWith("https://"));
        var code = link[(link.IndexOf("&code=") + 6)..].Trim();
        const string password = "Changed!Password2026";
        var reset = await client.PostAsJsonAsync("/api/account/password/reset", new { email, code, newPassword = password });
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/account/password/reset", new { email, code, newPassword = password })).StatusCode);
        Authorize(client, tokens);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/account/security")).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/refresh", new { refreshToken = tokens.GetProperty("refreshToken").GetString() })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/login", new { email, password })).StatusCode);
    }
    [Fact]
    public async Task MfaEnrollmentRequiresPasswordAndCodeAndRecoveryCodesAreSingleUse()
    {
        using var client = fixture.Client(); var (email, tokens) = await NewUser(client); Authorize(client, tokens);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/account/mfa/setup", new { password = "wrong" })).StatusCode);
        var setupResponse = await client.PostAsJsonAsync("/api/account/mfa/setup", new { password = ApiFixture.Password }); Assert.Equal(HttpStatusCode.OK, setupResponse.StatusCode);
        var setup = await setupResponse.Content.ReadFromJsonAsync<JsonElement>(); Authorize(client, setup.GetProperty("session"));
        var enabledResponse = await client.PostAsJsonAsync("/api/account/mfa/enable", new { code = Totp(setup.GetProperty("sharedKey").GetString()!) });
        Assert.Equal(HttpStatusCode.OK, enabledResponse.StatusCode);
        var enabled = await enabledResponse.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = null;
        var challenge = await client.PostAsJsonAsync("/login", new { email, password = ApiFixture.Password }); Assert.Equal(HttpStatusCode.Unauthorized, challenge.StatusCode);
        Assert.True((await challenge.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("requiresTwoFactor").GetBoolean());
        var recovery = enabled.GetProperty("recoveryCodes")[0].GetString();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/login", new { email, password = ApiFixture.Password, twoFactorRecoveryCode = recovery })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/login", new { email, password = ApiFixture.Password, twoFactorRecoveryCode = recovery })).StatusCode);
        var login = await client.PostAsJsonAsync("/login", new { email, password = ApiFixture.Password, twoFactorCode = Totp(setup.GetProperty("sharedKey").GetString()!) }); Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var current = await login.Content.ReadFromJsonAsync<JsonElement>(); Authorize(client, current);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/account/security")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/account/sessions/revoke", new { password = ApiFixture.Password })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/account/security")).StatusCode);
    }
    [Fact]
    public async Task PasswordChangeRequiresCurrentPasswordAndRevokesOtherSessions()
    {
        using var client = fixture.Client(); var (_, tokens) = await NewUser(client); Authorize(client, tokens);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/account/password/change", new { currentPassword = "wrong", newPassword = "New!Password2026" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/account/password/change", new { currentPassword = ApiFixture.Password, newPassword = "New!Password2026" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/account/security")).StatusCode);
    }
    [Fact]
    public async Task InvitationEmailUsesStoredRecipientAndRejectsNonAdministrators()
    {
        var mail = new Mail();
        using var factory = fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<IAccountEmailSender>(mail)));
        using var admin = factory.CreateClient(); using var authenticated = fixture.Client("SchoolAdmin");
        admin.DefaultRequestHeaders.Authorization = authenticated.DefaultRequestHeaders.Authorization;
        var invite = await admin.PostAsJsonAsync("/api/institution/invitations", new { email = Guid.NewGuid().ToString("N") + "@staff.example", role = "HOD" });
        Assert.Equal(HttpStatusCode.OK, invite.StatusCode);
        var path = (await invite.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("invitationPath").GetString()!;
        var token = path.Split("token=")[1];
        using var teacher = fixture.Client("Teacher");
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.PostAsJsonAsync("/api/institution/invitations/email", new { token })).StatusCode);
        Assert.Empty(mail.Bodies);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/institution/invitations/email", new { token })).StatusCode);
        Assert.Single(mail.Bodies); Assert.Contains("https://test.example/accept-invitation#token=", mail.Bodies[0]);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync("/api/institution/invitations/email", new { token = new string('0', 64) })).StatusCode);
        Assert.Single(mail.Bodies);
    }

    [Fact]
    public async Task AdministratorEnrollmentBlocksBusinessApisButAllowsAccountSetup()
    {
        using var factory = fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string,string?> { ["Authentication:RequireAdminMfa"] = "true" })));
        using var admin = factory.CreateClient(); using var authenticated = fixture.Client("SchoolAdmin");
        admin.DefaultRequestHeaders.Authorization = authenticated.DefaultRequestHeaders.Authorization;
        var status = await admin.GetFromJsonAsync<JsonElement>("/api/account/security");
        Assert.True(status.GetProperty("enrollmentRequired").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/assessments")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task DeployedRefreshTokensAreHttpOnlyAndRequireTheTrustedBrowserOrigin()
    {
        using var factory = fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string,string?> { ["Authentication:UseRefreshCookies"] = "true", ["AllowedHosts"] = "test.example" })));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://test.example") });
        client.DefaultRequestHeaders.Add("Origin", "https://test.example");
        var (email, tokens) = await NewUser(client);
        Assert.Equal("cookie", tokens.GetProperty("refreshToken").GetString());
        Authorize(client, tokens);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/account/security")).StatusCode);
        var login = await client.PostAsJsonAsync("/login", new { email, password = ApiFixture.Password });
        var header = string.Join(";", login.Headers.GetValues("Set-Cookie")).ToLowerInvariant();
        Assert.Contains("httponly", header); Assert.Contains("secure", header); Assert.Contains("samesite=strict", header);
        await Task.Delay(1100);
        var renewed = await client.PostAsJsonAsync("/refresh", new { refreshToken = "cookie" });
        Assert.Equal(HttpStatusCode.OK, renewed.StatusCode);
        var expiryBefore = System.Text.RegularExpressions.Regex.Match(header, "expires=([^;]+)").Groups[1].Value;
        var expiryAfter = System.Text.RegularExpressions.Regex.Match(string.Join(";", renewed.Headers.GetValues("Set-Cookie")).ToLowerInvariant(), "expires=([^;]+)").Groups[1].Value;
        Assert.Equal(expiryBefore, expiryAfter);
        client.DefaultRequestHeaders.Remove("Origin"); client.DefaultRequestHeaders.Add("Origin", "https://other.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/refresh", new { refreshToken = "cookie" })).StatusCode);
        client.DefaultRequestHeaders.Remove("Origin"); client.DefaultRequestHeaders.Add("Origin", "https://test.example");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/account/logout", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/refresh", new { refreshToken = "cookie" })).StatusCode);
    }

    [Fact]
    public async Task AdministratorCanReplaceALostAuthenticatorUsingRecoveryWithoutBypassingEnrollment()
    {
        using var factory = fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string,string?> { ["Authentication:RequireAdminMfa"] = "true" })));
        using var client = factory.CreateClient(); var (_, tokens) = await NewUser(client, "SchoolAdmin"); Authorize(client, tokens);
        var setup = await (await client.PostAsJsonAsync("/api/account/mfa/setup", new { password = ApiFixture.Password })).Content.ReadFromJsonAsync<JsonElement>(); Authorize(client, setup.GetProperty("session"));
        var enabled = await (await client.PostAsJsonAsync("/api/account/mfa/enable", new { code = Totp(setup.GetProperty("sharedKey").GetString()!) })).Content.ReadFromJsonAsync<JsonElement>(); Authorize(client, enabled.GetProperty("session"));
        var replacedResponse = await client.PostAsJsonAsync("/api/account/mfa/replace", new { password = ApiFixture.Password, code = enabled.GetProperty("recoveryCodes")[0].GetString(), recoveryCode = true });
        Assert.Equal(HttpStatusCode.OK, replacedResponse.StatusCode);
        var replaced = await replacedResponse.Content.ReadFromJsonAsync<JsonElement>(); Authorize(client, replaced.GetProperty("session"));
        Assert.NotEqual(setup.GetProperty("sharedKey").GetString(), replaced.GetProperty("sharedKey").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/assessments")).StatusCode);
        Assert.True((await client.GetFromJsonAsync<JsonElement>("/api/account/security")).GetProperty("enrollmentRequired").GetBoolean());
        var confirmed = await client.PostAsJsonAsync("/api/account/mfa/enable", new { code = Totp(replaced.GetProperty("sharedKey").GetString()!) }); Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        Authorize(client, (await confirmed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("session"));
        Assert.False((await client.GetFromJsonAsync<JsonElement>("/api/account/security")).GetProperty("enrollmentRequired").GetBoolean());
    }

}
