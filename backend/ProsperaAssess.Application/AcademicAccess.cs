using System.Security.Claims;


namespace ProsperaAssess;

[Volo.Abp.RemoteService(false)]
public class AcademicAccess : ProsperaAppService
{
    public static bool Leader(ClaimsPrincipal p) => new[] { "SchoolAdmin", "AcademicAdmin", "HOD", "Principal" }.Any(p.IsInRole);
    public static bool Manager(ClaimsPrincipal p) => p.IsInRole("SchoolAdmin") || p.IsInRole("AcademicAdmin");

    public async Task<bool> CanAccess(StaffAccount user, ClaimsPrincipal principal, AssessmentRow assessment)
    {
        if (assessment.SchoolId != user.SchoolId) return false;
        if (Leader(principal)) return true;
        var school = assessment.SchoolContext();
        if (school is not null && principal.IsInRole("Teacher"))
        {
            var subjects = await AsyncExecuter.ToListAsync((await StaffAllocationsRepository.GetQueryableAsync()).Where(a => a.SchoolId == user.SchoolId && a.UserId == user.Id && a.Grade == school.Grade && a.ModuleId == null).Select(a => a.Subject));
            return subjects.Any(subject => string.Equals(subject?.Trim(), school.Subject.Trim(), StringComparison.OrdinalIgnoreCase));
        }
        var institution = assessment.InstitutionContext();
        return institution is not null && (principal.IsInRole("Lecturer") || principal.IsInRole("Professor")) &&
            await AsyncExecuter.AnyAsync((await StaffAllocationsRepository.GetQueryableAsync()), a => a.SchoolId == user.SchoolId && a.UserId == user.Id && a.ModuleId == institution.ModuleId);
    }

    public async Task<List<AssessmentRow>> Visible(StaffAccount user, ClaimsPrincipal principal)
    {
        var rows = await AsyncExecuter.ToListAsync((await AssessmentsRepository.GetQueryableAsync()).Where(a => a.SchoolId == user.SchoolId).OrderBy(a => a.Code));
        if (Leader(principal)) return rows;
        var allocations = await AsyncExecuter.ToListAsync((await StaffAllocationsRepository.GetQueryableAsync()).Where(a => a.SchoolId == user.SchoolId && a.UserId == user.Id));
        return rows.Where(row =>
        {
            var school = row.SchoolContext();
            if (school is not null && principal.IsInRole("Teacher"))
                return allocations.Any(a => a.ModuleId == null && a.Grade == school.Grade && string.Equals(a.Subject?.Trim(), school.Subject?.Trim(), StringComparison.OrdinalIgnoreCase));
            var institution = row.InstitutionContext();
            return institution is not null && (principal.IsInRole("Lecturer") || principal.IsInRole("Professor")) && allocations.Any(a => a.ModuleId == institution.ModuleId);
        }).ToList();
    }
}
