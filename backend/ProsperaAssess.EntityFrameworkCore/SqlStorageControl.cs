using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;

namespace ProsperaAssess;

public sealed class SqlStorageControl(IDbContextProvider<AssessDb> provider, IConfiguration configuration) : IStorageControl, ITransientDependency
{
    public long QuotaBytes => configuration.GetValue<long?>("Storage:TenantQuotaBytes") ?? 10L * 1024 * 1024 * 1024;
    public async Task LockAsync(Guid schoolId, CancellationToken ct = default)
    {
        var db = await provider.GetDbContextAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r = sp_getapplock @Resource={"prospera-storage-" + schoolId}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @r < 0 THROW 51000, 'Storage is busy; retry the operation.', 1;", ct);
    }
    public async Task<long> UsedBytesAsync(Guid schoolId, CancellationToken ct = default)
    {
        var db = await provider.GetDbContextAsync();
        await db.Database.OpenConnectionAsync(ct);
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = "SELECT COALESCE((SELECT SUM(CONVERT(bigint,DATALENGTH(b.Paper))+CONVERT(bigint,DATALENGTH(b.Memo))) FROM AiBatches b JOIN Assessments a ON a.Id=b.AssessmentId WHERE a.SchoolId=@school),0)+COALESCE((SELECT SUM(CONVERT(bigint,DATALENGTH(s.Content))) FROM AiScripts s JOIN AiBatches b ON b.Id=s.BatchId JOIN Assessments a ON a.Id=b.AssessmentId WHERE a.SchoolId=@school),0)";
        var parameter = command.CreateParameter(); parameter.ParameterName = "@school"; parameter.Value = schoolId; command.Parameters.Add(parameter);
        return Convert.ToInt64(await command.ExecuteScalarAsync(ct));
    }
}
