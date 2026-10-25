using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProsperaAssess;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.Data;
using Volo.Abp.Modularity;

var configuration = new ConfigurationBuilder().AddUserSecrets<ProsperaDbMigratorModule>(optional: true).AddEnvironmentVariables().AddCommandLine(args).Build();
if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("Default"))) throw new InvalidOperationException("Configure ConnectionStrings:Default before running DbMigrator.");
using var application = await AbpApplicationFactory.CreateAsync<ProsperaDbMigratorModule>(options =>
{
    options.UseAutofac(); options.Services.AddSingleton<IConfiguration>(configuration); options.Services.AddLogging(builder => builder.AddConsole());
});
await application.InitializeAsync();
try
{
    using var scope = application.ServiceProvider.CreateScope();
    await scope.ServiceProvider.GetRequiredService<SchemaMigrator>().MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<IDataSeeder>().SeedAsync();
    Console.WriteLine("Database migrations and ABP data seeding completed.");
}
finally { await application.ShutdownAsync(); }

[DependsOn(typeof(ProsperaAssessApplicationModule), typeof(ProsperaAssessEntityFrameworkCoreModule), typeof(AbpAutofacModule))]
public class ProsperaDbMigratorModule : AbpModule { }
