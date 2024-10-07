using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Models.DocumentProcess;
using StackExchange.Redis;

namespace Microsoft.Greenlight.Shared.Repositories;

public class DocumentOutlineRepository : GenericRepository<DocumentOutline>
{
    private readonly TimeSpan DefaultCacheDuration = TimeSpan.FromMinutes(60);

    public DocumentOutlineRepository(DocGenerationDbContext dbContext, IConnectionMultiplexer redisConnection) : base(dbContext, redisConnection)
    {
        SetCacheDuration(DefaultCacheDuration);
    }
}
