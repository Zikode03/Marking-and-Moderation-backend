using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using ProsperaAssess;
namespace ProsperaAssess.Api;

public static class DatabaseSetup
{
    public static async Task InitializeAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope(); var local = app.Environment.IsDevelopment();
#if DEBUG
        local = true;
#endif
        if (app.Environment.IsStaging() || app.Environment.IsProduction())
        {
            var db = scope.ServiceProvider.GetRequiredService<AssessDb>();
            var connection = db.Database.GetDbConnection();
            await connection.OpenAsync();
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT CAST(SERVERPROPERTY('Edition') AS nvarchar(128))";
                var edition = (await command.ExecuteScalarAsync())?.ToString() ?? "";
                if (edition.Contains("Developer", StringComparison.OrdinalIgnoreCase) || edition.Contains("Evaluation", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Use a SQL edition licensed for production before starting staging/production.");
                if (app.Configuration.GetValue("Storage:RequireEncryptionAtRest", false))
                {
                    command.CommandText = "SELECT CONVERT(int,is_encrypted) FROM sys.databases WHERE database_id=DB_ID()";
                    if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 1) throw new InvalidOperationException("Enable database encryption at rest before starting staging/production.");
                }
                command.CommandText = "SELECT CASE WHEN IS_SRVROLEMEMBER('sysadmin') = 1 OR IS_MEMBER('db_owner') = 1 THEN 1 ELSE 0 END";
                if (Convert.ToInt32(await command.ExecuteScalarAsync()) == 1) throw new InvalidOperationException("The runtime database identity must not be sysadmin or db_owner.");
            }
            finally { await connection.CloseAsync(); }
        }
        var migrator = scope.ServiceProvider.GetRequiredService<SchemaMigrator>();
        if (app.Configuration.GetValue("Database:Initialize", local)) await migrator.MigrateAsync(); else await migrator.VerifyAsync();
        await scope.ServiceProvider.GetRequiredService<IDataSeeder>().SeedAsync();
    }
}
