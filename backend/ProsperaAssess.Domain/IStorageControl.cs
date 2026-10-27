namespace ProsperaAssess;

public interface IUploadSafety
{
    Task CheckAsync(byte[] content, CancellationToken ct);
}
public interface IStorageControl
{
    Task LockAsync(Guid schoolId, CancellationToken ct = default);
    Task<long> UsedBytesAsync(Guid schoolId, CancellationToken ct = default);
    long QuotaBytes { get; }
}
