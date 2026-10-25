using Microsoft.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
namespace ProsperaAssess;

public class SchemaMigrator(AssessDb db) : ITransientDependency
{
    public Task MigrateAsync(CancellationToken ct = default) => db.Database.MigrateAsync(ct);
    public async Task VerifyAsync(CancellationToken ct = default)
    {
        if (!await db.Database.CanConnectAsync(ct)) throw new InvalidOperationException("The SQL database is unavailable.");
        if ((await db.Database.GetPendingMigrationsAsync(ct)).Any()) throw new InvalidOperationException("Database migrations are pending. Run ProsperaAssess.DbMigrator first.");
    }
}
