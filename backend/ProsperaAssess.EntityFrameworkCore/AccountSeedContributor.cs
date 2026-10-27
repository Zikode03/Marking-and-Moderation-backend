using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Uow;
namespace ProsperaAssess;

public class AccountSeedContributor(RoleManager<IdentityRole> roles, UserManager<AppUser> users, IConfiguration configuration) : IDataSeedContributor, ITransientDependency
{
    [UnitOfWork]
    public virtual async Task SeedAsync(DataSeedContext context)
    {

        foreach (var role in new[] { "SchoolAdmin", "Teacher", "Lecturer", "Professor", "HOD", "Moderator", "Principal", "AcademicAdmin" })
            if (!await roles.RoleExistsAsync(role))
                Check(await roles.CreateAsync(new IdentityRole(role)));

        var email = configuration["Bootstrap:Email"];
        var password = configuration["Bootstrap:Password"];
        var school = configuration["Bootstrap:SchoolId"];
        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(password) && string.IsNullOrWhiteSpace(school)) return;
        if ((string.IsNullOrWhiteSpace(email) || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email)) || string.IsNullOrWhiteSpace(password) ||
            !Guid.TryParse(school, out var schoolId) || schoolId == Guid.Empty)
            throw new InvalidOperationException("Bootstrap requires a valid email, password, and non-empty SchoolId together.");

        var existing = await users.FindByEmailAsync(email!.Trim());
        if (existing is not null)
        {
            if (existing.SchoolId != schoolId) throw new InvalidOperationException("The bootstrap account belongs to another school.");
            return;
        }

        var user = new AppUser { UserName = email.Trim(), Email = email.Trim(), EmailConfirmed = true, SchoolId = schoolId };
        Check(await users.CreateAsync(user, password!));
        Check(await users.AddToRoleAsync(user, "SchoolAdmin"));

    }
    private static void Check(IdentityResult r) { if (!r.Succeeded) throw new InvalidOperationException(string.Join("; ", r.Errors.Select(e => e.Description))); }
}
