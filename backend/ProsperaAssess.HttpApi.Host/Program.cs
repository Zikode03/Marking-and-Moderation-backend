using ProsperaAssess;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using ProsperaAssess.Api;
using System.Threading.RateLimiting;
using Volo.Abp;
using Volo.Abp.Autofac;

var builder = WebApplication.CreateBuilder(args);
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
builder.Services.AddSingleton<AiMarkingWorker>();
builder.Services.AddHttpClient<LocalMarker>(client => client.Timeout = TimeSpan.FromMinutes(11));
builder.Services.AddTransient<IAiMarkingEngine>(sp => sp.GetRequiredService<LocalMarker>());

builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = UploadLimits.MaxUpload);
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o => o.MultipartBodyLengthLimit = UploadLimits.MaxUpload);
builder.Services.AddIdentityApiEndpoints<AppUser>(o => o.User.RequireUniqueEmail = true)
    .AddRoles<IdentityRole>();
builder.Services.AddTransient<IUserStore<AppUser>, PortalUserStore>();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
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
builder.Services.AddProblemDetails();
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
app.UseCors("frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseUnitOfWork();
app.UseAuthorization();
app.UseAuditing();
await DatabaseSetup.InitializeAsync(app);
await app.Services.GetRequiredService<Volo.Abp.BackgroundWorkers.IBackgroundWorkerManager>().AddAsync(app.Services.GetRequiredService<AiMarkingWorker>());
app.MapGet("/api/health", async (AssessDb db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct)
        ? Results.Ok(new { status = "ok", persistence = "sql-server", ai = "local-model" })
        : Results.Problem(statusCode: 503, title: "Database unavailable"));
app.MapAuthEndpoints();
app.MapControllers();
app.Run();

public partial class Program { }
