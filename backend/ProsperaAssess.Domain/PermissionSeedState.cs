namespace ProsperaAssess;

public class PermissionSeedState : Volo.Abp.Domain.Entities.BasicAggregateRoot<Guid>
{
    public string Version { get; protected set; } = "";
    protected PermissionSeedState() { }
    public PermissionSeedState(Guid id, string version) : base(id) => Version = version;
}
