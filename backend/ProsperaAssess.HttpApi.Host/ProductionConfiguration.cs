using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Data.SqlClient;
namespace ProsperaAssess.Api;

public static class ProductionConfiguration
{
    public static void Validate(IConfiguration configuration, IHostEnvironment environment)
    {
        if (!environment.IsProduction() && !environment.IsStaging()) return;
        if ((configuration["Monitoring:ApiKey"]?.Trim().Length ?? 0) < 32) throw new InvalidOperationException("Configure a dedicated monitoring credential of at least 32 characters.");
        if (string.IsNullOrWhiteSpace(configuration["Uploads:ScannerHost"])) throw new InvalidOperationException("Configure malware scanning before accepting production uploads.");
        if (!configuration.GetValue("Storage:RequireEncryptionAtRest", false) || configuration.GetValue<long>("Storage:TenantQuotaBytes") < 60L * 1024 * 1024) throw new InvalidOperationException("Require encrypted database storage and an explicit tenant document quota.");
        // WebApplicationFactory uses a dedicated Testing environment.
        var connection = new SqlConnectionStringBuilder(configuration.GetConnectionString("Default") ?? "");
        if (string.IsNullOrWhiteSpace(connection.DataSource) || string.IsNullOrWhiteSpace(connection.InitialCatalog)) throw new InvalidOperationException("A production database connection is required.");
        if (string.Equals(connection.UserID, "sa", StringComparison.OrdinalIgnoreCase) || connection.TrustServerCertificate || connection.Encrypt.Equals(SqlConnectionEncryptOption.Optional)) throw new InvalidOperationException("Production SQL requires a restricted application identity, mandatory encryption and a trusted server certificate.");
        if (configuration.GetValue("Database:Initialize", false) || !string.IsNullOrWhiteSpace(configuration["Bootstrap:Email"])) throw new InvalidOperationException("Run migrations separately; production API bootstrap and schema initialization must be disabled.");
        if (!Uri.TryCreate(configuration["App:PublicUrl"], UriKind.Absolute, out var url) || url.Scheme != "https") throw new InvalidOperationException("App:PublicUrl must be the public HTTPS origin.");
        if (configuration["AllowedHosts"] is null or "*") throw new InvalidOperationException("Configure the deployed hostname in AllowedHosts.");
        if (!configuration.GetValue("Authentication:UseRefreshCookies", false)) throw new InvalidOperationException("Use protected refresh cookies for the deployed browser application.");
        if (!configuration.GetValue("Authentication:RequireAdminMfa", false)) throw new InvalidOperationException("Administrator MFA must be required in staging/production.");
        if (string.IsNullOrWhiteSpace(configuration["Mail:Host"]) || string.IsNullOrWhiteSpace(configuration["Mail:From"])) throw new InvalidOperationException("Configure outgoing account email before production startup.");
        if (string.IsNullOrWhiteSpace(configuration["Authentication:DataProtectionApplicationName"])) throw new InvalidOperationException("A stable data-protection application name is required.");
    }
    public static void ConfigureProxy(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            if (!configuration.GetValue("ReverseProxy:Enabled", false)) return;
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
            options.ForwardLimit = 1;
            var cidr = configuration["ReverseProxy:KnownNetwork"];
            if (string.IsNullOrWhiteSpace(cidr)) throw new InvalidOperationException("Specify the trusted reverse proxy network.");
            options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(cidr));
        });
    }
}
