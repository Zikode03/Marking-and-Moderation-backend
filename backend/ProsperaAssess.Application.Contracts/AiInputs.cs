namespace ProsperaAssess;

public record AiUploadContext(string? Title, SchoolContext? School, InstitutionInput? Institution, string ScriptLanguage = "auto", string MemoLanguage = "auto");
public record AiReview(int Revision, string? Reason, Dictionary<string, decimal>? Marks);
public record UploadFileDto(string Name, string FileName, byte[] Content) { public long Length => Content.LongLength; }
public record UploadBatchDto(AiUploadContext Context, List<UploadFileDto> Files);