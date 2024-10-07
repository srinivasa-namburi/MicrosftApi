using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Microsoft.Greenlight.Shared.Data.Sql;

public class DocGenerationDbContextFactory : IDesignTimeDbContextFactory<DocGenerationDbContext>
{
    public DocGenerationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DocGenerationDbContext>();
        optionsBuilder.UseSqlServer("Server=localhost;Database=ProjectVICODb;");
        return new DocGenerationDbContext(optionsBuilder.Options);
    }
}
