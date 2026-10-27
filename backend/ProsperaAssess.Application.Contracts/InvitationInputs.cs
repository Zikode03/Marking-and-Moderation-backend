namespace ProsperaAssess;

public record InviteStaffInput(string Email, string Role, int? Grade = null, string? Subject = null, Guid? ModuleId = null);
public record AcceptInvitationInput(string Token, string Password);
public interface IInvitationAppService
{
    Task<OperationResult> CreateAsync(InviteStaffInput input);
    Task<OperationResult> SendEmailAsync(SendInvitationEmailInput input);
    Task<OperationResult> AcceptAsync(AcceptInvitationInput input);
}

public record SendInvitationEmailInput(string Token);
public interface IInvitationEmailSender { Task<bool> SendAsync(string email, string invitationPath); }
