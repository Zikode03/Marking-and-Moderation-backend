using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProsperaAssess;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.Data;
using Volo.Abp.Modularity;

var configuration = new ConfigurationBuilder().AddUserSecrets<ProsperaDbMigratorModule>(optional: true).AddEnvironmentVariables().AddKeyPerFile("/run/secrets", optional: true).AddCommandLine(args).Build();
if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("Default"))) throw new InvalidOperationException("Configure ConnectionStrings:Default before running DbMigrator.");
if (string.Equals(Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT"), "Production", StringComparison.OrdinalIgnoreCase))
{
    var sql = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(configuration.GetConnectionString("Default"));
    if (string.Equals(sql.UserID, "sa", StringComparison.OrdinalIgnoreCase) || sql.TrustServerCertificate || sql.Encrypt.Equals(Microsoft.Data.SqlClient.SqlConnectionEncryptOption.Optional)) throw new InvalidOperationException("Use a dedicated migration identity and trusted SQL TLS in production.");
}
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
