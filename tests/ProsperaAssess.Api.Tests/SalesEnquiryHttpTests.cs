using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace ProsperaAssess.Api.Tests;

public class SalesEnquiryHttpTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private sealed class CapturingTransport : ISalesMailTransport
    {
        public int Calls { get; private set; }
        public Task<bool> SendAsync(SalesEnquiryInput input) { Calls++; return Task.FromResult(true); }
    }
    [Fact]
    public async Task AnonymousRouteValidatesAndSendsThroughApplicationService()
    {
        var transport = new CapturingTransport();
        using var factory = fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<ISalesMailTransport>(transport)));
        using var client = factory.CreateClient();
        var invalid = await client.PostAsJsonAsync("/api/sales/enquiries", new { name = "Demo", email = "invalid", organisation = "Demo School", requirements = "Marking support" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(0, transport.Calls);
        var response = await client.PostAsJsonAsync("/api/sales/enquiries", new { name = "Demo", email = "teacher@example.org", organisation = "Demo School", requirements = "Marking support" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, transport.Calls);
        for (var i = 0; i < 3; i++) await client.PostAsJsonAsync("/api/sales/enquiries", new { name = "Demo", email = "teacher@example.org", organisation = "Demo School", requirements = "Marking support" });
        var limited = await client.PostAsJsonAsync("/api/sales/enquiries", new { name = "Demo", email = "teacher@example.org", organisation = "Demo School", requirements = "Marking support" });
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }
}
