using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Models.DocumentProcess;
using StackExchange.Redis;

namespace Microsoft.Greenlight.Shared.Repositories;

/// <summary>
/// Repository for managing <see cref="DocumentOutline"/> entities.
/// </summary>
public class DocumentOutlineRepository : GenericRepository<DocumentOutline>
{
    private readonly TimeSpan DefaultCacheDuration = TimeSpan.FromMinutes(60);

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentOutlineRepository"/> class.
    /// </summary>
    /// <param name="dbContext">The document generation db context.</param>
    /// <param name="redisConnection">The redis connection.</param>
    public DocumentOutlineRepository(DocGenerationDbContext dbContext, IConnectionMultiplexer redisConnection) : base(dbContext, redisConnection)
    {
        SetCacheDuration(DefaultCacheDuration);
    }
}
