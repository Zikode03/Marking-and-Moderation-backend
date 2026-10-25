namespace ProsperaAssess;

public class AiTrainingExample : Volo.Abp.Domain.Entities.BasicAggregateRoot<Guid>
{
    public AiTrainingExample(Guid id) : base(id) { }
    protected AiTrainingExample() { }
    public Guid ScriptId { get; set; }
    public string QuestionNumber { get; set; } = "";
    public string Question { get; set; } = "";
    public string Answer { get; set; } = "";
    public string Rubric { get; set; } = "";
    public decimal MaxMarks { get; set; }
    public decimal Marks { get; set; }
    public string LabelledBy { get; set; } = "";
    public string? VerifiedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
