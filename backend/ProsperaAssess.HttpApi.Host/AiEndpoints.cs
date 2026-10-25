using ProsperaAssess;
namespace ProsperaAssess.Api;

public static class AiEndpoints { public static string? Detect(byte[] data, string name) => ScriptBatchAppService.Detect(data, name); }