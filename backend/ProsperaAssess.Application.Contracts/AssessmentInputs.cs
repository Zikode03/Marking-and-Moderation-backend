namespace ProsperaAssess;

public record CriterionInput(string Name, decimal MaxMarks);
public record CreateAssessment(string? Title, string? Code, List<CriterionInput?>? Criteria, SchoolContext? School, InstitutionInput? Institution = null);
public record InstitutionInput(Guid ModuleId, int AcademicYear, int Semester, string AssessmentType, decimal WeightPercent = 0);
public record CreateSubmission(Guid AssessmentId, string? StudentReference, string? AnswerText);
public record MarkRequest(string? Marker, Dictionary<Guid, decimal>? Marks, string? Feedback, int? Revision = null);
public record ModerationRequest(string? Moderator, bool? Approve, string? Comments, int? Revision = null);