using System.Net;
using System.Net.Mail;
namespace ProsperaAssess.Api;

public interface IAccountEmailSender { Task<bool> SendAsync(string recipient, string subject, string body); }
public sealed class AccountEmailSender(IConfiguration configuration, ILogger<AccountEmailSender> logger, OperationsTelemetry telemetry) : IAccountEmailSender
{
    public async Task<bool> SendAsync(string recipient, string subject, string body)
    {
        string? Setting(string name) => configuration[$"Mail:{name}"] ?? configuration[$"SalesMail:{name}"];
        if (string.IsNullOrWhiteSpace(Setting("Host")) || string.IsNullOrWhiteSpace(Setting("From"))) { logger.LogWarning("Account email is not configured."); telemetry.Mail(false); return false; }
        try
        {
            using var message = new MailMessage(Setting("From")!, recipient) { Subject = subject, Body = body, IsBodyHtml = false, BodyEncoding = System.Text.Encoding.UTF8 };
            using var smtp = new SmtpClient(Setting("Host"), int.TryParse(Setting("Port"), out var port) ? port : 587) { EnableSsl = true, UseDefaultCredentials = false };
            if (!string.IsNullOrWhiteSpace(Setting("Username"))) smtp.Credentials = new NetworkCredential(Setting("Username"), Setting("Password"));
            await smtp.SendMailAsync(message).WaitAsync(TimeSpan.FromSeconds(15)); telemetry.Mail(true); return true;
        }
        catch (Exception ex) when (ex is SmtpException or FormatException or InvalidOperationException or TimeoutException)
        { logger.LogWarning("Account email delivery failed ({FailureType}).", ex.GetType().Name); telemetry.Mail(false); return false; }
    }
}

public sealed class InvitationEmailSender(IConfiguration configuration, IAccountEmailSender mail) : ProsperaAssess.IInvitationEmailSender
{
    public Task<bool> SendAsync(string email, string invitationPath)
    {
        var origin = configuration["App:PublicUrl"];
        if (!Uri.TryCreate(origin, UriKind.Absolute, out _)) return Task.FromResult(false);
        return mail.SendAsync(email, "Your Prospera Assess staff invitation", "Your organisation invited you to Prospera Assess. Choose your password using this private link (valid for seven days):\n\n" + origin.TrimEnd('/') + invitationPath + "\n\nIf you did not expect this invitation, contact your organisation.");
    }
}
