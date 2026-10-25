using Volo.Abp.Authorization.Permissions;
namespace ProsperaAssess;

public static class ProsperaPermissions
{
    public const string GroupName = "Prospera";
    public const string Workspace = "Prospera.Workspace";
    public const string Mark = "Prospera.Scripts.Mark";
    public const string Upload = "Prospera.Scripts.Upload";
    public const string Moderate = "Prospera.Scripts.Moderate";
    public const string Approve = "Prospera.Scripts.Approve";
    public const string ManageAcademics = "Prospera.Academics.Manage";
    public const string CreateStaff = "Prospera.Staff.Create";
    public const string LabelTraining = "Prospera.Training.Label";
}
public class ProsperaPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var group = context.AddGroup(ProsperaPermissions.GroupName, Volo.Abp.Localization.LocalizableString.Create<ProsperaAssessResource>("Permission:Group"));
        foreach (var name in new[] { ProsperaPermissions.Workspace, ProsperaPermissions.Mark, ProsperaPermissions.Upload, ProsperaPermissions.Moderate, ProsperaPermissions.Approve, ProsperaPermissions.ManageAcademics, ProsperaPermissions.CreateStaff, ProsperaPermissions.LabelTraining }) group.AddPermission(name, Volo.Abp.Localization.LocalizableString.Create<ProsperaAssessResource>("Permission:" + typeof(ProsperaPermissions).GetFields().First(f => f.IsLiteral && Equals(f.GetRawConstantValue(), name)).Name));
    }
}
