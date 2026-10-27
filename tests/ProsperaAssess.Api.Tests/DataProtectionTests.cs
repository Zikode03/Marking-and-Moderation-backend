using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ProsperaAssess.Api.Tests;

public sealed class DataProtectionTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private async Task<Guid> SeedExpiredBatch()
    {
        var assessment = await fixture.SeedAssessment();
        using var scope = fixture.Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AssessDb>();
        var batch = new AiBatch(Guid.NewGuid()) {AssessmentId=assessment.GetProperty("id").GetGuid(), CreatedAt=DateTimeOffset.UtcNow.AddDays(-90), Paper=Encoding.UTF8.GetBytes("Synthetic question paper"), Memo=Encoding.UTF8.GetBytes("Synthetic memo")};
        var script = new AiScript(Guid.NewGuid()) {BatchId=batch.Id, Candidate="SYNTHETIC-RETENTION", Content=Encoding.UTF8.GetBytes("Synthetic learner answer")};
        typeof(AiScript).GetProperty(nameof(AiScript.Status))!.SetValue(script,"Approved");
        typeof(AiScript).GetProperty(nameof(AiScript.UpdatedAt))!.SetValue(script,DateTimeOffset.UtcNow.AddDays(-90));
        db.AddRange(batch,script); await db.SaveChangesAsync();
        return batch.Id;
    }
    private async Task Policy(int days)
    {
        using var scope = fixture.Factory.Services.CreateScope(); var db=scope.ServiceProvider.GetRequiredService<AssessDb>();
        var settings=await db.InstitutionRecords.FirstOrDefaultAsync(r=>r.SchoolId==fixture.School && r.Kind=="settings");
        if(settings is null) {settings=new InstitutionRecord(Guid.NewGuid()) {SchoolId=fixture.School,Kind="settings",Code="CURRENT"}; db.Add(settings);}
        settings.DataJson=AppJson.Write(new Dictionary<string,string>{{"retentionDays",days.ToString()}}); settings.Revision++; await db.SaveChangesAsync();
    }
    private static async Task<JsonElement> Preview(HttpClient admin, Guid id) => (await admin.GetFromJsonAsync<JsonElement>("/api/data-protection")).GetProperty("batches").EnumerateArray().Single(b=>b.GetProperty("id").GetGuid()==id).Clone();
    [Fact]
    public async Task RetentionRequiresManagerFreshPreviewAndNoHoldAndDeletesLinkedTrainingOnly()
    {
        var id=await SeedExpiredBatch(); var other=await SeedExpiredBatch(); await Policy(30);
        using var admin=fixture.Client("SchoolAdmin"); using var teacher=fixture.Client("Teacher");
        Assert.Equal(HttpStatusCode.Forbidden,(await teacher.GetAsync("/api/data-protection")).StatusCode);
        var preview=await Preview(admin,id); Assert.True(preview.GetProperty("eligible").GetBoolean());
        var token=preview.GetProperty("token").GetString();
        Assert.Equal(HttpStatusCode.OK,(await admin.PostAsJsonAsync($"/api/data-protection/batches/{id}/hold",new {enabled=true,reason="Synthetic appeal REF-001"})).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await admin.PostAsJsonAsync($"/api/data-protection/batches/{id}/purge",new {previewToken=token,confirmation="DELETE EXPIRED BATCH"})).StatusCode);
        Assert.False((await Preview(admin,id)).GetProperty("eligible").GetBoolean());
        Assert.Equal(HttpStatusCode.OK,(await admin.PostAsJsonAsync($"/api/data-protection/batches/{id}/hold",new {enabled=false,reason="Synthetic appeal resolved REF-001"})).StatusCode);
        await Policy(365); Assert.Equal(HttpStatusCode.Conflict,(await admin.PostAsJsonAsync($"/api/data-protection/batches/{id}/purge",new {previewToken=token,confirmation="DELETE EXPIRED BATCH"})).StatusCode);
        await Policy(30); preview=await Preview(admin,id);
        using(var scope=fixture.Factory.Services.CreateScope()) {var db=scope.ServiceProvider.GetRequiredService<AssessDb>();var script=await db.AiScripts.FirstAsync(s=>s.BatchId==id);db.AiTrainingExamples.Add(new AiTrainingExample(Guid.NewGuid()){ScriptId=script.Id,QuestionNumber="1",Answer="Synthetic answer",Question="Synthetic question",Rubric="Synthetic rubric"});await db.SaveChangesAsync();}
        preview=await Preview(admin,id);
        Assert.Equal(HttpStatusCode.BadRequest,(await admin.PostAsJsonAsync($"/api/data-protection/batches/{id}/purge",new {previewToken=preview.GetProperty("token").GetString(),confirmation="yes"})).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await admin.PostAsJsonAsync($"/api/data-protection/batches/{id}/purge",new {previewToken=preview.GetProperty("token").GetString(),confirmation="DELETE EXPIRED BATCH"})).StatusCode);
        using var verify=fixture.Factory.Services.CreateScope(); var context=verify.ServiceProvider.GetRequiredService<AssessDb>();
        Assert.False(await context.AiBatches.AnyAsync(b=>b.Id==id)); Assert.False(await context.AiScripts.AnyAsync(s=>s.BatchId==id)); Assert.Empty(await context.AiTrainingExamples.ToListAsync()); Assert.True(await context.AiBatches.AnyAsync(b=>b.Id==other));
        Assert.True(await context.InstitutionEvents.AnyAsync(e=>e.RecordId==id && e.Action=="retention.batchPurged"));
        Assert.Equal(HttpStatusCode.NotFound,(await admin.PostAsJsonAsync($"/api/data-protection/batches/{Guid.NewGuid()}/hold",new {enabled=true,reason="No cross institution access"})).StatusCode);
    }
    [Fact]
    public async Task ActiveWorkCannotBePurgedAndUsageIncludesAllDocumentBytes()
    {
        var id=await SeedExpiredBatch(); await Policy(30);
        using(var scope=fixture.Factory.Services.CreateScope()) {var db=scope.ServiceProvider.GetRequiredService<AssessDb>();var script=await db.AiScripts.FirstAsync(s=>s.BatchId==id);typeof(AiScript).GetProperty(nameof(AiScript.Status))!.SetValue(script,"Uploaded");await db.SaveChangesAsync();}
        using var admin=fixture.Client("SchoolAdmin"); var preview=await Preview(admin,id); Assert.False(preview.GetProperty("eligible").GetBoolean());
        var status=await admin.GetFromJsonAsync<JsonElement>("/api/data-protection"); Assert.True(status.GetProperty("usedBytes").GetInt64()>0);
        Assert.Equal(HttpStatusCode.Conflict,(await admin.PostAsJsonAsync($"/api/data-protection/batches/{id}/purge",new {previewToken=preview.GetProperty("token").GetString(),confirmation="DELETE EXPIRED BATCH"})).StatusCode);
    }
}
