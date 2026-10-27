using System.ComponentModel.DataAnnotations;

namespace ProsperaAssess;

public static class InputRules
{
    public static bool Text(string? value, int max) => !string.IsNullOrWhiteSpace(value) && value.Length <= max;
    public static bool Email(string? value) => Text(value, 256) && new EmailAddressAttribute().IsValid(value!.Trim());
    public static bool Assessment(CreateAssessment request) =>
        Text(request.Title, 200) && Text(request.Code, 50) &&
        ((request.School is null) != (request.Institution is null)) &&
        (request.Institution is null || (request.Institution.ModuleId != Guid.Empty && request.Institution.AcademicYear is >= 2000 and <= 2100 && request.Institution.Semester is >= 1 and <= 2 && request.Institution.WeightPercent is >= 0 and <= 100 && request.Institution.AssessmentType is "Test" or "Examination" or "Assignment" or "Project" or "Practical" or "Other")) &&
        request.Criteria is { Count: > 0 and <= 100 } &&
        request.Criteria.All(c => c is not null && Text(c.Name, 200) && c.MaxMarks > 0 && c.MaxMarks <= 1_000_000) &&
        (request.School is null ||
         (Text(request.School.Subject, 200) && (request.School.AcademicYear is null or >= 2000 and <= 2200) && request.School.Grade is >= 8 and <= 12 &&
          request.School.Term is >= 1 and <= 4 && request.School.WeightPercent is >= 0 and <= 100 &&
          request.School.Pathway is "IEB" or "DBE-NSC" or "Other" &&
          request.School.SchoolType is "Independent" or "Public" &&
          request.School.AssessmentType is "Test" or "Examination" or "Assignment" or "Project" or "Practical" or "Other"));
}
