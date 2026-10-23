using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

var builder=WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5080");
var connection=builder.Configuration.GetConnectionString("Default");
if(string.IsNullOrWhiteSpace(connection))throw new InvalidOperationException("Configure ConnectionStrings:Default in user secrets.");
builder.Services.AddDbContext<AssessDb>(o=>o.UseSqlServer(connection));
builder.Services.AddIdentityApiEndpoints<AppUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<AssessDb>();
builder.Services.AddAuthorization(o=>{
 o.AddPolicy("Staff",p=>p.RequireRole("SchoolAdmin","Teacher","HOD","Principal","AcademicAdmin"));
 o.AddPolicy("Marker",p=>p.RequireRole("Teacher","AcademicAdmin"));
 o.AddPolicy("Moderator",p=>p.RequireRole("HOD","AcademicAdmin"));
 o.AddPolicy("Approver",p=>p.RequireRole("Principal","SchoolAdmin","AcademicAdmin"));
});
builder.Services.AddCors(o=>o.AddPolicy("dev",p=>p.WithOrigins("http://localhost:5173").AllowAnyHeader().AllowAnyMethod()));
var app=builder.Build();app.UseCors("dev");app.UseAuthentication();app.UseAuthorization();
using(var scope=app.Services.CreateScope()){
 var db=scope.ServiceProvider.GetRequiredService<AssessDb>();
 await db.Database.EnsureCreatedAsync(); // Development only: replace with migrations before deployments.
 var roles=scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
 foreach(var role in new[]{"SchoolAdmin","Teacher","HOD","Principal","AcademicAdmin"})if(!await roles.RoleExistsAsync(role))await roles.CreateAsync(new IdentityRole(role));
 var email=app.Configuration["Bootstrap:Email"];var pass=app.Configuration["Bootstrap:Password"];var school=app.Configuration["Bootstrap:SchoolId"];
 if(!string.IsNullOrWhiteSpace(email)&&!string.IsNullOrWhiteSpace(pass)&&Guid.TryParse(school,out var schoolId)){
  var users=scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
  if(await users.FindByEmailAsync(email)==null){
   var user=new AppUser{UserName=email,Email=email,EmailConfirmed=true,SchoolId=schoolId};
   var created=await users.CreateAsync(user,pass);
   if(!created.Succeeded)throw new InvalidOperationException(string.Join("; ",created.Errors.Select(e=>e.Description)));
   await users.AddToRoleAsync(user,"SchoolAdmin");
  }
 }
}
// Identity maps /login and /refresh. Do not map public /register.
app.MapPost("/login",async(LoginRequest req,SignInManager<AppUser> signIn,UserManager<AppUser> users)=>{
 var user=await users.FindByEmailAsync(req.Email);
 if(user is null)return Results.Unauthorized();
 var result=await signIn.CheckPasswordSignInAsync(user,req.Password,lockoutOnFailure:true);
 if(!result.Succeeded)return Results.Unauthorized();
 // Identity bearer token response, not a hand-rolled JWT.
 signIn.AuthenticationScheme=IdentityConstants.BearerScheme;
 await signIn.SignInAsync(user,isPersistent:false);
 return Results.Empty;
});
app.MapPost("/api/bootstrap/staff",async(CreateStaffRequest req,ClaimsPrincipal p,UserManager<AppUser> um)=>{
 var current=await um.GetUserAsync(p);if(current is null)return Results.Unauthorized();
 if(!await um.IsInRoleAsync(current,"SchoolAdmin"))return Results.Forbid();
 if(!new[]{"Teacher","HOD","Principal","AcademicAdmin"}.Contains(req.Role)||string.IsNullOrWhiteSpace(req.Email)||string.IsNullOrWhiteSpace(req.Password))return Results.BadRequest(new {error="Valid email, password and school role are required."});
 if(await um.FindByEmailAsync(req.Email)!=null)return Results.Conflict(new {error="Account already exists."});
 var user=new AppUser{UserName=req.Email,Email=req.Email,EmailConfirmed=true,SchoolId=current.SchoolId};
 var created=await um.CreateAsync(user,req.Password);if(!created.Succeeded)return Results.BadRequest(new {errors=created.Errors.Select(x=>x.Description)});
 var assigned=await um.AddToRoleAsync(user,req.Role);if(!assigned.Succeeded)return Results.BadRequest(new {errors=assigned.Errors.Select(x=>x.Description)});
 return Results.Created("/api/me",new {user.Email,user.SchoolId,req.Role});
}).RequireAuthorization();
app.MapGet("/api/health",()=>Results.Ok(new {status="ok",persistence="sql-server",ai="not-trained"}));
var api=app.MapGroup("/api").RequireAuthorization();
api.MapGet("/me",async(ClaimsPrincipal p,UserManager<AppUser> um)=>{
 var u=await um.GetUserAsync(p);return u is null?Results.Unauthorized():Results.Ok(new {u.Email,u.SchoolId,roles=await um.GetRolesAsync(u)});
});
api.MapGet("/assessments",async(AssessDb db,ClaimsPrincipal p,UserManager<AppUser> um)=>{
 var u=await um.GetUserAsync(p);if(u is null)return Results.Unauthorized();
 var a=await db.Assessments.AsNoTracking().Where(x=>x.SchoolId==u.SchoolId).ToListAsync();
 return Results.Ok(a.Select(x=>x.ToView()));
});
api.MapGet("/schools/grades",()=>Results.Ok(Enumerable.Range(8,5)));
api.MapGet("/schools/assessments",async(AssessDb db,ClaimsPrincipal p,UserManager<AppUser> um)=>{
 var u=await um.GetUserAsync(p);if(u is null)return Results.Unauthorized();
 var a=await db.Assessments.AsNoTracking().Where(x=>x.SchoolId==u.SchoolId&&x.SchoolJson!=null).ToListAsync();
 return Results.Ok(a.Select(x=>x.ToView()));
});
api.MapPost("/assessments",async(CreateAssessment req,AssessDb db,ClaimsPrincipal p,UserManager<AppUser> um)=>{
 var u=await um.GetUserAsync(p);if(u is null)return Results.Unauthorized();
 if(string.IsNullOrWhiteSpace(req.Title)||string.IsNullOrWhiteSpace(req.Code)||req.Criteria is null||req.Criteria.Count==0||req.Criteria.Any(c=>string.IsNullOrWhiteSpace(c.Name)||c.MaxMarks<=0))return Results.BadRequest(new {error="Title, code and positive rubric criteria required."});
 if(req.School is not null&&(req.School.Grade<8||req.School.Grade>12||req.School.Term<1||req.School.Term>4||req.School.WeightPercent<0||req.School.WeightPercent>100||string.IsNullOrWhiteSpace(req.School.Subject)||!new[]{"IEB","DBE-NSC","Other"}.Contains(req.School.Pathway)||!new[]{"Independent","Public"}.Contains(req.School.SchoolType)))return Results.BadRequest(new {error="Invalid school assessment settings."});
 var a=new AssessmentRow{Id=Guid.NewGuid(),SchoolId=u.SchoolId,Title=req.Title.Trim(),Code=req.Code.Trim(),CriteriaJson=AppJson.Write(req.Criteria.Select(c=>new Criterion(Guid.NewGuid(),c.Name,c.MaxMarks)).ToList()),SchoolJson=req.School==null?null:AppJson.Write(req.School)};
 db.Assessments.Add(a);await db.SaveChangesAsync();return Results.Created($"/api/assessments/{a.Id}",a.ToView());
}).RequireAuthorization("Staff");
api.MapGet("/submissions",async(Guid? assessmentId,AssessDb db,ClaimsPrincipal p,UserManager<AppUser> um)=>{
 var u=await um.GetUserAsync(p);if(u is null)return Results.Unauthorized();
 var s=await db.Submissions.AsNoTracking().Where(x=>x.SchoolId==u.SchoolId&&(assessmentId==null||x.AssessmentId==assessmentId)).ToListAsync();
 return Results.Ok(s.Select(x=>x.ToView()));
});
api.MapPost("/submissions",async(CreateSubmission req,AssessDb db,ClaimsPrincipal p,UserManager<AppUser> um)=>{
 var u=await um.GetUserAsync(p);if(u is null)return Results.Unauthorized();
 if(!await db.Assessments.AnyAsync(a=>a.Id==req.AssessmentId&&a.SchoolId==u.SchoolId))return Results.NotFound();
 if(string.IsNullOrWhiteSpace(req.StudentReference)||string.IsNullOrWhiteSpace(req.AnswerText))return Results.BadRequest(new {error="Student reference and answer text required."});
 var s=new SubmissionRow{Id=Guid.NewGuid(),AssessmentId=req.AssessmentId,SchoolId=u.SchoolId,StudentReference=req.StudentReference,AnswerText=req.AnswerText};
 db.Submissions.Add(s);await db.SaveChangesAsync();return Results.Created($"/api/submissions/{s.Id}",s.ToView());
}).RequireAuthorization("Staff");
api.MapPost("/submissions/{id:guid}/mark",async(Guid id,MarkRequest req,AssessDb db,ClaimsPrincipal p,UserManager<AppUser> um)=>{
 var u=await um.GetUserAsync(p);if(u is null)return Results.Unauthorized();
 var s=await db.Submissions.FirstOrDefaultAsync(x=>x.Id==id&&x.SchoolId==u.SchoolId);if(s is null)return Results.NotFound();
 var a=await db.Assessments.FirstAsync(x=>x.Id==s.AssessmentId&&x.SchoolId==u.SchoolId);
 if(s.Status=="Approved")return Results.Conflict(new {error="Finalised mark cannot be edited."});
 var c=a.Criteria();if(req.Marks==null||req.Marks.Count!=c.Count||c.Any(x=>!req.Marks.TryGetValue(x.Id,out var m)||m<0||m>x.MaxMarks))return Results.BadRequest(new {error="All rubric marks must be valid."});
 s.MarksJson=AppJson.Write(req.Marks);s.Feedback=req.Feedback;s.LastMarkerId=u.Id;s.Status="Marked";s.AddAudit(u.Id,"Marked",req.Feedback??"");
 await db.SaveChangesAsync();return Results.Ok(s.ToView());
}).RequireAuthorization("Marker");
api.MapPost("/submissions/{id:guid}/moderate",async(Guid id,ModerationRequest req,AssessDb db,ClaimsPrincipal p,UserManager<AppUser> um)=>{
 var u=await um.GetUserAsync(p);if(u is null)return Results.Unauthorized();
 var s=await db.Submissions.FirstOrDefaultAsync(x=>x.Id==id&&x.SchoolId==u.SchoolId);if(s is null)return Results.NotFound();
 if(s.Status!="Marked")return Results.Conflict(new {error="Only marked submissions can be moderated."});
 if(s.LastMarkerId==u.Id)return Results.Conflict(new {error="Marker cannot moderate their own marking."});
 if(string.IsNullOrWhiteSpace(req.Comments))return Results.BadRequest(new {error="Moderation comments required."});
 s.Status=req.Approve?"Moderated":"Submitted";s.AddAudit(u.Id,req.Approve?"Moderated":"Returned",req.Comments);await db.SaveChangesAsync();return Results.Ok(s.ToView());
}).RequireAuthorization("Moderator");
api.MapPost("/submissions/{id:guid}/approve",async(Guid id,AssessDb db,ClaimsPrincipal p,UserManager<AppUser> um)=>{
 var u=await um.GetUserAsync(p);if(u is null)return Results.Unauthorized();
 var s=await db.Submissions.FirstOrDefaultAsync(x=>x.Id==id&&x.SchoolId==u.SchoolId);if(s is null)return Results.NotFound();
 if(s.Status!="Moderated")return Results.Conflict(new {error="Moderation required before approval."});
 s.Status="Approved";s.AddAudit(u.Id,"Approved","Approved academic result");await db.SaveChangesAsync();return Results.Ok(s.ToView());
}).RequireAuthorization("Approver");
api.MapPost("/submissions/{id:guid}/ai-suggestion",()=>Results.Problem("No validated proprietary AI model is deployed.",statusCode:503));
app.Run();
record LoginRequest(string Email,string Password);
record CreateStaffRequest(string Email,string Password,string Role);
