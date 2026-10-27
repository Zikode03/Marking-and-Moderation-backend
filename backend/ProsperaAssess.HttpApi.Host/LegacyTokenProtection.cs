using Microsoft.AspNetCore.DataProtection;
namespace ProsperaAssess.Api;

public static class LegacyTokenProtection
{
    public static void Configure(IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        if (configuration["DataProtection:KeyDirectory"] is { Length: > 0 } keys) services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keys));
        // Moving the host project must not change the protection purposes of installed bearer/refresh tokens.
        services.PostConfigure<DataProtectionOptions>(options =>
        {
            var configured = configuration["Authentication:DataProtectionApplicationName"];
            if (!string.IsNullOrWhiteSpace(configured)) { options.ApplicationDiscriminator = configured; return; }
            var root = environment.ContentRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(Path.GetFileName(root), "ProsperaAssess.HttpApi.Host", StringComparison.OrdinalIgnoreCase))
                options.ApplicationDiscriminator = Path.Combine(Path.GetDirectoryName(root)!, "ProsperaAssess.Api") + Path.DirectorySeparatorChar;
        });
    }
}
