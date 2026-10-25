namespace ProsperaAssess;

public record CreateFaculty(string? Name, string? Code);
public record CreateModule(Guid FacultyId, string? Name, string? Code);
public record CreateAllocation(string? UserId, int? Grade, string? Subject, Guid? ModuleId);
