namespace ProsperaAssess;

internal sealed class UploadCollection(List<UploadFileDto> files)
{
    public List<UploadFileDto> Files => files;
    public UploadFileDto? GetFile(string key) => files.FirstOrDefault(f => f.Name == key);
    public List<UploadFileDto> GetFiles(string key) => files.Where(f => f.Name == key).ToList();
}
