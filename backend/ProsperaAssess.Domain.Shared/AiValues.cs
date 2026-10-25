namespace ProsperaAssess;

public record AiAnnotation(int Page, decimal X, decimal Y, decimal Width, decimal Height, string Kind, string Note);
public record AiQuestion(string Number, decimal MaxMarks, decimal? Marks, string Evidence, string Feedback, bool NeedsReview,
    string? MemoEvidence = null, string? DetectedLanguage = null, decimal? ReadingConfidence = null, List<AiAnnotation>? Annotations = null);
public record AiMarkingResult(bool NeedsAttention, string Summary, List<AiQuestion> Questions);
public record AiHistory(DateTimeOffset At, string Actor, string Action, string Reason, AiMarkingResult? Result);
