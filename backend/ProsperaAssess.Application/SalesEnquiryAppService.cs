using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Auditing;
using Volo.Abp.DependencyInjection;
namespace ProsperaAssess;

public interface ISalesMailTransport
{
    Task<bool> SendAsync(SalesEnquiryInput input);
}

[AllowAnonymous, DisableAuditing, RemoteService(false)]
public class SalesEnquiryAppService(ISalesMailTransport transport) : ApplicationService, ISalesEnquiryAppService
{
    public virtual Task<bool> SendAsync(SalesEnquiryInput input)
    {
        Validator.ValidateObject(input, new ValidationContext(input), validateAllProperties: true);
        return transport.SendAsync(input);
    }
}

public sealed class SmtpSalesMailTransport(IConfiguration configuration, ILogger<SmtpSalesMailTransport> logger) : ISalesMailTransport, ITransientDependency
{
    public async Task<bool> SendAsync(SalesEnquiryInput input)
    {
        var host = (configuration["Mail:Host"] ?? configuration["SalesMail:Host"]);
        var from = (configuration["Mail:From"] ?? configuration["SalesMail:From"]);
        var recipient = configuration["SalesMail:Recipient"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(recipient))
        {
            logger.LogWarning("Sales email delivery is not configured.");
            return false;
        }
        try
        {
            using var message = new MailMessage(new MailAddress(from), new MailAddress(recipient))
            {
                Subject = "Prospera Assess sales enquiry",
                Body = $"Name: {input.Name.Trim()}\nEmail: {input.Email.Trim()}\nOrganisation: {input.Organisation.Trim()}\n\nRequirements:\n{input.Requirements.Trim()}",
                IsBodyHtml = false,
                BodyEncoding = System.Text.Encoding.UTF8
            };
            message.ReplyToList.Add(new MailAddress(input.Email.Trim()));
            using var smtp = new SmtpClient(host, configuration.GetValue("Mail:Port", configuration.GetValue("SalesMail:Port", 587)))
            {
                EnableSsl = true,
                UseDefaultCredentials = false,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };
            var username = (configuration["Mail:Username"] ?? configuration["SalesMail:Username"]);
            if (!string.IsNullOrWhiteSpace(username))
                smtp.Credentials = new NetworkCredential(username, (configuration["Mail:Password"] ?? configuration["SalesMail:Password"]));
            await smtp.SendMailAsync(message).WaitAsync(TimeSpan.FromSeconds(15));
            return true;
        }
        catch (Exception exception) when (exception is SmtpException or FormatException or InvalidOperationException or TimeoutException)
        {
            // Do not log the body, personal details or SMTP credentials.
            logger.LogWarning("Sales email delivery failed ({FailureType}).", exception.GetType().Name);
            return false;
        }
    }
}
