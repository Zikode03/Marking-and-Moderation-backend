using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

public class AppUser:IdentityUser {public Guid SchoolId {get;set;}}
public class AssessDb(DbContextOptions<AssessDb> options):IdentityDbContext<AppUser>(options) {
 public DbSet<AssessmentRow> Assessments=>Set<AssessmentRow>();
 public DbSet<SubmissionRow> Submissions=>Set<SubmissionRow>();
 protected override void OnModelCreating(ModelBuilder model){
  base.OnModelCreating(model);
  model.Entity<AssessmentRow>().HasIndex(x=>new{x.SchoolId,x.Code});
  model.Entity<SubmissionRow>().HasIndex(x=>new{x.SchoolId,x.AssessmentId});
  model.Entity<SubmissionRow>().HasOne<AssessmentRow>().WithMany().HasForeignKey(x=>x.AssessmentId).OnDelete(DeleteBehavior.Restrict);
 }
}
public class AssessmentRow {
 public Guid Id {get;set;} public Guid SchoolId {get;set;} public string Title {get;set;}="";
 public string Code {get;set;}="";public string CriteriaJson {get;set;}="[]";public string? SchoolJson {get;set;}
 public List<Criterion> Criteria()=>JsonSerializer.Deserialize<List<Criterion>>(CriteriaJson,AppJson.Options)??[];
 public object ToView(){var c=Criteria();return new {Id,Title,Code,TotalMarks=c.Sum(x=>x.MaxMarks),Criteria=c,School=SchoolJson==null?null:JsonSerializer.Deserialize<SchoolContext>(SchoolJson,AppJson.Options)};}
}
public class SubmissionRow{
 public Guid Id {get;set;}public Guid SchoolId {get;set;}public Guid AssessmentId {get;set;}
 public string StudentReference {get;set;}="";public string AnswerText {get;set;}="";
 public string Status {get;set;}="Submitted";public string MarksJson {get;set;}="{}";
 public string? LastMarkerId {get;set;}public string? Feedback {get;set;}public string AuditJson {get;set;}="[]";
 public void AddAudit(string actor,string action,string notes){var entries=JsonSerializer.Deserialize<List<AuditEntry>>(AuditJson,AppJson.Options)??[];entries.Add(new AuditEntry(DateTimeOffset.UtcNow,actor,action,notes));AuditJson=JsonSerializer.Serialize(entries,AppJson.Options);}
 public object ToView(){var marks=JsonSerializer.Deserialize<Dictionary<Guid,decimal>>(MarksJson,AppJson.Options)??new();return new {Id,AssessmentId,StudentReference,AnswerText,Status,Marks=marks,Feedback,Total=marks.Values.Sum(),Audit=JsonSerializer.Deserialize<List<AuditEntry>>(AuditJson,AppJson.Options)??[]};}
}
public static class AppJson {
 public static readonly JsonSerializerOptions Options=new(JsonSerializerDefaults.Web);
 public static string Write<T>(T value)=>JsonSerializer.Serialize(value,Options);
}
public record Criterion(Guid Id,string Name,decimal MaxMarks);
public record SchoolContext(string Subject,int Grade,int Term,string AssessmentType,string SchoolType="Independent",string Pathway="IEB",decimal WeightPercent=0);
public record CriterionInput(string Name,decimal MaxMarks);
public record CreateAssessment(string Title,string Code,List<CriterionInput> Criteria,SchoolContext? School);
public record CreateSubmission(Guid AssessmentId,string StudentReference,string AnswerText);
public record MarkRequest(string Marker,Dictionary<Guid,decimal> Marks,string? Feedback);
public record ModerationRequest(string Moderator,bool Approve,string Comments);
public record AuditEntry(DateTimeOffset At,string Actor,string Action,string Notes);
