using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ProsperaAssess;

namespace ProsperaAssess.Api;

public sealed class OperationsTelemetry
{
    public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;
    private long requests, errors, slow, mailSent, mailFailed;
    public void Request(int status, TimeSpan duration) { Interlocked.Increment(ref requests); if (status >= 500) Interlocked.Increment(ref errors); if (duration.TotalSeconds >= 5) Interlocked.Increment(ref slow); }
    public void Mail(bool success) { if (success) Interlocked.Increment(ref mailSent); else Interlocked.Increment(ref mailFailed); }
    public object Snapshot() => new {StartedAt, requests=Interlocked.Read(ref requests), serverErrors=Interlocked.Read(ref errors), requestsOverFiveSeconds=Interlocked.Read(ref slow), mailSent=Interlocked.Read(ref mailSent), mailFailed=Interlocked.Read(ref mailFailed)};
}
public static class OperationsEndpoints
{
    public static void MapOperationsEndpoints(this WebApplication app)
    {
        app.MapGet("/api/health/live", () => Results.Ok(new {status="alive"})).AllowAnonymous();
        app.MapGet("/api/operations/metrics", async (HttpContext context, IConfiguration configuration, OperationsTelemetry telemetry, AssessDb db, CancellationToken ct) =>
        {
            var expected = configuration["Monitoring:ApiKey"]?.Trim();
            if (string.IsNullOrWhiteSpace(expected) || expected.Length < 32) return Results.NotFound();
            var supplied = context.Request.Headers["X-Prospera-Monitor-Key"].ToString();
            if (supplied.Length > 512 || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(expected)), SHA256.HashData(Encoding.UTF8.GetBytes(supplied)))) return Results.Unauthorized();
            if (!await db.Database.CanConnectAsync(ct)) return Results.Problem(statusCode:503,title:"Database unavailable");
            var counts = await db.AiScripts.GroupBy(s=>s.Status).Select(g=>new {status=g.Key,count=g.Count()}).ToListAsync(ct);
            var oldest = await db.AiScripts.Where(s=>s.Status=="Queued" || s.Status=="Processing").Select(s=>(DateTimeOffset?)s.UpdatedAt).MinAsync(ct);
            return Results.Ok(new {checkedAt=DateTimeOffset.UtcNow, database="available", process=telemetry.Snapshot(), scripts=counts, oldestPendingSeconds=oldest is null?0:Math.Max(0,(DateTimeOffset.UtcNow-oldest.Value).TotalSeconds)});
        }).AllowAnonymous().RequireRateLimiting("account").Produces(200).Produces(401).Produces(404).Produces(503);
    }
}
