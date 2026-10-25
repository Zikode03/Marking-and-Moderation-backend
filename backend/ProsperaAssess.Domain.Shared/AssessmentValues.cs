using System.Text.Json;
namespace ProsperaAssess;

public static class AppJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);
}
public record Criterion(Guid Id, string Name, decimal MaxMarks);
public record SchoolContext(string Subject, int Grade, int Term, string AssessmentType, string SchoolType = "Independent", string Pathway = "IEB", decimal WeightPercent = 0);
public record InstitutionContext(Guid FacultyId, string FacultyName, Guid ModuleId, string ModuleCode, string ModuleName, int AcademicYear, int Semester, string AssessmentType, decimal WeightPercent);
// Marker and Moderator remain optional for older clients. Only the authenticated
// account determines the actor recorded in the audit history.
public record AuditEntry(DateTimeOffset At, string Actor, string Action, string Notes, Dictionary<Guid, decimal>? Marks = null, string? Feedback = null, string? Status = null);