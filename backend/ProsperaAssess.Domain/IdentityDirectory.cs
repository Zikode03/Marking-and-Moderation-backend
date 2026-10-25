using System.Security.Claims;
namespace ProsperaAssess;

public sealed class StaffAccount { public string Id { get; set; } = Guid.NewGuid().ToString(); public string? Email { get; set; } public string? UserName { get; set; } public bool EmailConfirmed { get; set; } public Guid SchoolId { get; set; } }
public record AccountError(string Description);
public record AccountOperation(bool Succeeded, List<AccountError> Errors);
public interface IStaffIdentityService
{
    Task<StaffAccount?> GetUserAsync(ClaimsPrincipal principal); Task<StaffAccount?> FindByEmailAsync(string email); Task<StaffAccount?> FindByIdAsync(string id);
    Task<IList<string>> GetRolesAsync(StaffAccount user); Task<bool> IsInRoleAsync(StaffAccount user, string role);
    Task<List<StaffAccount>> GetUsersAsync(Guid schoolId); Task<AccountOperation> CreateAsync(StaffAccount user, string password); Task<AccountOperation> AddToRoleAsync(StaffAccount user, string role);
}
