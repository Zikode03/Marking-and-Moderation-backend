namespace ProsperaAssess;

public record ManagedRecordInput(string? Code, string? Name, Dictionary<string, string>? Fields, int Revision = 0, bool Active = true);
public record AssignmentInput(string TeacherId, string ModeratorId, DateTimeOffset? DueAt, int Revision = 0);
public interface IInstitutionAppService
{
    Task<OperationResult> GetAsync(string kind);
    Task<OperationResult> SaveAsync(string kind, Guid? id, ManagedRecordInput input);
    Task<OperationResult> ImportAsync(string kind, List<ManagedRecordInput> records);
    Task<OperationResult> SampleAsync(int percentage);
    Task<OperationResult> AuditAsync(string? search);
    Task<OperationResult> TasksAsync();
    Task<OperationResult> ReportAsync(string? candidate);
    Task<OperationResult> StaffAsync();
    Task<OperationResult> SetStaffAsync(string id, StaffStatusInput input);
    Task<OperationResult> DocumentsAsync();
    Task<OperationResult> ApproveMemoAsync(Guid batchId);
    Task<OperationResult> AssignAsync(Guid scriptId, AssignmentInput input);
}
public record StaffStatusInput(bool Active, string[] Roles);
