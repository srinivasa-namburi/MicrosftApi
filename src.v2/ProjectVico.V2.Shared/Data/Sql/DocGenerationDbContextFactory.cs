using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ProjectVico.V2.Shared.Data.Sql;

public class DocGenerationDbContextFactory : IDesignTimeDbContextFactory<DocGenerationDbContext>
{
    public DocGenerationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DocGenerationDbContext>();
        optionsBuilder.UseSqlServer("Server=localhost;Database=ProjectVICO;User Id=sa;Password=5YCzxi!B6xM4HJFp?;");
        return new DocGenerationDbContext(optionsBuilder.Options);
    }
}