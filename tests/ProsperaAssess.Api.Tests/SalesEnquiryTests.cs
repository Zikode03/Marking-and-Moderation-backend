using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
namespace ProsperaAssess.Api.Tests;

public class SalesEnquiryTests
{
    private static SalesEnquiryInput Valid() => new() { Name = "Demo teacher", Email = "teacher@example.org", Organisation = "Demo School", Requirements = "Grade 12 marking support" };
    private sealed class Transport(bool result) : ISalesMailTransport
    {
        public int Calls { get; private set; }
        public Task<bool> SendAsync(SalesEnquiryInput input) { Calls++; return Task.FromResult(result); }
    }
    [Fact]
    public async Task ConfirmsOnlyWhenTransportAcceptsMail()
    {
        var transport = new Transport(true);
        var controller = new SalesEnquiryController(new SalesEnquiryAppService(transport));
        Assert.IsType<OkObjectResult>(await controller.Send(Valid()));
        Assert.Equal(1, transport.Calls);
    }
    [Fact]
    public async Task ReportsUnavailableWhenTransportFails()
    {
        var controller = new SalesEnquiryController(new SalesEnquiryAppService(new Transport(false)));
        Assert.Equal(503, Assert.IsType<ObjectResult>(await controller.Send(Valid())).StatusCode);
    }
    [Fact]
    public async Task InvalidEmailNeverReachesTransport()
    {
        var transport = new Transport(true);
        var input = Valid(); input.Email = "invalid";
        await Assert.ThrowsAsync<ValidationException>(() => new SalesEnquiryAppService(transport).SendAsync(input));
        Assert.Equal(0, transport.Calls);
    }
    [Fact]
    public async Task MissingConfigurationNeverClaimsMailWasSent()
    {
        var transport = new SmtpSalesMailTransport(new ConfigurationBuilder().Build(), NullLogger<SmtpSalesMailTransport>.Instance);
        Assert.False(await transport.SendAsync(Valid()));
    }
}
