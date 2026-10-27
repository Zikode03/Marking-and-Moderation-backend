using ProsperaAssess;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using ProsperaAssess.Api;
using System.Threading.RateLimiting;
using Volo.Abp;
using Volo.Abp.Autofac;

#if DEBUG
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, EnvironmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? Environments.Development });
#else
var builder = WebApplication.CreateBuilder(args);
#endif
builder.Configuration.AddKeyPerFile("/run/secrets", optional: true);
ProductionConfiguration.Validate(builder.Configuration, builder.Environment);
if (string.IsNullOrWhiteSpace(builder.Configuration["urls"]))
    builder.WebHost.UseUrls("http://localhost:5080");
builder.Host.UseAutofac();
LegacyTokenProtection.Configure(builder.Services, builder.Configuration, builder.Environment);
#if DEBUG
// Visual Studio can launch a Debug executable without the Development profile.
if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("Default")))
    builder.Configuration.AddUserSecrets<Program>(optional: true);
#endif
var connection = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connection))
    throw new InvalidOperationException("Set ConnectionStrings:Default using .NET user secrets or ConnectionStrings__Default. See docs/SQL_AUTH_SETUP.md.");

builder.Services.AddTransient<AcademicAccess>();
builder.Services.AddTransient<IUploadSafety, ClamUploadSafety>();
builder.Services.AddTransient<IAccountEmailSender, AccountEmailSender>();
builder.Services.AddTransient<IInvitationEmailSender, InvitationEmailSender>();
builder.Services.Configure<Microsoft.AspNetCore.Authentication.BearerToken.BearerTokenOptions>(IdentityConstants.BearerScheme, o => { o.BearerTokenExpiration = TimeSpan.FromMinutes(20); o.RefreshTokenExpiration = TimeSpan.FromHours(8); });
ProductionConfiguration.ConfigureProxy(builder.Services, builder.Configuration);
builder.Services.AddSingleton<AiMarkingWorker>();
builder.Services.AddSingleton<OperationsTelemetry>();
builder.Services.AddHttpClient<LocalMarker>(client => client.Timeout = TimeSpan.FromMinutes(11));
builder.Services.AddTransient<IAiMarkingEngine>(sp => sp.GetRequiredService<LocalMarker>());

builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = UploadLimits.MaxUpload);
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o => o.MultipartBodyLengthLimit = UploadLimits.MaxUpload);
builder.Services.AddIdentityApiEndpoints<AppUser>(o => { o.User.RequireUniqueEmail = true; o.Password.RequiredLength = 12; })
    .AddRoles<IdentityRole>();
builder.Services.AddTransient<IUserStore<AppUser>, PortalUserStore>();
builder.Services.AddRateLimiter(o =>
{
    o.AddPolicy("uploads", _ => RateLimitPartition.GetConcurrencyLimiter("uploads", _ => new ConcurrencyLimiterOptions {PermitLimit=2,QueueLimit=0}));
    o.AddPolicy("account", context => RateLimitPartition.GetFixedWindowLimiter(context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(10), QueueLimit = 0 }));
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.StatusCode = 429;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry)) context.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retry.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
        await context.HttpContext.Response.WriteAsJsonAsync(new { error = "Too many attempts. Please wait before trying again." }, ct);
    };
    o.AddPolicy("sales", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(10),
            QueueLimit = 0
        }));
    o.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});
builder.Services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromHours(1));
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<SecurityOpenApiTransformer>());
var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173"];
builder.Services.AddCors(o => o.AddPolicy("frontend", p => p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));
await builder.AddApplicationAsync<ProsperaAssessHttpApiHostModule>();
var app = builder.Build();
await app.InitializeApplicationAsync();
app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var status = exception switch
    {
        DbUpdateConcurrencyException => 409,
        DbUpdateException update when update.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 } => 409,
        BadHttpRequestException badRequest => badRequest.StatusCode,
        _ => 500
    };
    var title = status switch
    {
        409 => "The record changed. Refresh before saving again.",
        400 => "The request body or parameters are invalid.",
        413 => "The request body is too large.",
        _ => "An unexpected error occurred."
    };
    await Results.Problem(statusCode: status, title: title)
        .ExecuteAsync(context);
}));
app.Use(async (context, next) =>
{
    var started = System.Diagnostics.Stopwatch.GetTimestamp();
    var status = 500;
    try { await next(); status=context.Response.StatusCode; }
    finally { app.Services.GetRequiredService<OperationsTelemetry>().Request(status,System.Diagnostics.Stopwatch.GetElapsedTime(started)); }
});
app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment()) app.UseHsts();
app.Use(async (context, next) => { context.Response.Headers.CacheControl = "no-store"; context.Response.Headers["X-Content-Type-Options"] = "nosniff"; context.Response.Headers["Referrer-Policy"] = "no-referrer"; await next(); });
app.UseCors("frontend");
app.UseAuthentication();
app.UseRateLimiter();
app.UseUnitOfWork();
app.UseMiddleware<AccountSecurityMiddleware>();
app.UseAuthorization();
app.UseAuditing();
await DatabaseSetup.InitializeAsync(app);
await app.Services.GetRequiredService<Volo.Abp.BackgroundWorkers.IBackgroundWorkerManager>().AddAsync(app.Services.GetRequiredService<AiMarkingWorker>());
app.MapGet("/api/health", async (AssessDb db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct)
        ? Results.Ok(new { status = "ok", persistence = "sql-server", ai = "local-model" })
        : Results.Problem(statusCode: 503, title: "Database unavailable"));
if (app.Environment.IsDevelopment() || app.Environment.IsStaging() || app.Configuration.GetValue("OpenApi:Enabled", false)) app.MapOpenApi();
app.MapOperationsEndpoints();
app.MapAuthEndpoints();
app.MapAccountSecurityEndpoints();
app.MapControllers();
app.Run();

public partial class Program { }
