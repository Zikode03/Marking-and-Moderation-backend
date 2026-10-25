namespace ProsperaAssess;

public record AssessmentDto(Guid Id, string Title, string Code, decimal TotalMarks, List<Criterion> Criteria, SchoolContext? School, InstitutionContext? Institution);
public record SubmissionDto(Guid Id, Guid AssessmentId, string StudentReference, string AnswerText, string Status, Dictionary<Guid, decimal> Marks, string? Feedback, decimal Total, List<AuditEntry> Audit, int Revision);
public record ScriptDto(Guid Id, Guid BatchId, string Candidate, string FileName, string Status, DateTimeOffset UpdatedAt, AiMarkingResult? Result, decimal? Total, string? Error, string? Model, string? ProviderResponseId, int Revision, List<AiHistory> Audit);
