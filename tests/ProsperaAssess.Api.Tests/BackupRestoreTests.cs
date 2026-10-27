using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace ProsperaAssess.Api.Tests;

public sealed class BackupRestoreTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task CopyOnlyBackupRestoresIntoUniqueDatabaseAndPassesIntegrityCheck()
    {
        using var scope=fixture.Factory.Services.CreateScope(); var db=scope.ServiceProvider.GetRequiredService<AssessDb>();
        var source=new SqlConnectionStringBuilder(db.Database.GetConnectionString());
        Assert.StartsWith("ProsperaAssess_Test_",source.InitialCatalog);
        var sourceName=source.InitialCatalog; var restored="ProsperaAssess_Test_Restore_"+Guid.NewGuid().ToString("N");
        var backupName="prospera-restore-test-"+Guid.NewGuid().ToString("N")+".bak";
        var assessment=await fixture.SeedAssessment();
        var batch=new AiBatch(Guid.NewGuid()){AssessmentId=assessment.GetProperty("id").GetGuid(),Paper=[1,2,3],Memo=[4,5,6],CreatedAt=DateTimeOffset.UtcNow};
        var script=new AiScript(Guid.NewGuid()){BatchId=batch.Id,Candidate="SYNTHETIC-BACKUP-ONLY",Content=System.Text.Encoding.UTF8.GetBytes("Synthetic document bytes to verify after restore")};
        db.AddRange(batch,script);await db.SaveChangesAsync();
        var expected=await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM AspNetUsers").SingleAsync();
        source.InitialCatalog="master"; await using var connection=new SqlConnection(source.ConnectionString); await connection.OpenAsync();
        async Task Execute(string sql) {await using var command=new SqlCommand(sql,connection){CommandTimeout=180};await command.ExecuteNonQueryAsync();}
        await using var directoryCommand=new SqlCommand("SELECT CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultDataPath'))",connection);
        var directory=(string)(await directoryCommand.ExecuteScalarAsync())!; var path=directory+backupName;
        Assert.DoesNotContain("'",path);
        try
        {
            await Execute($"BACKUP DATABASE [{sourceName}] TO DISK=N'{path}' WITH COPY_ONLY,CHECKSUM,COMPRESSION");
            await Execute($"RESTORE VERIFYONLY FROM DISK=N'{path}' WITH CHECKSUM");
            await using var files=new SqlCommand($"RESTORE FILELISTONLY FROM DISK=N'{path}'",connection);
            var moves=new List<string>();await using(var reader=await files.ExecuteReaderAsync()) {var index=0;while(await reader.ReadAsync()){var logical=reader.GetString(reader.GetOrdinal("LogicalName")).Replace("'","''");var type=reader.GetString(reader.GetOrdinal("Type"));moves.Add($"MOVE N'{logical}' TO N'{directory}{restored}_{index++}.{(type=="L"?"ldf":"mdf")}'");}}
            await Execute($"RESTORE DATABASE [{restored}] FROM DISK=N'{path}' WITH CHECKSUM,RECOVERY,"+string.Join(',',moves));
            await Execute($"DBCC CHECKDB ([{restored}]) WITH NO_INFOMSGS");
            await using var count=new SqlCommand($"SELECT COUNT(*) FROM [{restored}].dbo.AspNetUsers",connection);
            Assert.Equal(expected,Convert.ToInt32(await count.ExecuteScalarAsync()));
            await using var content=new SqlCommand($"SELECT HASHBYTES('SHA2_256',Content) FROM [{restored}].dbo.AiScripts WHERE Id=@id",connection);content.Parameters.AddWithValue("@id",script.Id);
            Assert.Equal(System.Security.Cryptography.SHA256.HashData(script.Content),(byte[])(await content.ExecuteScalarAsync())!);
            var evidence=Environment.GetEnvironmentVariable("PROSPERA_BACKUP_EVIDENCE_OUTPUT");
            if(!string.IsNullOrWhiteSpace(evidence)) {Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(evidence))!);await File.WriteAllTextAsync(evidence,System.Text.Json.JsonSerializer.Serialize(new {checkedAt=DateTimeOffset.UtcNow,backupChecksum=true,restored=true,integrityCheck=true,syntheticDataOnly=true,backupFile=backupName}));}
        }
        finally
        {
            // The target name is generated above and never supplied by the caller.
            Assert.StartsWith("ProsperaAssess_Test_Restore_",restored);
            await Execute($"IF DB_ID(N'{restored}') IS NOT NULL BEGIN ALTER DATABASE [{restored}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{restored}]; END");
        }
    }
}
