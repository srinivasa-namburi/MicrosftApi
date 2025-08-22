// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Greenlight.Shared.Data.Sql;

namespace Microsoft.Greenlight.Grains.Shared.DesignTime;

/// <summary>
/// Design-time factory so EF CLI can create DocGenerationDbContext when the migrations
/// are placed in the Microsoft.Greenlight.Grains.Shared project.
/// </summary>
public sealed class DesignTimeDocGenerationDbContextFactory : IDesignTimeDbContextFactory<DocGenerationDbContext>
{
    public DocGenerationDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<DocGenerationDbContext>();

        // Use a conservative default connection; the value is irrelevant for code generation,
        // but must be syntactically valid. The runtime connection is configured elsewhere.
        var connectionString = Environment.GetEnvironmentVariable("GL_EFTOOLS_CONN")
                               ?? "Server=(localdb)\\MSSQLLocalDB;Database=ProjectVicoDB;Trusted_Connection=True;TrustServerCertificate=True;";

        builder.UseSqlServer(connectionString, sql =>
        {
            // Ensure migrations live in this assembly
            sql.MigrationsAssembly(typeof(DesignTimeDocGenerationDbContextFactory).Assembly.FullName);
        });

        return new DocGenerationDbContext(builder.Options);
    }
}
