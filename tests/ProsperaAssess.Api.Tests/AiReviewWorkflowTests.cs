using System.Reflection;
using System.Text.Json;
using ProsperaAssess;
using Volo.Abp;
using Xunit;

namespace ProsperaAssess.Api.Tests;

public class AiReviewWorkflowTests
{
    private static AiScript PendingScript()
    {
        var script = new AiScript(Guid.NewGuid());
        typeof(AiScript).GetProperty(nameof(AiScript.Status))!.SetValue(script, "AwaitingModeration");
        var result = new AiMarkingResult(false, "Provisional AI feedback", [
            new AiQuestion("1", 10m, 6m, "Candidate response", "Explain the missing steps", false)
        ]);
        typeof(AiScript).GetProperty(nameof(AiScript.ResultJson))!.SetValue(script, JsonSerializer.Serialize(result, AppJson.Options));
        return script;
    }

    [Fact]
    public void AiResultRequiresTeacherReviewThenIndependentHodModerationAndApproval()
    {
        var script = PendingScript();
        var marks = new Dictionary<string, decimal> { ["1"] = 7m };
        Assert.Throws<BusinessException>(() => script.ConfirmModeration("hod-1", marks, "Reviewed"));
        Assert.Throws<BusinessException>(() => script.Approve("principal"));
        script.ConfirmTeacherReview("teacher-1", marks, "AI missed partial credit; explain the working");
        Assert.Equal("TeacherReviewed", script.Status);
        Assert.Throws<BusinessException>(() => script.ConfirmModeration("teacher-1", marks, "Self moderation"));
        script.ConfirmModeration("hod-1", marks, "HOD verified correction");
        Assert.Equal("Moderated", script.Status);
        script.Approve("principal");
        Assert.Equal("Approved", script.Status);
        Assert.Throws<BusinessException>(() => script.ConfirmTeacherReview("teacher-1", marks, "Edit after approval"));
    }

    [Fact]
    public void ReturnedScriptMustBeReviewedAgain()
    {
        var script = PendingScript();
        var marks = new Dictionary<string, decimal> { ["1"] = 6m };
        script.ConfirmTeacherReview("teacher-1", marks, "Checked the feedback");
        script.ReturnToTeacher("hod-1", "Please recheck the second step");
        Assert.Equal("ReturnedToTeacher", script.Status);
        Assert.Throws<BusinessException>(() => script.Approve("principal"));
        Assert.Throws<BusinessException>(() => script.ConfirmModeration("hod-1", marks, "Cannot bypass teacher"));
        script.ConfirmTeacherReview("teacher-1", marks, "Reviewed step and confirmed decision");
        script.ConfirmModeration("hod-1", marks, "Confirmed after correction");
        Assert.Equal("Moderated", script.Status);
    }
}
