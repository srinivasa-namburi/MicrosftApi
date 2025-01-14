using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Microsoft.Greenlight.Shared.Data.Sql;

/// <summary>
/// Factory for creating instances of <see cref="DocGenerationDbContext"/> at design time.
/// </summary>
public class DocGenerationDbContextFactory : IDesignTimeDbContextFactory<DocGenerationDbContext>
{
    /// <summary>
    /// Creates a new instance of <see cref="DocGenerationDbContext"/>.
    /// </summary>
    /// <param name="args">Arguments for creating the context.</param>
    /// <returns>A new instance of <see cref="DocGenerationDbContext"/>.</returns>
    public DocGenerationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DocGenerationDbContext>();
        optionsBuilder.UseSqlServer("Server=localhost;Database=ProjectVICODb;");
        return new DocGenerationDbContext(optionsBuilder.Options);
    }
}
