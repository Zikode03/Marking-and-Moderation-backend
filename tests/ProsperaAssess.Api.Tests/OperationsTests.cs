using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Xunit;
namespace ProsperaAssess.Api.Tests;
public sealed class OperationsTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task MonitoringRequiresSeparateCredentialAndExposesNoLearnerData()
    {
        const string key="SyntheticTestMonitoringCredentialOver32Characters";
        using var factory=fixture.Factory.WithWebHostBuilder(builder=>builder.ConfigureAppConfiguration((_,configuration)=>configuration.AddInMemoryCollection(new Dictionary<string,string?>{{"Monitoring:ApiKey",key}})));
        using var client=factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/api/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/operations/metrics")).StatusCode);
        client.DefaultRequestHeaders.Add("X-Prospera-Monitor-Key","wrong");
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/operations/metrics")).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Prospera-Monitor-Key");client.DefaultRequestHeaders.Add("X-Prospera-Monitor-Key",key);
        var response=await client.GetAsync("/api/operations/metrics"); Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var text=await response.Content.ReadAsStringAsync(); var metrics=JsonDocument.Parse(text).RootElement;
        Assert.Equal("available",metrics.GetProperty("database").GetString()); Assert.True(metrics.GetProperty("process").GetProperty("requests").GetInt64()>=1);
        Assert.DoesNotContain("candidate",text,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain("email",text,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain(key,text);Assert.Equal("no-store",response.Headers.CacheControl!.ToString());
    }
}
