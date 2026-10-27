using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Services;
namespace ProsperaAssess;

public sealed class SalesEnquiryInput
{
    [Required, StringLength(120)] public string Name { get; set; } = "";
    [Required, EmailAddress, StringLength(180)] public string Email { get; set; } = "";
    [Required, StringLength(180)] public string Organisation { get; set; } = "";
    [Required, StringLength(3000)] public string Requirements { get; set; } = "";
}
public interface ISalesEnquiryAppService : IApplicationService
{
    Task<bool> SendAsync(SalesEnquiryInput input);
}
