using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Uow;
namespace ProsperaAssess;
// Compatibility adapter: existing string user IDs/password hashes and bearer tokens are retained.
public class PortalUserStore : UserStore<AppUser, IdentityRole, AssessDb, string>
{
    public PortalUserStore(IServiceProvider services, IUnitOfWorkManager units, IDbContextProvider<AssessDb> provider)
    : base(units.Current is null ? services.GetRequiredService<AssessDb>() : Volo.Abp.Threading.AsyncHelper.RunSync(provider.GetDbContextAsync)) { }
}
public class StaffIdentityService(IServiceScopeFactory scopes) : IStaffIdentityService
{
    private async Task<T> With<T>(Func<UserManager<AppUser>, Task<T>> operation) { using var scope = scopes.CreateScope(); return await operation(scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>()); }
    private static StaffAccount? View(AppUser? u) => u is null ? null : new() { Id = u.Id, Email = u.Email, UserName = u.UserName, EmailConfirmed = u.EmailConfirmed, SchoolId = u.SchoolId };
    private static AccountOperation Result(IdentityResult r) => new(r.Succeeded, r.Errors.Select(e => new AccountError(e.Description)).ToList());
    public Task<StaffAccount?> GetUserAsync(ClaimsPrincipal p) => With(async u => View(await u.GetUserAsync(p)));
    public Task<StaffAccount?> FindByEmailAsync(string email) => With(async u => View(await u.FindByEmailAsync(email)));
    public Task<StaffAccount?> FindByIdAsync(string id) => With(async u => View(await u.FindByIdAsync(id)));
    public Task<IList<string>> GetRolesAsync(StaffAccount user) => With(async u => await u.GetRolesAsync((await u.FindByIdAsync(user.Id))!));
    public Task<bool> IsInRoleAsync(StaffAccount user, string role) => With(async u => await u.IsInRoleAsync((await u.FindByIdAsync(user.Id))!, role));
    public Task<List<StaffAccount>> GetUsersAsync(Guid schoolId) => With(async u => (await u.Users.Where(x => x.SchoolId == schoolId).OrderBy(x => x.Email).ToListAsync()).Select(x => View(x)!).ToList());
    public Task<AccountOperation> CreateAsync(StaffAccount user, string password) => With(async u => Result(await u.CreateAsync(new AppUser { Id = user.Id, Email = user.Email, UserName = user.UserName, EmailConfirmed = user.EmailConfirmed, SchoolId = user.SchoolId }, password)));
    public Task<AccountOperation> AddToRoleAsync(StaffAccount user, string role) => With(async u => Result(await u.AddToRoleAsync((await u.FindByIdAsync(user.Id))!, role)));
}
