namespace ProsperaAssess;

public static class MarkingLanguages
{
    public static readonly string[] Codes = ["af", "en", "nr", "nso", "st", "ss", "tn", "ts", "ve", "xh", "zu"];
    public static bool Valid(string? code) => code is "auto" or "mixed" || Codes.Contains(code);
    public static readonly string[] ReviewKinds = ["uncertain_handwriting", "alternative_answer", "possible_harsh_marking", "memo_ambiguity", "missing_page"];
}
