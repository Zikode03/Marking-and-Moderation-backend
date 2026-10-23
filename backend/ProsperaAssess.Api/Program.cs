using System.Collections.Concurrent;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5080");
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddCors(o => o.AddPolicy("dev", p => p.WithOrigins("http://localhost:5173").AllowAnyHeader().AllowAnyMethod()));
var app = builder.Build();
app.UseCors("dev");

var assessments = new ConcurrentDictionary<Guid, Assessment>();
var submissions = new ConcurrentDictionary<Guid, Submission>();
var sample = new Assessment(Guid.NewGuid(),"Business Management — Formative Assessment","BMAN101",50,
  [new Criterion(Guid.NewGuid(),"Conceptual understanding",20),new Criterion(Guid.NewGuid(),"Practical application",20),new Criterion(Guid.NewGuid(),"Structure and clarity",10)]);
assessments[sample.Id]=sample;
var demo = new Submission(Guid.NewGuid(),sample.Id,"STU-001","A sample submission for demonstration only.",WorkflowStatus.Submitted,new Dictionary<Guid,decimal>(),null,new List<AuditEntry>());
submissions[demo.Id]=demo;

app.MapGet("/api/health", () => Results.Ok(new { status="ok", service="Prospera Assess API", persistence="in-memory-demo", ai="not-trained" }));
app.MapGet("/api/assessments", () => Results.Ok(assessments.Values.OrderBy(x=>x.Title)));
app.MapGet("/api/schools/grades", () => Results.Ok(Enumerable.Range(8,5)));
app.MapGet("/api/schools/assessments", () => Results.Ok(assessments.Values.Where(a=>a.School is not null).OrderBy(a=>a.School!.Grade).ThenBy(a=>a.School!.Subject)));
app.MapGet("/api/assessments/{id:guid}", (Guid id) => assessments.TryGetValue(id,out var a)?Results.Ok(a):Results.NotFound());
app.MapPost("/api/assessments", (CreateAssessment req) => {
 if(req.School is not null && (req.School.Grade<8||req.School.Grade>12||req.School.Term<1||req.School.Term>4||string.IsNullOrWhiteSpace(req.School.Subject)||string.IsNullOrWhiteSpace(req.School.AssessmentType)||!new[]{"IEB","DBE-NSC","Other"}.Contains(req.School.Pathway)||!new[]{"Independent","Public"}.Contains(req.School.SchoolType))) return Results.BadRequest(new {error="School assessments require a subject, assessment type, grade 8–12 and term 1–4."});
 if(string.IsNullOrWhiteSpace(req.Title)||string.IsNullOrWhiteSpace(req.Code)||req.Criteria is null||req.Criteria.Count==0||req.Criteria.Any(c=>c.MaxMarks<=0))return Results.BadRequest(new {error="Provide title, code and positive rubric criteria."});
 var a=new Assessment(Guid.NewGuid(),req.Title.Trim(),req.Code.Trim(),req.Criteria.Sum(c=>c.MaxMarks),req.Criteria.Select(c=>new Criterion(Guid.NewGuid(),c.Name,c.MaxMarks)).ToList(),req.School);
 assessments[a.Id]=a;return Results.Created($"/api/assessments/{a.Id}",a);
});
app.MapGet("/api/submissions", (Guid? assessmentId) => Results.Ok(submissions.Values.Where(s=>assessmentId is null||s.AssessmentId==assessmentId).OrderBy(s=>s.StudentReference)));
app.MapPost("/api/submissions", (CreateSubmission req) => {
 if(!assessments.ContainsKey(req.AssessmentId))return Results.BadRequest(new {error="Assessment does not exist."});
 if(string.IsNullOrWhiteSpace(req.StudentReference)||string.IsNullOrWhiteSpace(req.AnswerText))return Results.BadRequest(new {error="Student reference and answer required."});
 var s=new Submission(Guid.NewGuid(),req.AssessmentId,req.StudentReference,req.AnswerText,WorkflowStatus.Submitted,new Dictionary<Guid,decimal>(),null,new List<AuditEntry>());
 submissions[s.Id]=s;return Results.Created($"/api/submissions/{s.Id}",s);
});
app.MapGet("/api/submissions/{id:guid}",(Guid id)=>submissions.TryGetValue(id,out var s)?Results.Ok(s):Results.NotFound());
app.MapPost("/api/submissions/{id:guid}/mark",(Guid id,MarkRequest req)=>{
 if(!submissions.TryGetValue(id,out var s)||!assessments.TryGetValue(s.AssessmentId,out var a))return Results.NotFound();
 if(s.Status is WorkflowStatus.Approved)return Results.Conflict(new {error="Approved marks cannot be changed."});
 if(string.IsNullOrWhiteSpace(req.Marker)||req.Marks is null ||req.Marks.Count!=a.Criteria.Count)return Results.BadRequest(new {error="Marker and a mark for every rubric criterion are required."});
 if(a.Criteria.Any(c=>!req.Marks.TryGetValue(c.Id,out var mark)||mark<0||mark>c.MaxMarks))return Results.BadRequest(new {error="Each rubric mark must be within its maximum."});
 s.Marks=new Dictionary<Guid,decimal>(req.Marks);s.Status=WorkflowStatus.Marked;s.Audit.Add(new AuditEntry(DateTimeOffset.UtcNow,req.Marker,"Marked",req.Feedback??""));
 s.Feedback=req.Feedback;return Results.Ok(s);
});
app.MapPost("/api/submissions/{id:guid}/moderate",(Guid id,ModerationRequest req)=>{
 if(!submissions.TryGetValue(id,out var s))return Results.NotFound();
 if(s.Status!=WorkflowStatus.Marked)return Results.Conflict(new {error="Submission must be marked before moderation."});
 if(string.IsNullOrWhiteSpace(req.Moderator)||string.IsNullOrWhiteSpace(req.Comments))return Results.BadRequest(new {error="Moderator and comments required."});
 s.Status=req.Approve?WorkflowStatus.Moderated:WorkflowStatus.Submitted;
 s.Audit.Add(new AuditEntry(DateTimeOffset.UtcNow,req.Moderator,req.Approve?"Moderated":"Returned for remarking",req.Comments));
 return Results.Ok(s);
});
app.MapPost("/api/submissions/{id:guid}/approve",(Guid id,ApprovalRequest req)=>{
 if(!submissions.TryGetValue(id,out var s))return Results.NotFound();
 if(s.Status!=WorkflowStatus.Moderated)return Results.Conflict(new {error="Academic approval requires moderation."});
 if(string.IsNullOrWhiteSpace(req.Approver))return Results.BadRequest(new {error="Approver required."});
 s.Status=WorkflowStatus.Approved;s.Audit.Add(new AuditEntry(DateTimeOffset.UtcNow,req.Approver,"Approved","Final result approved"));
 return Results.Ok(s);
});
app.MapPost("/api/submissions/{id:guid}/ai-suggestion",(Guid id)=>{
 if(!submissions.ContainsKey(id))return Results.NotFound();
 return Results.Problem("No validated Prospera-trained model has been deployed. Human marking remains available.",statusCode:503);
});
app.Run();

