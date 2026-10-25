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
        var migrator = scope.ServiceProvider.GetRequiredService<SchemaMigrator>();
        if (app.Configuration.GetValue("Database:Initialize", local)) await migrator.MigrateAsync(); else await migrator.VerifyAsync();
        await scope.ServiceProvider.GetRequiredService<IDataSeeder>().SeedAsync();
    }
}
