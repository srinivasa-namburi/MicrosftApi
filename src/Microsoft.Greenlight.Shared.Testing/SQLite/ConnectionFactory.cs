using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Shared.Data.Sql;
using System.Data.Common;

namespace Microsoft.Greenlight.Shared.Testing.SQLite
{
    /// <summary>
    /// Factory for creating SQLite in-memory database connections that can be used for unit testing.
    /// </summary>
    public class ConnectionFactory : IDisposable
    {
        private DbConnection? _connection;

        /// <summary>
        /// Creates a new instance of the <see cref="TestDocGenerationDbContext"/> class using SQLite.
        /// </summary>
        /// <returns>A new <see cref="TestDocGenerationDbContext"/> instance.</returns>
        public TestDocGenerationDbContext CreateContext()
        {
            if (_connection == null)
            {
                _connection = new SqliteConnection("Filename=:memory:");
                _connection.Open();

                var options = CreateOptions();
                using var context = new TestDocGenerationDbContext(options);
                context.Database.EnsureCreated();
            }

            return new TestDocGenerationDbContext(CreateOptions());
        }

        private DbContextOptions<DocGenerationDbContext> CreateOptions()
        {
            return new DbContextOptionsBuilder<DocGenerationDbContext>()
                .UseSqlite(_connection!)
                .Options;
        }

        /// <summary>
        /// Disposes the SQLite connection.
        /// </summary>
        public void Dispose()
        {
            if (_connection != null)
            {
                _connection.Dispose();
                _connection = null;
            }
            GC.SuppressFinalize(this);
        }
    }
}
