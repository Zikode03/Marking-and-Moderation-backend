namespace ProsperaAssess;

public record TrainingLabel(string? QuestionNumber, string? Question, string? Answer, string? Rubric, decimal MaxMarks, decimal Marks, bool SourceMarked = false, bool LearnerWritingOnly = false, string? PreparationNotes = null, string? DatasetScope = null, string? LearnerGroup = null);
