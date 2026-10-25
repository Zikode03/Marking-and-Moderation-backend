namespace ProsperaAssess;

public static class AiMarkingPolicy
{
    public static void Validate(AiMarkingResult result, bool requireMemoEvidence = false)
    {
        if (result.Questions is null || result.Questions.Count is < 1 or > 300 || string.IsNullOrWhiteSpace(result.Summary) || result.Questions.Select(q => q.Number).Distinct(StringComparer.OrdinalIgnoreCase).Count() != result.Questions.Count)
            throw new InvalidOperationException("The model returned an incomplete question breakdown; no marks were accepted.");
        foreach (var q in result.Questions)
        {
            if (string.IsNullOrWhiteSpace(q.Number) || q.MaxMarks <= 0 || q.Marks < 0 || q.Marks > q.MaxMarks || string.IsNullOrWhiteSpace(q.Feedback) || q.Evidence is null || (q.Marks is null && !q.NeedsReview) || (q.NeedsReview && !result.NeedsAttention))
                throw new InvalidOperationException("The model returned invalid question marks; no marks were accepted.");
            if (requireMemoEvidence && (string.IsNullOrWhiteSpace(q.MemoEvidence) || q.DetectedLanguage is null || !(MarkingLanguages.Codes.Contains(q.DetectedLanguage) || q.DetectedLanguage is "mixed" or "und") || q.ReadingConfidence is null))
                throw new InvalidOperationException("The model must link each mark to the uploaded memorandum and report reading language/confidence.");
            if (q.ReadingConfidence is < 0 or > 1 || ((q.ReadingConfidence < 0.85m || q.DetectedLanguage is "und" or "mixed") && !q.NeedsReview))
                throw new InvalidOperationException("Uncertain handwriting or language must be flagged for academic review.");
            foreach (var a in q.Annotations ?? [])
                if (a.Page is < 1 or > 1000 || a.X < 0 || a.Y < 0 || a.Width <= 0 || a.Height <= 0 || a.X + a.Width > 1 || a.Y + a.Height > 1 || !MarkingLanguages.ReviewKinds.Contains(a.Kind) || (string.IsNullOrWhiteSpace(a.Note) || a.Note.Length > 5000) || !q.NeedsReview)
                    throw new InvalidOperationException("Invalid script review annotation; no marks were accepted.");
        }
    }
}