using Volo.Abp;
using Volo.Abp.Uow;
namespace ProsperaAssess;

[RemoteService(false)]
public class MarkingQueueProcessor(IAiQueueRepository queue) : ProsperaAppService
{
    [UnitOfWork(false)]
    public virtual async Task RunOnceAsync(CancellationToken ct)
    {
        await queue.RecoverInterruptedAsync(DateTimeOffset.UtcNow.AddMinutes(-15), ct);
        if (!(await marker.Health(ct)).Ready) return;
        var script = await queue.ClaimNextAsync(ct);
        if (script is null) return;
        try
        {
            var batch = await AiBatchesRepository.GetAsync(script.BatchId, cancellationToken: ct);
            var assessment = await AssessmentsRepository.GetAsync(batch.AssessmentId, cancellationToken: ct);
            var context = AppJson.Write(new { school = assessment.SchoolContext(), institution = assessment.InstitutionContext() });
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromMinutes(10));
            var result = await marker.Mark(batch, script, timeout.Token, context);
            script.RecordAiResult(result);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            script.FailMarking(ex is InvalidOperationException ? ex.Message : "The local AI service could not complete marking. Check the service, then retry.");
        }
        await AiScriptsRepository.UpdateAsync(script, cancellationToken: ct);
        await UnitOfWorkManager.Current!.SaveChangesAsync(ct);
    }
}
