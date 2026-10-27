namespace ProsperaAssess;

// Stable identifiers preserve references across renaming and archival.
public class InstitutionRecord : Volo.Abp.Domain.Entities.BasicAggregateRoot<Guid>
{
    public InstitutionRecord(Guid id) : base(id) { }
    protected InstitutionRecord() { }
    public Guid SchoolId { get; set; }
    public string Kind { get; set; } = "";
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string DataJson { get; set; } = "{}";
    public bool Active { get; set; } = true;
    public int Revision { get; set; }
}
public class InstitutionEvent : Volo.Abp.Domain.Entities.BasicAggregateRoot<Guid>
{
    public InstitutionEvent(Guid id) : base(id) { }
    protected InstitutionEvent() { }
    public Guid SchoolId { get; set; }
    public Guid RecordId { get; set; }
    public string Actor { get; set; } = "";
    public string Action { get; set; } = "";
    public string BeforeJson { get; set; } = "";
    public string AfterJson { get; set; } = "";
    public DateTimeOffset At { get; set; }
}
