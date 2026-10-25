using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.PermissionManagement.EntityFrameworkCore;
using Volo.Abp.AuditLogging.EntityFrameworkCore;
using Volo.Abp.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ProsperaAssess;

public class AppUser : IdentityUser { public Guid SchoolId { get; set; } }
[ConnectionStringName("Default")]
public class AssessDb(DbContextOptions<AssessDb> options) : AbpDbContext<AssessDb>(options)
{
    public DbSet<PermissionSeedState> PermissionSeedStates => Set<PermissionSeedState>();
    public DbSet<ProsperaAssess.AiTrainingExample> AiTrainingExamples => Set<ProsperaAssess.AiTrainingExample>();
    public DbSet<ProsperaAssess.AiBatch> AiBatches => Set<ProsperaAssess.AiBatch>();
    public DbSet<ProsperaAssess.AiScript> AiScripts => Set<ProsperaAssess.AiScript>();
    public DbSet<AssessmentRow> Assessments => Set<AssessmentRow>();
    public DbSet<SubmissionRow> Submissions => Set<SubmissionRow>();
    public DbSet<FacultyRow> Faculties => Set<FacultyRow>();
    public DbSet<ModuleRow> Modules => Set<ModuleRow>();
    public DbSet<StaffAllocationRow> StaffAllocations => Set<StaffAllocationRow>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);
        new IdentitySchemaContext(new DbContextOptionsBuilder<IdentitySchemaContext>().UseSqlServer("Server=localhost;Database=IdentitySchema;Trusted_Connection=True;TrustServerCertificate=True").Options).ConfigureIdentity(model);
        model.ConfigurePermissionManagement();
        model.ConfigureAuditLogging();
        model.Entity<ProsperaAssess.AiTrainingExample>().HasOne<ProsperaAssess.AiScript>().WithMany().HasForeignKey(x => x.ScriptId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<ProsperaAssess.AiTrainingExample>().Property(x => x.MaxMarks).HasPrecision(18, 2);
        model.Entity<ProsperaAssess.AiTrainingExample>().Property(x => x.Marks).HasPrecision(18, 2);
        model.Entity<ProsperaAssess.AiTrainingExample>().Property(x => x.VerifiedBy).IsConcurrencyToken();
        model.Entity<ProsperaAssess.AiBatch>().HasOne<AssessmentRow>().WithMany().HasForeignKey(x => x.AssessmentId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<ProsperaAssess.AiScript>().HasOne<ProsperaAssess.AiBatch>().WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<ProsperaAssess.AiScript>().Property(x => x.Status).HasMaxLength(40);
        model.Entity<ProsperaAssess.AiScript>().HasIndex(x => x.Status);
        model.Entity<FacultyRow>().Property(x => x.Name).HasMaxLength(200);
        model.Entity<FacultyRow>().Property(x => x.Code).HasMaxLength(50);
        model.Entity<FacultyRow>().HasIndex(x => new { x.SchoolId, x.Code }).IsUnique();
        model.Entity<ModuleRow>().Property(x => x.Name).HasMaxLength(200);
        model.Entity<ModuleRow>().Property(x => x.Code).HasMaxLength(50);
        model.Entity<ModuleRow>().HasIndex(x => new { x.SchoolId, x.Code }).IsUnique();
        model.Entity<ModuleRow>().HasOne<FacultyRow>().WithMany().HasForeignKey(x => x.FacultyId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<StaffAllocationRow>().Property(x => x.UserId).HasMaxLength(450);
        model.Entity<StaffAllocationRow>().Property(x => x.Subject).HasMaxLength(200);
        model.Entity<StaffAllocationRow>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<StaffAllocationRow>().HasOne<ModuleRow>().WithMany().HasForeignKey(x => x.ModuleId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<StaffAllocationRow>().HasIndex(x => new { x.SchoolId, x.UserId, x.Grade, x.Subject }).IsUnique().HasFilter("[ModuleId] IS NULL");
        model.Entity<StaffAllocationRow>().HasIndex(x => new { x.SchoolId, x.UserId, x.ModuleId }).IsUnique().HasFilter("[ModuleId] IS NOT NULL");
        model.Entity<AssessmentRow>().HasIndex(x => new { x.SchoolId, x.Code });
        model.Entity<SubmissionRow>().HasIndex(x => new { x.SchoolId, x.AssessmentId });
        // Every workflow change appends an audit entry. Reuse this existing column
        // for optimistic concurrency without changing the installed SQL schema.
        model.Entity<SubmissionRow>().Property(x => x.AuditJson).IsConcurrencyToken();
        model.Entity<SubmissionRow>().HasOne<AssessmentRow>().WithMany().HasForeignKey(x => x.AssessmentId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class IdentitySchemaContext(DbContextOptions<IdentitySchemaContext> options) : IdentityDbContext<AppUser>(options) { public void ConfigureIdentity(ModelBuilder model) => base.OnModelCreating(model); }
