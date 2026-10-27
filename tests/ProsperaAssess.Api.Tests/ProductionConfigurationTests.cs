using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using ProsperaAssess.Api;
using Xunit;
namespace ProsperaAssess.Api.Tests;
public class ProductionConfigurationTests
{
    private sealed class Environment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Staging;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
    private static Dictionary<string,string?> Settings() => new()
    {
        ["ConnectionStrings:Default"] = "Server=sql.example;Database=ProsperaAssess;User Id=prospera_runtime;Password=Example!Only123;Encrypt=True;TrustServerCertificate=False",
        ["App:PublicUrl"] = "https://staging.example.org",
        ["AllowedHosts"] = "staging.example.org",
        ["Authentication:DataProtectionApplicationName"] = "ProsperaAssess",
        ["Authentication:UseRefreshCookies"] = "true",
        ["Authentication:RequireAdminMfa"] = "true",
        ["Monitoring:ApiKey"] = "SyntheticMonitoringKeyWithAtLeast32Characters",
        ["Uploads:ScannerHost"] = "clamav",
        ["Storage:RequireEncryptionAtRest"] = "true",
        ["Storage:TenantQuotaBytes"] = "10737418240",
        ["Mail:Host"] = "smtp.example.org",
        ["Mail:From"] = "staff@example.org"
    };
    [Fact]
    public void AcceptsExplicitProductionConfigurationWithoutConnectingToTheDatabase() => ProductionConfiguration.Validate(new ConfigurationBuilder().AddInMemoryCollection(Settings()).Build(), new Environment());
    [Theory]
    [InlineData("Monitoring:ApiKey", "short")]
    [InlineData("Uploads:ScannerHost", "")]
    [InlineData("Storage:RequireEncryptionAtRest", "false")]
    [InlineData("Storage:TenantQuotaBytes", "0")]
    [InlineData("Database:Initialize", "true")]
    [InlineData("Bootstrap:Email", "bootstrap@example.org")]
    [InlineData("AllowedHosts", "*")]
    [InlineData("App:PublicUrl", "http://example.org")]
    [InlineData("Authentication:UseRefreshCookies", "false")]
    [InlineData("Authentication:RequireAdminMfa", "false")]
    [InlineData("Authentication:DataProtectionApplicationName", "")]
    [InlineData("Mail:Host", "")]
    [InlineData("ConnectionStrings:Default", "Server=sql.example;Database=ProsperaAssess;User Id=sa;Encrypt=True;TrustServerCertificate=False")]
    [InlineData("ConnectionStrings:Default", "Server=sql.example;Database=ProsperaAssess;User Id=prospera_runtime;Encrypt=True;TrustServerCertificate=True")]
    public void RejectsUnsafeProductionDefaults(string key, string value)
    {
        var settings = Settings(); settings[key] = value;
        Assert.Throws<InvalidOperationException>(() => ProductionConfiguration.Validate(new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), new Environment()));
    }
}
