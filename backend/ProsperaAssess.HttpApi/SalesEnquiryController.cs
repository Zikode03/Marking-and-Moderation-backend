using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Auditing;
namespace ProsperaAssess;

[ApiController, AllowAnonymous, DisableAuditing, EnableRateLimiting("sales")]
public class SalesEnquiryController(ISalesEnquiryAppService service) : AbpController
{
    [HttpPost("/api/sales/enquiries")]
    [RequestSizeLimit(16384)]
    public async Task<IActionResult> Send([FromBody] SalesEnquiryInput input)
    {
        if (!await service.SendAsync(input))
            return StatusCode(503, new { error = "We could not send your enquiry. Please try again or email prosperacore.tech@gmail.com." });
        return Ok(new { sent = true });
    }
}
