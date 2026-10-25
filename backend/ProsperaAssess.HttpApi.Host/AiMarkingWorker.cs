using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;
using ProsperaAssess;
namespace ProsperaAssess.Api;

public class AiMarkingWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly CancellationTokenSource stopping = new();
    public AiMarkingWorker(AbpAsyncTimer timer, IServiceScopeFactory scopes) : base(timer, scopes) => Timer.Period = 3000;
    public override async Task StopAsync(CancellationToken cancellationToken = default) { stopping.Cancel(); await base.StopAsync(cancellationToken); }
    protected override Task DoWorkAsync(PeriodicBackgroundWorkerContext context) => context.ServiceProvider.GetRequiredService<MarkingQueueProcessor>().RunOnceAsync(stopping.Token);
}