record Criterion(Guid Id,string Name,decimal MaxMarks);
record SchoolContext(string Subject,int Grade,int Term,string AssessmentType,string SchoolType="Independent",string Pathway="IEB");
record Assessment(Guid Id,string Title,string Code,decimal TotalMarks,List<Criterion> Criteria,SchoolContext? School=null);
enum WorkflowStatus { Submitted,Marked,Moderated,Approved }
class Submission(Guid id,Guid assessmentId,string studentReference,string answerText,WorkflowStatus status,Dictionary<Guid,decimal> marks,string? feedback,List<AuditEntry> audit) {
 public Guid Id {get;}=id; public Guid AssessmentId {get;}=assessmentId;
 public string StudentReference {get;}=studentReference;public string AnswerText {get;}=answerText;
 public WorkflowStatus Status {get;set;}=status;public Dictionary<Guid,decimal> Marks {get;set;}=marks;
 public string? Feedback {get;set;}=feedback;public List<AuditEntry> Audit {get;}=audit;
 public decimal Total => Marks.Values.Sum();
}
record AuditEntry(DateTimeOffset At,string Actor,string Action,string Notes);
record CriterionInput(string Name,decimal MaxMarks);
record CreateAssessment(string Title,string Code,List<CriterionInput> Criteria,SchoolContext? School=null);
record CreateSubmission(Guid AssessmentId,string StudentReference,string AnswerText);
record MarkRequest(string Marker,Dictionary<Guid,decimal> Marks,string? Feedback);
record ModerationRequest(string Moderator,bool Approve,string Comments);
record ApprovalRequest(string Approver);
