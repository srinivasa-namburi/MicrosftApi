using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.Shared.Testing.SQLite
{
    /// <summary>
    /// The database context to be used for testing only for the document generation system.
    /// </summary>
    public class TestDocGenerationDbContext(DbContextOptions<DocGenerationDbContext> options)
        : DocGenerationDbContext(options)
    {
        /// <summary>
        /// Configures the database context for testing.
        /// </summary>
        /// <remarks>
        /// Sets the row version property to be optional for all entities that inherit from 
        /// <see cref="EntityBase"/> for compatibility with SQLite.
        /// 
        /// Also sets the column type for
        /// nvarchar(max) to TEXT for SQLite since nvarchar(max) isn't supported in SQLite.
        /// </remarks>
        /// <param name="builder">The <see cref="ModelBuilder"/></param>
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            foreach (var entityType in builder.Model.GetEntityTypes())
            {
                if (entityType.ClrType.IsSubclassOf(typeof(EntityBase)))
                {
                    builder.Entity(entityType.ClrType)
                        .Property(typeof(byte[]), nameof(EntityBase.RowVersion))
                        .IsRequired(false);
                }
            }

            if (Database.IsSqlite())
            {
                foreach (var entityType in builder.Model.GetEntityTypes())
                {
                    foreach (var property in entityType.GetProperties())
                    {
                        if (property.GetColumnType() == "nvarchar(max)")
                        {
                            property.SetColumnType("TEXT");
                        }
                    }
                }
            }
        }
    }
}

