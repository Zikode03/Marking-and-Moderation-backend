namespace ProsperaAssess;

public record LoginRequest(string? Email, string? Password);
public record RefreshRequest(string? RefreshToken);
public record CreateStaffRequest(string? Email, string? Password, string? Role, int? Grade = null, string? Subject = null, Guid? ModuleId = null);
