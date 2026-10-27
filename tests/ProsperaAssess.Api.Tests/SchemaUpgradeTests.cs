using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ProsperaAssess.Api.Tests;

public sealed class SchemaUpgradeTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task AbpUpgradeMigrationMatchesModelAndSupportsResourcePermissions()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AssessDb>();
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('AbpPermissions') AND name IN ('ManagementPermissionName', 'ResourceName')";
        Assert.Equal(2, Convert.ToInt32(await command.ExecuteScalarAsync()));
        command.CommandText = "SELECT COUNT(*) FROM sys.tables WHERE name = 'AbpResourcePermissionGrants'";
        Assert.Equal(1, Convert.ToInt32(await command.ExecuteScalarAsync()));
    }
}
