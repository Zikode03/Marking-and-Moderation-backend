using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Uow;
namespace ProsperaAssess;

public class PermissionSeedContributor(IPermissionDataSeeder seeder, Volo.Abp.Domain.Repositories.IRepository<PermissionSeedState, Guid> checkpoints) : IDataSeedContributor, ITransientDependency
{
    [UnitOfWork]
    public virtual async Task SeedAsync(DataSeedContext context)
    {
        var checkpointId = Guid.Parse("f0658730-5f5e-4d43-9068-89c0381e95f1");
        if (await checkpoints.FindAsync(checkpointId) is not null) return;
        var staff = new[] { "SchoolAdmin", "Teacher", "Lecturer", "Professor", "HOD", "Principal", "AcademicAdmin" };
        foreach (var role in staff)
        {
            var grants = new List<string> { ProsperaPermissions.Workspace };
            if (new[] { "Teacher", "Lecturer", "Professor", "AcademicAdmin" }.Contains(role)) grants.AddRange([ProsperaPermissions.Mark, ProsperaPermissions.Upload, ProsperaPermissions.LabelTraining]);
            if (role == "SchoolAdmin") grants.AddRange([ProsperaPermissions.Upload, ProsperaPermissions.CreateStaff]);
            if (role is "HOD" or "AcademicAdmin") grants.Add(ProsperaPermissions.Moderate);
            if (role is "Principal" or "SchoolAdmin" or "AcademicAdmin") grants.Add(ProsperaPermissions.Approve);
            if (role is "SchoolAdmin" or "AcademicAdmin") grants.Add(ProsperaPermissions.ManageAcademics);
            await seeder.SeedAsync("R", role, grants, context.TenantId);
        }
        await checkpoints.InsertAsync(new PermissionSeedState(checkpointId, "DefaultRoles.v1"), autoSave: true);
    }
}
