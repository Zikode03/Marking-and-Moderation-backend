using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace ProsperaAssess;

public class AssessDbFactory : IDesignTimeDbContextFactory<AssessDb>
{
    public AssessDb CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Default") ?? "Server=localhost;Database=ProsperaAssess;Trusted_Connection=True;TrustServerCertificate=True";
        return new AssessDb(new DbContextOptionsBuilder<AssessDb>().UseSqlServer(connection).Options);
    }
}
