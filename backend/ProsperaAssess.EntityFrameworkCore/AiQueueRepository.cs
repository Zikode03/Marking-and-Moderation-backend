using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
namespace ProsperaAssess;

public class AiQueueRepository(IDbContextProvider<AssessDb> contexts) : EfCoreRepository<AssessDb, AiScript, Guid>(contexts), IAiQueueRepository
{
    public async Task RecoverInterruptedAsync(DateTimeOffset cutoff, CancellationToken ct) { var db = await GetDbContextAsync(); await db.AiScripts.Where(s => s.Status == "Processing" && s.UpdatedAt < cutoff).ExecuteUpdateAsync(set => set.SetProperty(s => s.Status, "Failed").SetProperty(s => s.Error, "Processing was interrupted. Retry this batch.").SetProperty(s => s.Revision, s => s.Revision + 1), ct); }
    public async Task<AiScript?> ClaimNextAsync(CancellationToken ct) { var db = await GetDbContextAsync(); var id = await db.AiScripts.Where(s => s.Status == "Queued").OrderBy(s => s.UpdatedAt).Select(s => (Guid?)s.Id).FirstOrDefaultAsync(ct); if (id is null) return null; var now = DateTimeOffset.UtcNow; if (await db.AiScripts.Where(s => s.Id == id && s.Status == "Queued").ExecuteUpdateAsync(set => set.SetProperty(s => s.Status, "Processing").SetProperty(s => s.UpdatedAt, now).SetProperty(s => s.Revision, s => s.Revision + 1), ct) != 1) return null; return await db.AiScripts.FirstAsync(s => s.Id == id, ct); }
}
