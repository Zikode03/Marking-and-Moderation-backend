namespace ProsperaAssess;

public class FacultyRow : Volo.Abp.Domain.Entities.BasicAggregateRoot<Guid>
{
    public FacultyRow(Guid id) : base(id) { }
    protected FacultyRow() { }
    public Guid SchoolId { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
}
public class ModuleRow : Volo.Abp.Domain.Entities.BasicAggregateRoot<Guid>
{
    public ModuleRow(Guid id) : base(id) { }
    protected ModuleRow() { }
    public Guid SchoolId { get; set; }
    public Guid FacultyId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}
public class StaffAllocationRow : Volo.Abp.Domain.Entities.BasicAggregateRoot<Guid>
{
    public StaffAllocationRow(Guid id) : base(id) { }
    protected StaffAllocationRow() { }
    public Guid SchoolId { get; set; }
    public string UserId { get; set; } = "";
    public int? Grade { get; set; }
    public string? Subject { get; set; }
    public Guid? ModuleId { get; set; }
}
