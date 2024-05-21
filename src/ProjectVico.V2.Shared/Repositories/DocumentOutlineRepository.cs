using ProjectVico.V2.Shared.Data.Sql;
using ProjectVico.V2.Shared.Models.DocumentProcess;
using StackExchange.Redis;

namespace ProjectVico.V2.Shared.Repositories;

public class DocumentOutlineRepository : GenericRepository<DocumentOutline>
{
    private readonly TimeSpan DefaultCacheDuration = TimeSpan.FromMinutes(60);

    public DocumentOutlineRepository(DocGenerationDbContext dbContext, IConnectionMultiplexer redisConnection) : base(dbContext, redisConnection)
    {
        SetCacheDuration(DefaultCacheDuration);
    }
}